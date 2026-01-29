using UnityEngine;

public class CountdownTextConfig : MonoBehaviour
{
    [Header("Message")]
    [SerializeField] private string titleMessage = "Super Panopticon!";

    [Header("Animation")]
    [SerializeField] private float rotationDuration = 2f;
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private bool rotateClockwise = true;

    public bool IsClockwise()
    {
        return rotateClockwise;
    }
    public string GetTitle()
    {
        return titleMessage;
    }

    public float GetRotationDuration()
    {
        return rotationDuration;
    }

    public float GetFadeOutDuration()
    {
        return fadeOutDuration;
    }
}