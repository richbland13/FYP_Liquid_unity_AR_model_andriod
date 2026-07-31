using UnityEngine;

/// <summary>
/// Selects the liquid-state estimator used by the renderer,
/// spill-risk calculator and status display.
///
/// This version does not silently fall back to the classical
/// estimator when Neural Network mode is selected.
/// </summary>
[DefaultExecutionOrder(150)]
public sealed class LiquidEstimatorRouter : LiquidStateProvider
{
    public enum EstimatorMode
    {
        Classical,
        NeuralNetwork
    }

    [Header("Mode")]
    [SerializeField]
    private EstimatorMode estimatorMode = EstimatorMode.Classical;

    [Header("Providers")]
    [SerializeField]
    private LiquidStateProvider classicalEstimator;

    [SerializeField]
    private LiquidStateProvider neuralEstimator;

    public EstimatorMode Mode => estimatorMode;
    public LiquidStateProvider ClassicalEstimator => classicalEstimator;
    public LiquidStateProvider NeuralEstimator => neuralEstimator;
    public bool NeuralEstimatorAvailable => neuralEstimator != null;

    public LiquidStateProvider ActiveProvider
    {
        get
        {
            return estimatorMode == EstimatorMode.NeuralNetwork
                ? neuralEstimator
                : classicalEstimator;
        }
    }

    public override LiquidState CurrentState
    {
        get
        {
            LiquidStateProvider active = ActiveProvider;

            return active != null && active.HasValidState
                ? active.CurrentState
                : LiquidState.Level;
        }
    }

    public override bool HasValidState
    {
        get
        {
            LiquidStateProvider active = ActiveProvider;
            return active != null && active.HasValidState;
        }
    }

    public void UseClassicalEstimator()
    {
        estimatorMode = EstimatorMode.Classical;
    }

    public bool TryUseNeuralEstimator()
    {
        if (neuralEstimator == null)
        {
            Debug.LogWarning(
                "Neural estimator mode was requested, but no neural estimator is assigned.",
                this);
            return false;
        }

        estimatorMode = EstimatorMode.NeuralNetwork;
        return true;
    }
}
