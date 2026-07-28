using TMPro;
using UnityEngine;

/// <summary>
/// Demonstrates three fixed Blender liquid states:
///
/// 1. Still
/// 2. Pre-authored left/right animation
/// 3. Pre-authored forward/back animation
///
/// The controller does not calculate liquid motion from the marker,
/// acceleration, orientation or IMU data.
///
/// It deliberately demonstrates the limitations of selecting from a
/// finite library of prepared animation cases.
/// </summary>
public class PrebakedLiquidDemoController : MonoBehaviour
{
    [Header("Left/right model")]

    [SerializeField]
    private GameObject leftRightModel;

    [SerializeField]
    private Animator leftRightAnimator;

    [SerializeField]
    private string leftRightStateName = "LeftRight";

    [Header("Forward/back model")]

    [SerializeField]
    private GameObject forwardBackModel;

    [SerializeField]
    private Animator forwardBackAnimator;

    [SerializeField]
    private string forwardBackStateName = "ForwardBack";

    [Header("Optional UI")]

    [SerializeField]
    private TMP_Text modelStatusText;

    private int leftRightStateHash;
    private int forwardBackStateHash;

    private void Awake()
    {
        leftRightStateHash = Animator.StringToHash(
            $"Base Layer.{leftRightStateName}"
        );

        forwardBackStateHash = Animator.StringToHash(
            $"Base Layer.{forwardBackStateName}"
        );

        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        ShowStill();
    }

    /// <summary>
    /// Display the first, flat frame of the left/right model.
    /// </summary>
    public void ShowStill()
    {
        leftRightModel.SetActive(true);
        forwardBackModel.SetActive(false);

        leftRightAnimator.speed = 1.0f;

        leftRightAnimator.Play(
            leftRightStateHash,
            0,
            0.0f
        );

        // Evaluate the first frame immediately.
        leftRightAnimator.Update(0.0f);

        // Freeze the animation at that still frame.
        leftRightAnimator.speed = 0.0f;

        SetStatus(
            "Blender animation: Still\n" +
            "No motion input is being used."
        );
    }

    /// <summary>
    /// Start the fixed left/right animation from its first frame.
    /// </summary>
    public void PlayLeftRight()
    {
        leftRightModel.SetActive(true);
        forwardBackModel.SetActive(false);

        leftRightAnimator.speed = 1.0f;

        leftRightAnimator.Play(
            leftRightStateHash,
            0,
            0.0f
        );

        leftRightAnimator.Update(0.0f);

        SetStatus(
            "Blender animation: Left / Right\n" +
            "Playing one fixed pre-authored case."
        );
    }

    /// <summary>
    /// Start the fixed forward/back animation from its first frame.
    /// </summary>
    public void PlayForwardBack()
    {
        leftRightModel.SetActive(false);
        forwardBackModel.SetActive(true);

        forwardBackAnimator.speed = 1.0f;

        forwardBackAnimator.Play(
            forwardBackStateHash,
            0,
            0.0f
        );

        forwardBackAnimator.Update(0.0f);

        SetStatus(
            "Blender animation: Forward / Back\n" +
            "Playing one fixed pre-authored case."
        );
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        if (leftRightModel == null)
        {
            Debug.LogError(
                "Left/right model has not been assigned.",
                this
            );

            valid = false;
        }

        if (leftRightAnimator == null)
        {
            Debug.LogError(
                "Left/right Animator has not been assigned.",
                this
            );

            valid = false;
        }

        if (forwardBackModel == null)
        {
            Debug.LogError(
                "Forward/back model has not been assigned.",
                this
            );

            valid = false;
        }

        if (forwardBackAnimator == null)
        {
            Debug.LogError(
                "Forward/back Animator has not been assigned.",
                this
            );

            valid = false;
        }

        return valid;
    }

    private void SetStatus(string message)
    {
        if (modelStatusText != null)
        {
            modelStatusText.text = message;
        }
    }
}