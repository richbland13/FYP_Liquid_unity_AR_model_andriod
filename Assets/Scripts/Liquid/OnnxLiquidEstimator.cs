using UnityEngine;
using Unity.InferenceEngine;

/// <summary>
/// Runs a fixed-window ONNX liquid-state model.
///
/// Required model interface:
/// Input:  "imu_window",  float32 [1, 7, 50]
/// Output: one float32 tensor [1, 4]
/// </summary>
[DefaultExecutionOrder(130)]
public sealed class OnnxLiquidEstimator : LiquidStateProvider
{
    private const int ChannelCount = 7;
    private const int WindowLength = 50;
    private const int OutputCount = 4;

    [Header("Model")]
    [SerializeField] private ModelAsset modelAsset;
    [SerializeField] private BackendType backend = BackendType.CPU;

    [Header("Input references")]
    [SerializeField] private NiclaImuMotionProvider imuMotionProvider;
    [SerializeField] private BeakerGeometry geometry;

    [Header("Timing")]
    [Min(1.0f)]
    [SerializeField] private float inputSampleRateHertz = 50.0f;

    [Min(1.0f)]
    [SerializeField] private float inferenceRateHertz = 10.0f;

    [Min(0.1f)]
    [SerializeField] private float outputTimeoutSeconds = 0.5f;

    [Header("Preprocessing")]
    [Min(0.001f)]
    [SerializeField] private float gyroNormalisation = 180.0f;

    [Header("Output safety")]
    [Min(0.1f)]
    [SerializeField] private float maximumSlopeMagnitude = 5.0f;

    [Min(0.1f)]
    [SerializeField] private float maximumSlopeVelocityMagnitude = 20.0f;

    private readonly float[,] history =
        new float[ChannelCount, WindowLength];

    private readonly float[] packedInput =
        new float[ChannelCount * WindowLength];

    private Model runtimeModel;
    private Worker worker;

    private int writeIndex;
    private int sampleCount;

    private float sampleAccumulator;
    private float inferenceAccumulator;
    private float lastInferenceTime = float.NegativeInfinity;

    private LiquidState currentState = LiquidState.Level;
    private bool modelLoaded;
    private bool hasValidState;

    public override LiquidState CurrentState => currentState;

    public override bool HasValidState =>
        modelLoaded &&
        hasValidState &&
        sampleCount >= WindowLength &&
        Time.unscaledTime - lastInferenceTime <= outputTimeoutSeconds;

    public bool ModelLoaded => modelLoaded;
    public int SamplesInWindow => sampleCount;
    public int RequiredWindowSamples => WindowLength;
    public float LastInferenceMilliseconds { get; private set; }

    private void Start()
    {
        LoadModel();
        ResetEstimator();
    }

    private void Update()
    {
        if (!modelLoaded ||
            worker == null ||
            imuMotionProvider == null ||
            geometry == null)
        {
            return;
        }

        if (!imuMotionProvider.HasValidInput)
        {
            hasValidState = false;
            return;
        }

        float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0f);

        sampleAccumulator += deltaTime;
        inferenceAccumulator += deltaTime;

        float samplePeriod =
            1.0f / Mathf.Max(inputSampleRateHertz, 1.0f);

        int samplesAddedThisFrame = 0;

        while (sampleAccumulator >= samplePeriod &&
               samplesAddedThisFrame < 4)
        {
            sampleAccumulator -= samplePeriod;
            AddCurrentSample();
            samplesAddedThisFrame++;
        }

        float inferencePeriod =
            1.0f / Mathf.Max(inferenceRateHertz, 1.0f);

        if (sampleCount < WindowLength ||
            inferenceAccumulator < inferencePeriod)
        {
            return;
        }

