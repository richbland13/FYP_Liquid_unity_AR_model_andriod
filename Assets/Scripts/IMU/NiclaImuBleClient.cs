using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// Connects the Android phone to the Nicla Vision over BLE.
///
/// Expected fixed 16-byte packet:
///
/// 0..3   uint32 timestamp in milliseconds
/// 4..5   int16 acceleration X in milligravity
/// 6..7   int16 acceleration Y in milligravity
/// 8..9   int16 acceleration Z in milligravity
/// 10..11 int16 gyro X in centidegrees/second
/// 12..13 int16 gyro Y in centidegrees/second
/// 14..15 int16 gyro Z in centidegrees/second
///
/// This script handles only BLE transport and packet decoding.
/// It does not calculate liquid motion.
/// </summary>
public sealed class NiclaImuBleClient : MonoBehaviour
{
    private const int ExpectedPacketLength = 16;
    private const int MaximumQueuedPackets = 128;

    private const string BluetoothScanPermission =
    "android.permission.BLUETOOTH_SCAN";

    private const string BluetoothConnectPermission =
        "android.permission.BLUETOOTH_CONNECT";

    private const string FineLocationPermission =
        "android.permission.ACCESS_FINE_LOCATION";

    public enum BleState
    {
        Idle,
        Initialising,
        Scanning,
        WaitingToConnect,
        Connecting,
        Discovering,
        WaitingToSubscribe,
        Subscribing,
        Streaming,
        Disconnecting,
        Error
    }

    [Header("Nicla BLE identity")]

    [SerializeField]
    private string deviceName = "NiclaLiquidIMU";

    [SerializeField]
    private string serviceUuid =
        "7A1E0001-6B3C-4E2B-9F31-46F9C42A0100";

    [SerializeField]
    private string characteristicUuid =
        "7A1E0002-6B3C-4E2B-9F31-46F9C42A0100";

    [Header("Connection")]

    [SerializeField]
    private bool connectAutomatically = true;

    [Min(1.0f)]
    [SerializeField]
    private float scanTimeoutSeconds = 15.0f;

    [Min(0.0f)]
    [SerializeField]
    private float connectDelaySeconds = 0.35f;

    [Min(0.0f)]
    [SerializeField]
    private float subscribeDelaySeconds = 0.75f;

    [Tooltip(
        "Enable only if filtering by the custom service UUID " +
        "does not find the Nicla.")]
    [SerializeField]
    private bool scanWithoutServiceFilter = false;

    [Header("Optional UI")]

    [SerializeField]
    private TMP_Text connectionStatusText;

    [SerializeField]
    private TMP_Text imuValuesText;

    [Header("Diagnostics")]

    [SerializeField]
    private bool logDiscoveredDevices = true;

    public BleState CurrentState { get; private set; } =
        BleState.Idle;

    public NiclaImuSample LatestSample { get; private set; }

    public bool IsStreaming =>
        CurrentState == BleState.Streaming;

    public float PacketAgeSeconds
    {
        get
        {
            if (!LatestSample.IsValid)
            {
                return float.PositiveInfinity;
            }

            return Time.realtimeSinceStartup -
                   LatestSample.UnityReceiveTime;
        }
    }

    private string deviceAddress;

    private float scanStartedAt;
    private float nextOperationAt;

    private bool characteristicFound;
    private bool shuttingDown;

    private ulong sequence;

    private readonly object packetLock = new object();

    private readonly Queue<byte[]> pendingPackets =
        new Queue<byte[]>();

    private int packetsThisWindow;
    private float packetWindowStartedAt;
    private float packetRateHz;

    private void Start()
    {
        packetWindowStartedAt =
            Time.realtimeSinceStartup;

        if (connectAutomatically)
        {
            StartConnection();
        }
        else
        {
            SetStatus("BLE idle");
        }
    }

    private void Update()
    {
        ProcessPendingPackets();
        UpdatePacketRate();
        UpdateImuDisplay();

#if UNITY_ANDROID && !UNITY_EDITOR

        float now = Time.realtimeSinceStartup;

        if (CurrentState == BleState.Scanning &&
            now - scanStartedAt >= scanTimeoutSeconds)
        {
            BluetoothLEHardwareInterface.StopScan();

            Fail(
                $"Scan timed out after " +
                $"{scanTimeoutSeconds:F0} seconds.");
        }

        if (CurrentState == BleState.WaitingToConnect &&
            now >= nextOperationAt)
        {
            ConnectToPeripheral();
        }

        if (CurrentState == BleState.WaitingToSubscribe &&
            now >= nextOperationAt)
        {
            SubscribeToImu();
        }

#endif
    }

