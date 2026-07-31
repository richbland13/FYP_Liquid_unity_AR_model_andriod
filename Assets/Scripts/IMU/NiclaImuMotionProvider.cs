using UnityEngine;

/// <summary>
/// Maps the Nicla accelerometer and gyroscope axes into the
/// beaker coordinate frame and applies simple low-pass filtering.
///
/// Beaker coordinate convention:
/// +X = right when looking at the beaker from the front
/// +Y = upward through the beaker
/// +Z = forward through the front of the beaker
/// </summary>
[DefaultExecutionOrder(50)]
public sealed class NiclaImuMotionProvider : MonoBehaviour
{
    [Header("BLE source")]

    [SerializeField]
    private NiclaImuBleClient bleClient;

    [Header("Sensor-to-beaker mapping")]

    /*
     * Your measured Nicla orientation:
     *
     * Upright beaker:
     * Sensor X = +1 g
     *
     * Left side facing down:
     * Sensor Y = +1 g
     *
     * Beaker front facing down:
     * Sensor Z = +1 g
     *
     * Therefore:
     * Beaker X = Sensor Y
     * Beaker Y = Sensor X
     * Beaker Z = -Sensor Z
     */

    [SerializeField]
    private Vector3 beakerXFromSensor =
        new Vector3(0.0f, 1.0f, 0.0f);

    [SerializeField]
    private Vector3 beakerYFromSensor =
        new Vector3(1.0f, 0.0f, 0.0f);

    [SerializeField]
    private Vector3 beakerZFromSensor =
        new Vector3(0.0f, 0.0f, -1.0f);

    [Header("Filtering")]

    [Min(0.1f)]
    [SerializeField]
    private float accelerationFilterRate = 10.0f;

    [Min(0.1f)]
    [SerializeField]
    private float gyroFilterRate = 15.0f;

    [Min(0.01f)]
    [SerializeField]
    private float maximumPacketAgeSeconds = 0.25f;

    public Vector3 FilteredAccelerationBeakerG
    {
        get;
        private set;
    }

    public Vector3 FilteredGyroBeakerDegreesPerSecond
    {
        get;
        private set;
    }

    public bool HasValidInput
    {
        get;
        private set;
    }

    private ulong lastSequence;
    private float lastReceiveTime;

    private void Update()
    {
        if (bleClient == null ||
            !bleClient.HasFreshSample(maximumPacketAgeSeconds))
        {
            HasValidInput = false;
            return;
        }

        NiclaImuSample sample = bleClient.LatestSample;

        if (!sample.IsValid)
        {
            HasValidInput = false;
            return;
        }

        /*
         * There is nothing new to process if this sequence
         * number was already handled.
         */
        if (sample.Sequence == lastSequence)
        {
            return;
        }

        lastSequence = sample.Sequence;

        Vector3 mappedAcceleration =
            MapSensorToBeaker(sample.AccelerationG);

        Vector3 mappedGyro =
            MapSensorToBeaker(
                sample.AngularVelocityDegreesPerSecond);

        float deltaTime =
            lastReceiveTime > 0.0f
                ? Mathf.Max(
                    sample.UnityReceiveTime - lastReceiveTime,
                    0.001f)
                : 0.02f;

        lastReceiveTime = sample.UnityReceiveTime;

        float accelerationBlend =
            1.0f -
            Mathf.Exp(
                -accelerationFilterRate * deltaTime);

        float gyroBlend =
            1.0f -
            Mathf.Exp(
                -gyroFilterRate * deltaTime);

        if (!HasValidInput)
        {
            FilteredAccelerationBeakerG =
                mappedAcceleration;

            FilteredGyroBeakerDegreesPerSecond =
                mappedGyro;
        }
        else
        {
            FilteredAccelerationBeakerG =
                Vector3.Lerp(
                    FilteredAccelerationBeakerG,
                    mappedAcceleration,
                    accelerationBlend);

            FilteredGyroBeakerDegreesPerSecond =
                Vector3.Lerp(
                    FilteredGyroBeakerDegreesPerSecond,
                    mappedGyro,
                    gyroBlend);
        }

        HasValidInput = true;
    }

    private Vector3 MapSensorToBeaker(
        Vector3 sensorVector)
    {
        return new Vector3(
            Vector3.Dot(
                sensorVector,
                beakerXFromSensor),

            Vector3.Dot(
                sensorVector,
                beakerYFromSensor),

            Vector3.Dot(
                sensorVector,
                beakerZFromSensor));
    }
}