using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporary stand-in for the future temporal CNN.
///
/// This is NOT an AI model. It reads the classical state, applies
/// a fixed delay and smoothing, and returns the same four outputs.
/// Its purpose is only to test the complete model-switching pipeline.
/// </summary>
[DefaultExecutionOrder(120)]
public sealed class PlaceholderNeuralLiquidEstimator : LiquidStateProvider
{
    private struct TimedState
    {
        public float Time;
        public LiquidState State;
    }

    [Header("Temporary source")]
    [SerializeField]
    private ClassicalLiquidEstimator classicalSource;

    [SerializeField]
    private BeakerGeometry geometry;

    [Header("Placeholder behaviour")]
    [Min(0.0f)]
    [SerializeField]
    private float simulatedInferenceDelaySeconds = 0.15f;

    [Min(0.1f)]
    [SerializeField]
    private float outputSmoothingRate = 8.0f;

    [Header("Future CNN training envelope")]
    [Min(0.005f)]
    [SerializeField]
    private float trainedInnerRadius = 0.032f;

    [Min(0.020f)]
    [SerializeField]
    private float trainedInternalHeight = 0.120f;

    [Range(0.05f, 0.95f)]
    [SerializeField]
    private float minimumTrainedFillFraction = 0.30f;

    [Range(0.05f, 0.95f)]
    [SerializeField]
    private float maximumTrainedFillFraction = 0.80f;

    private readonly Queue<TimedState> history = new Queue<TimedState>();

    private LiquidState currentState = LiquidState.Level;
    private bool hasValidState;

    public override LiquidState CurrentState => currentState;

    public override bool HasValidState =>
        hasValidState && GeometryMatchesTrainingEnvelope;

    public bool IsPlaceholder => true;
    public float TrainedInnerRadius => trainedInnerRadius;
    public float TrainedInternalHeight => trainedInternalHeight;
    public float MinimumTrainedFillFraction => minimumTrainedFillFraction;
    public float MaximumTrainedFillFraction => maximumTrainedFillFraction;

    public bool GeometryMatchesTrainingEnvelope
    {
        get
        {
            if (geometry == null)
            {
                return false;
            }

            return
                Mathf.Abs(geometry.InnerRadius - trainedInnerRadius) <= 0.0001f &&
                Mathf.Abs(geometry.InternalHeight - trainedInternalHeight) <= 0.0001f &&
                geometry.FillFraction >= minimumTrainedFillFraction &&
                geometry.FillFraction <= maximumTrainedFillFraction;
        }
    }

    private void Update()
    {
        if (classicalSource == null || !classicalSource.HasValidState)
        {
            hasValidState = false;
            return;
        }

        history.Enqueue(
            new TimedState
            {
                Time = Time.realtimeSinceStartup,
                State = classicalSource.CurrentState
            });

        float targetTime =
            Time.realtimeSinceStartup - simulatedInferenceDelaySeconds;

        LiquidState delayedState = history.Peek().State;

        while (history.Count > 1 && history.Peek().Time <= targetTime)
        {
            delayedState = history.Dequeue().State;
        }

        float blend =
            1.0f - Mathf.Exp(
                -outputSmoothingRate * Mathf.Max(Time.deltaTime, 0.0001f));

        currentState = new LiquidState(
            Vector2.Lerp(
                currentState.SurfaceSlope,
                delayedState.SurfaceSlope,
                blend),
            Vector2.Lerp(
                currentState.SurfaceSlopeVelocity,
                delayedState.SurfaceSlopeVelocity,
                blend));

        hasValidState = true;
    }

    public void ResetEstimator()
    {
        history.Clear();
        currentState = LiquidState.Level;
        hasValidState = false;
    }

    public void ApplyTrainedGeometry()
    {
        if (geometry == null)
        {
            return;
        }

        geometry.SetInnerRadius(trainedInnerRadius);
        geometry.SetInternalHeight(trainedInternalHeight);
        geometry.SetFillFraction(
            Mathf.Clamp(
                geometry.FillFraction,
                minimumTrainedFillFraction,
                maximumTrainedFillFraction));
    }

    private void OnValidate()
    {
        maximumTrainedFillFraction = Mathf.Max(
            minimumTrainedFillFraction,
            maximumTrainedFillFraction);
    }
}