    /// <summary>
    /// Can also be connected to a Unity UI button.
    /// </summary>
    public void StartConnection()
    {
    #if UNITY_ANDROID && !UNITY_EDITOR

        if (CurrentState != BleState.Idle &&
            CurrentState != BleState.Error)
        {
            Debug.LogWarning(
                $"BLE is already active in state {CurrentState}.",
                this);

            return;
        }

        ResetRuntimeState();

        RequestBluetoothPermissions();

    #else

        SetStatus(
            "BLE runs in the Android application.\n" +
            "Build and run this scene on the Samsung phone.");

    #endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR

    private void RequestBluetoothPermissions()
    {
        /*
        * Android 12 is API level 31.
        *
        * Android 12+:
        *   BLUETOOTH_SCAN
        *   BLUETOOTH_CONNECT
        *
        * Android 11 and older:
        *   ACCESS_FINE_LOCATION is commonly required for BLE scanning.
        */
        int sdkVersion = GetAndroidSdkVersion();

        if (sdkVersion >= 31)
        {
            bool hasScanPermission =
                Permission.HasUserAuthorizedPermission(
                    BluetoothScanPermission);

            bool hasConnectPermission =
                Permission.HasUserAuthorizedPermission(
                    BluetoothConnectPermission);

            if (hasScanPermission &&
                hasConnectPermission)
            {
                InitialiseBluetooth();
                return;
            }

            SetStatus(
                "Requesting Nearby Devices permission...");

            PermissionCallbacks callbacks =
                new PermissionCallbacks();

            callbacks.PermissionGranted +=
                OnBluetoothPermissionResult;

            callbacks.PermissionDenied +=
                OnBluetoothPermissionDenied;

            callbacks.PermissionDeniedAndDontAskAgain +=
                OnBluetoothPermissionDenied;

            Permission.RequestUserPermissions(
                new[]
                {
                    BluetoothScanPermission,
                    BluetoothConnectPermission
                },
                callbacks);
        }
        else
        {
            if (Permission.HasUserAuthorizedPermission(
                    FineLocationPermission))
            {
                InitialiseBluetooth();
                return;
            }

            SetStatus(
                "Requesting location permission for BLE...");

            PermissionCallbacks callbacks =
                new PermissionCallbacks();

            callbacks.PermissionGranted +=
                OnBluetoothPermissionResult;

            callbacks.PermissionDenied +=
                OnBluetoothPermissionDenied;

            callbacks.PermissionDeniedAndDontAskAgain +=
                OnBluetoothPermissionDenied;

            Permission.RequestUserPermission(
                FineLocationPermission,
                callbacks);
        }
    }

    private void OnBluetoothPermissionResult(
        string permissionName)
    {
        Debug.Log(
            $"Permission granted: {permissionName}",
            this);

        /*
        * A multiple-permission request may call this once for each
        * permission. Only continue after every required permission
        * has actually been granted.
        */
        if (HasRequiredBluetoothPermissions())
        {
            InitialiseBluetooth();
        }
    }

    private void OnBluetoothPermissionDenied(
        string permissionName)
    {
        Fail(
            $"Bluetooth permission was denied:\n" +
            $"{permissionName}\n\n" +
            "Open Android Settings → Apps → " +
            "Beaker AR - IMU → Permissions and allow " +
            "Nearby devices.");
    }

    private bool HasRequiredBluetoothPermissions()
    {
        int sdkVersion = GetAndroidSdkVersion();

        if (sdkVersion >= 31)
        {
            return
                Permission.HasUserAuthorizedPermission(
                    BluetoothScanPermission) &&
                Permission.HasUserAuthorizedPermission(
                    BluetoothConnectPermission);
        }

        return Permission.HasUserAuthorizedPermission(
            FineLocationPermission);
    }

    private static int GetAndroidSdkVersion()
    {
        using AndroidJavaClass versionClass =
            new AndroidJavaClass(
                "android.os.Build$VERSION");

        return versionClass.GetStatic<int>("SDK_INT");
    }

    private void InitialiseBluetooth()
    {
        CurrentState = BleState.Initialising;

        SetStatus("Initialising Bluetooth...");

        BluetoothLEHardwareInterface.Initialize(
            true,
            false,
            BeginScan,
            error =>
            {
                Fail(
                    $"Bluetooth initialisation failed:\n" +
                    $"{error}");
            });
    }

    #endif


#if UNITY_ANDROID && !UNITY_EDITOR

    private void BeginScan()
    {
        if (shuttingDown)
        {
            return;
        }

        CurrentState = BleState.Scanning;

        scanStartedAt =
            Time.realtimeSinceStartup;

        SetStatus(
            $"Scanning for {deviceName}...");

        BluetoothLEHardwareInterface.BluetoothScanMode(
            BluetoothLEHardwareInterface
                .ScanMode.LowLatency);

        string[] serviceFilters =
            scanWithoutServiceFilter
                ? null
                : new[] { serviceUuid };

        BluetoothLEHardwareInterface
            .ScanForPeripheralsWithServices(
                serviceFilters,
                OnPeripheralFound,
                null,
                false,
                true);
    }

    private void OnPeripheralFound(
        string address,
        string advertisedName)
    {
        if (CurrentState != BleState.Scanning)
        {
            return;
        }

        if (logDiscoveredDevices)
        {
            Debug.Log(
                $"BLE discovery: " +
                $"name='{advertisedName}', " +
                $"address='{address}'",
                this);
        }

        bool nameMatches =
            !string.IsNullOrWhiteSpace(
                advertisedName) &&
            advertisedName.IndexOf(
                deviceName,
                StringComparison.OrdinalIgnoreCase) >= 0;

        /*
         * The scan is already filtered using the custom service.
         * Some Android devices report an empty local name initially.
         */
        bool acceptableUnnamedResult =
            !scanWithoutServiceFilter &&
            string.IsNullOrWhiteSpace(
                advertisedName);

        if (!nameMatches &&
            !acceptableUnnamedResult)
        {
            return;
        }

        deviceAddress = address;

        BluetoothLEHardwareInterface.StopScan();

        CurrentState =
            BleState.WaitingToConnect;

        nextOperationAt =
            Time.realtimeSinceStartup +
            connectDelaySeconds;

        string displayName =
            string.IsNullOrWhiteSpace(advertisedName)
                ? deviceName
                : advertisedName;

        SetStatus(
            $"Found {displayName}\n" +
            "Preparing connection...");
    }

    private void ConnectToPeripheral()
    {
        if (string.IsNullOrWhiteSpace(
                deviceAddress))
        {
            Fail(
                "No BLE device address was recorded.");

            return;
        }

        CurrentState = BleState.Connecting;

        characteristicFound = false;

        SetStatus("Connecting...");

        BluetoothLEHardwareInterface
            .ConnectToPeripheral(
                deviceAddress,

                connectedAddress =>
                {
                    if (shuttingDown)
                    {
                        return;
                    }

                    CurrentState =
                        BleState.Discovering;

                    SetStatus(
                        "Connected\n" +
                        "Discovering services...");
                },

                (
                    address,
                    discoveredServiceUuid) =>
                {
                    if (logDiscoveredDevices)
                    {
                        Debug.Log(
                            $"BLE service: " +
                            $"{discoveredServiceUuid}",
                            this);
                    }
                },

                (
                    address,
                    discoveredServiceUuid,
                    discoveredCharacteristicUuid) =>
                {
                    if (logDiscoveredDevices)
                    {
                        Debug.Log(
                            $"BLE characteristic: " +
                            $"{discoveredServiceUuid} / " +
                            $"{discoveredCharacteristicUuid}",
                            this);
                    }

                    if (characteristicFound)
                    {
                        return;
                    }

                    bool correctService =
                        UuidEquals(
                            discoveredServiceUuid,
                            serviceUuid);

                    bool correctCharacteristic =
                        UuidEquals(
                            discoveredCharacteristicUuid,
                            characteristicUuid);

                    if (!correctService ||
                        !correctCharacteristic)
                    {
                        return;
                    }

                    characteristicFound = true;

                    CurrentState =
                        BleState.WaitingToSubscribe;

                    nextOperationAt =
                        Time.realtimeSinceStartup +
                        subscribeDelaySeconds;

                    SetStatus(
                        "IMU characteristic found\n" +
                        "Preparing subscription...");
                },

                disconnectedAddress =>
                {
                    if (shuttingDown)
                    {
                        return;
                    }

                    Fail(
                        $"Nicla disconnected:\n" +
                        $"{disconnectedAddress}");
                });
    }

    private void SubscribeToImu()
    {
        if (!characteristicFound)
        {
            Fail(
                "The IMU characteristic was not found.");

            return;
        }

        CurrentState = BleState.Subscribing;

        SetStatus(
            "Subscribing to IMU notifications...");

        BluetoothLEHardwareInterface
            .BluetoothConnectionPriority(
                BluetoothLEHardwareInterface
                    .ConnectionPriority.High);

        BluetoothLEHardwareInterface
            .SubscribeCharacteristicWithDeviceAddress(
                deviceAddress,
                serviceUuid,
                characteristicUuid,

                (
                    address,
                    subscribedCharacteristicUuid) =>
                {
                    if (shuttingDown)
                    {
                        return;
                    }

                    CurrentState =
                        BleState.Streaming;

                    SetStatus(
                        "Nicla connected\n" +
                        "Waiting for IMU packets...");
                },

                (
                    address,
                    updatedCharacteristicUuid,
                    bytes) =>
                {
                    if (bytes == null ||
                        bytes.Length == 0)
                    {
                        return;
                    }

                    byte[] copy =
                        new byte[bytes.Length];

                    Buffer.BlockCopy(
                        bytes,
                        0,
                        copy,
                        0,
                        bytes.Length);

                    lock (packetLock)
                    {
                        while (
                            pendingPackets.Count >=
                            MaximumQueuedPackets)
                        {
                            pendingPackets.Dequeue();
                        }

                        pendingPackets.Enqueue(copy);
                    }
                });
    }

#endif

    private void ProcessPendingPackets()
    {
        int processedThisFrame = 0;

        while (processedThisFrame < 64)
        {
            byte[] packet = null;

            lock (packetLock)
            {
                if (pendingPackets.Count > 0)
                {
                    packet =
                        pendingPackets.Dequeue();
                }
            }

            if (packet == null)
            {
                break;
            }

            processedThisFrame++;

            DecodePacket(packet);
        }
    }

    private void DecodePacket(byte[] packet)
    {
        if (packet.Length != ExpectedPacketLength)
        {
            Debug.LogWarning(
                $"Unexpected IMU packet length " +
                $"{packet.Length}; expected " +
                $"{ExpectedPacketLength}.",
                this);

            return;
        }

        uint timestamp =
            ReadUInt32LittleEndian(packet, 0);

        short axMilligravity =
            ReadInt16LittleEndian(packet, 4);

        short ayMilligravity =
            ReadInt16LittleEndian(packet, 6);

        short azMilligravity =
            ReadInt16LittleEndian(packet, 8);

        short gxCentiDegreesPerSecond =
            ReadInt16LittleEndian(packet, 10);

        short gyCentiDegreesPerSecond =
            ReadInt16LittleEndian(packet, 12);

        short gzCentiDegreesPerSecond =
            ReadInt16LittleEndian(packet, 14);

        sequence++;

        LatestSample = new NiclaImuSample
        {
            TimestampMilliseconds =
                timestamp,

            AccelerationG = new Vector3(
                axMilligravity / 1000.0f,
                ayMilligravity / 1000.0f,
                azMilligravity / 1000.0f),

            AngularVelocityDegreesPerSecond =
                new Vector3(
                    gxCentiDegreesPerSecond /
                    100.0f,

                    gyCentiDegreesPerSecond /
                    100.0f,

                    gzCentiDegreesPerSecond /
                    100.0f),

            UnityReceiveTime =
                Time.realtimeSinceStartup,

            Sequence = sequence,

            IsValid = true
        };

        packetsThisWindow++;

        if (CurrentState != BleState.Streaming)
        {
            CurrentState =
                BleState.Streaming;

            SetStatus(
                "Nicla connected\n" +
                "IMU data streaming");
        }
    }

    private void UpdatePacketRate()
    {
        float now =
            Time.realtimeSinceStartup;

        float elapsed =
            now - packetWindowStartedAt;

        if (elapsed < 1.0f)
        {
            return;
        }

        packetRateHz =
            packetsThisWindow / elapsed;

        packetsThisWindow = 0;

        packetWindowStartedAt = now;
    }

    private void UpdateImuDisplay()
    {
        if (imuValuesText == null)
        {
            return;
        }

        if (!LatestSample.IsValid)
        {
            imuValuesText.text =
                "No IMU data received.";

            return;
        }

        Vector3 acceleration =
            LatestSample.AccelerationG;

        Vector3 gyro =
            LatestSample
                .AngularVelocityDegreesPerSecond;

        imuValuesText.text =
            $"IMU rate: {packetRateHz:F1} Hz\n" +
            $"Packet age: " +
            $"{PacketAgeSeconds * 1000.0f:F0} ms\n\n" +

            $"Acceleration (g)\n" +
            $"X: {acceleration.x,7:F3}\n" +
            $"Y: {acceleration.y,7:F3}\n" +
            $"Z: {acceleration.z,7:F3}\n" +
            $"|a|: {acceleration.magnitude:F3}\n\n" +

            $"Gyroscope (deg/s)\n" +
            $"X: {gyro.x,7:F2}\n" +
            $"Y: {gyro.y,7:F2}\n" +
            $"Z: {gyro.z,7:F2}";
    }

    public bool HasFreshSample(
        float maximumAgeSeconds = 0.25f)
    {
        return LatestSample.IsValid &&
               PacketAgeSeconds <=
               maximumAgeSeconds;
    }

    public void Disconnect()
    {
#if UNITY_ANDROID && !UNITY_EDITOR

        if (CurrentState ==
            BleState.Disconnecting)
        {
            return;
        }

        CurrentState =
            BleState.Disconnecting;

        SetStatus("Disconnecting...");

        if (!string.IsNullOrWhiteSpace(
                deviceAddress))
        {
            BluetoothLEHardwareInterface
                .DisconnectPeripheral(
                    deviceAddress,
                    disconnectedAddress =>
                    {
                        BluetoothLEHardwareInterface
                            .DeInitialize(
                                () =>
                                {
                                    ResetRuntimeState();

                                    SetStatus(
                                        "Bluetooth " +
                                        "disconnected.");
                                });
                    });
        }
        else
        {
            BluetoothLEHardwareInterface
                .DeInitialize(
                    () =>
                    {
                        ResetRuntimeState();

                        SetStatus(
                            "Bluetooth deinitialised.");
                    });
        }

#endif
    }

    private void ResetRuntimeState()
    {
        deviceAddress = null;

        characteristicFound = false;

        sequence = 0;

        LatestSample = default;

        packetRateHz = 0.0f;

        packetsThisWindow = 0;

        packetWindowStartedAt =
            Time.realtimeSinceStartup;

        lock (packetLock)
        {
            pendingPackets.Clear();
        }

        CurrentState = BleState.Idle;
    }

    private void SetStatus(string message)
    {
        Debug.Log(
            $"[Nicla BLE] {message}",
            this);

        if (connectionStatusText != null)
        {
            connectionStatusText.text =
                message;
        }
    }

    private void Fail(string message)
    {
        CurrentState = BleState.Error;

        Debug.LogError(
            $"[Nicla BLE] {message}",
            this);

        if (connectionStatusText != null)
        {
            connectionStatusText.text =
                $"BLE ERROR\n{message}";
        }
    }

    private void OnApplicationPause(
        bool paused)
    {
#if UNITY_ANDROID && !UNITY_EDITOR

        BluetoothLEHardwareInterface
            .PauseMessages(paused);

#endif
    }

    private void OnApplicationQuit()
    {
        shuttingDown = true;

#if UNITY_ANDROID && !UNITY_EDITOR

        try
        {
            BluetoothLEHardwareInterface
                .DeInitialize(() => { });
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"BLE shutdown warning: " +
                $"{exception.Message}",
                this);
        }

#endif
    }

    private static short
        ReadInt16LittleEndian(
            byte[] bytes,
            int offset)
    {
        ushort raw =
            (ushort)(
                bytes[offset] |
                (bytes[offset + 1] << 8));

        return unchecked((short)raw);
    }

    private static uint
        ReadUInt32LittleEndian(
            byte[] bytes,
            int offset)
    {
        return
            (uint)bytes[offset] |
            ((uint)bytes[offset + 1] << 8) |
            ((uint)bytes[offset + 2] << 16) |
            ((uint)bytes[offset + 3] << 24);
    }

    private static bool UuidEquals(
        string first,
        string second)
    {
        if (string.IsNullOrWhiteSpace(first) ||
            string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        return
            NormaliseUuid(first) ==
            NormaliseUuid(second);
    }

    private static string NormaliseUuid(
        string uuid)
    {
        return uuid
            .Replace("-", string.Empty)
            .Trim()
            .ToUpperInvariant();
    }
}