        inferenceAccumulator = 0.0f;
        RunInference();
    }

    private void LoadModel()
    {
        DisposeWorker();

        if (modelAsset == null)
        {
            Debug.LogError(
                "OnnxLiquidEstimator: no Model Asset assigned.",
                this);

            return;
        }

        try
        {
            runtimeModel = ModelLoader.Load(modelAsset);
            worker = new Worker(runtimeModel, backend);
            modelLoaded = true;

            Debug.Log(
                "ONNX liquid model loaded successfully.",
                this);
        }
        catch (System.Exception exception)
        {
            modelLoaded = false;

            Debug.LogError(
                "Failed to load ONNX liquid model:\n" + exception,
                this);
        }
    }

    private void AddCurrentSample()
    {
        Vector3 acceleration =
            imuMotionProvider.FilteredAccelerationBeakerG;

        Vector3 gyro =
            imuMotionProvider.FilteredGyroBeakerDegreesPerSecond /
            Mathf.Max(gyroNormalisation, 0.001f);

        history[0, writeIndex] = acceleration.x;
        history[1, writeIndex] = acceleration.y;
        history[2, writeIndex] = acceleration.z;

        history[3, writeIndex] = gyro.x;
        history[4, writeIndex] = gyro.y;
        history[5, writeIndex] = gyro.z;

        history[6, writeIndex] = geometry.FillFraction;

        writeIndex = (writeIndex + 1) % WindowLength;
        sampleCount = Mathf.Min(sampleCount + 1, WindowLength);
    }

    private void PackInput()
    {
        int oldestIndex =
            sampleCount >= WindowLength ? writeIndex : 0;

        for (int channel = 0; channel < ChannelCount; channel++)
        {
            for (int timeIndex = 0;
                 timeIndex < WindowLength;
                 timeIndex++)
            {
                int historyIndex =
                    (oldestIndex + timeIndex) % WindowLength;

                int packedIndex =
                    channel * WindowLength + timeIndex;

                packedInput[packedIndex] =
                    history[channel, historyIndex];
            }
        }
    }

    private void RunInference()
    {
        PackInput();

        float startTime = Time.realtimeSinceStartup;
        Tensor<float> inputTensor = null;

        try
        {
            inputTensor =
                new Tensor<float>(
                    new TensorShape(
                        1,
                        ChannelCount,
                        WindowLength),
                    packedInput);

            worker.Schedule(inputTensor);

            Tensor<float> outputTensor =
                worker.PeekOutput() as Tensor<float>;

            if (outputTensor == null)
            {
                throw new System.InvalidOperationException(
                    "The ONNX output is not a float tensor.");
            }

            float[] output = outputTensor.DownloadToArray();

            if (output == null || output.Length < OutputCount)
            {
                throw new System.InvalidOperationException(
                    "The ONNX output must contain four values.");
            }

            for (int i = 0; i < OutputCount; i++)
            {
                if (float.IsNaN(output[i]) ||
                    float.IsInfinity(output[i]))
                {
                    throw new System.InvalidOperationException(
                        "The ONNX model returned NaN or Infinity.");
                }
            }

            Vector2 slope =
                new Vector2(output[0], output[1]);

            Vector2 slopeVelocity =
                new Vector2(output[2], output[3]);

            slope =
                Vector2.ClampMagnitude(
                    slope,
                    maximumSlopeMagnitude);

            slopeVelocity =
                Vector2.ClampMagnitude(
                    slopeVelocity,
                    maximumSlopeVelocityMagnitude);

            currentState =
                new LiquidState(slope, slopeVelocity);

            lastInferenceTime = Time.unscaledTime;
            hasValidState = true;
        }
        catch (System.Exception exception)
        {
            hasValidState = false;

            Debug.LogError(
                "ONNX liquid inference failed:\n" + exception,
                this);
        }
        finally
        {
            if (inputTensor != null)
            {
                inputTensor.Dispose();
            }

            LastInferenceMilliseconds =
                (Time.realtimeSinceStartup - startTime) * 1000.0f;
        }
    }

    public void ResetEstimator()
    {
        System.Array.Clear(history, 0, history.Length);
        System.Array.Clear(packedInput, 0, packedInput.Length);

        writeIndex = 0;
        sampleCount = 0;
        sampleAccumulator = 0.0f;
        inferenceAccumulator = 0.0f;
        lastInferenceTime = float.NegativeInfinity;
        LastInferenceMilliseconds = 0.0f;
        currentState = LiquidState.Level;
        hasValidState = false;
    }

    public void ReloadModel()
    {
        LoadModel();
        ResetEstimator();
    }

    private void OnDestroy()
    {
        DisposeWorker();
    }

    private void DisposeWorker()
    {
        if (worker != null)
        {
            worker.Dispose();
            worker = null;
        }

        runtimeModel = null;
        modelLoaded = false;
        hasValidState = false;
    }
}
