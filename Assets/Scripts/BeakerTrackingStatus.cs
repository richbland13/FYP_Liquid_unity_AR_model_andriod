using TMPro;
using UnityEngine;
using Vuforia;

public class BeakerTrackingStatus : MonoBehaviour
{
    [SerializeField]
    private TMP_Text statusText;

    private ObserverBehaviour observer;

    private void Awake()
    {
        observer = GetComponent<ObserverBehaviour>();

        if (observer == null)
        {
            Debug.LogError(
                "BeakerTrackingStatus must be attached to an ImageTarget.");
        }
    }

    private void OnEnable()
    {
        if (observer == null)
        {
            return;
        }

        observer.OnTargetStatusChanged += HandleTargetStatusChanged;
        UpdateStatusText(observer.TargetStatus);
    }

    private void OnDisable()
    {
        if (observer != null)
        {
            observer.OnTargetStatusChanged -= HandleTargetStatusChanged;
        }
    }

    private void HandleTargetStatusChanged(
        ObserverBehaviour behaviour,
        TargetStatus targetStatus)
    {
        UpdateStatusText(targetStatus);
    }

    private void UpdateStatusText(TargetStatus targetStatus)
    {
        if (statusText == null)
        {
            return;
        }

        switch (targetStatus.Status)
        {
            case Status.TRACKED:
                statusText.text = "Beaker marker visible";
                statusText.color = Color.green;
                break;

            case Status.EXTENDED_TRACKED:
                statusText.text =
                    "Marker out of view - beaker pose estimated";
                statusText.color = Color.yellow;
                break;

            case Status.LIMITED:
                statusText.text =
                    "Tracking limited - show the marker clearly";
                statusText.color = Color.yellow;
                break;

            case Status.NO_POSE:
            default:
                statusText.text =
                    "Point the camera at the beaker marker";
                statusText.color = Color.red;
                break;
        }
    }
}