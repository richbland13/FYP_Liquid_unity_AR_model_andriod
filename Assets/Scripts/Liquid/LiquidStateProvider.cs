using UnityEngine;

/// <summary>
/// Common base component for interchangeable liquid-state estimators.
/// </summary>
public abstract class LiquidStateProvider : MonoBehaviour
{
    public abstract LiquidState CurrentState { get; }

    public abstract bool HasValidState { get; }
}