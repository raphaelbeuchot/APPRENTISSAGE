using UnityEngine;
using System.Collections;

public class TutorialTrigger : MonoBehaviour
{
    public enum InputToWatch
    {
        None,           // Timeout uniquement
        Sprint,
        Climb,
        Crouch,
        CameraToggle
    }

    [Header("Tutorial Settings")]
    [SerializeField] private string tipMessage = "Press X to interact";
    [SerializeField] private string tipID = "Tuto_Default";
    [SerializeField] private bool showOnce = true;

    [Header("Hide Behavior")]
    [SerializeField] private InputToWatch watchForInput = InputToWatch.None;
    [SerializeField] private float autoHideDelay = 5f; // Secondes avant hide auto

    [Header("References")]
    [SerializeField] private TutorialPromptUI promptUI;

    private bool hasTriggered = false;
    private Coroutine hideCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (hasTriggered && showOnce)
            return;

        if (showOnce && PlayerPrefs.GetInt(tipID, 0) == 1)
        {
            Debug.Log("[TutorialTrigger] Tip " + tipID + " deja vu, skip.");
            return;
        }

        ShowTip();
    }

    private void ShowTip()
    {
        if (promptUI == null)
        {
            Debug.LogError("[TutorialTrigger] PromptUI non assigne sur " + gameObject.name);
            return;
        }

        promptUI.SetMessage(tipMessage);
        promptUI.Show();

        hasTriggered = true;

        if (showOnce)
        {
            PlayerPrefs.SetInt(tipID, 1);
            PlayerPrefs.Save();
        }

        // Lancer la coroutine de surveillance
        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(WatchForHideCondition());
    }

    private IEnumerator WatchForHideCondition()
    {
        float elapsed = 0f;

        while (elapsed < autoHideDelay)
        {
            // Check si l'input correspondant a ete presse
            if (CheckInputPressed())
            {
                Debug.Log("[TutorialTrigger] Input detecte, cache le prompt");
                promptUI.Hide();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Timeout atteint, cache automatiquement
        Debug.Log("[TutorialTrigger] Timeout atteint, cache le prompt");
        promptUI.Hide();
    }

    private bool CheckInputPressed()
    {
        switch (watchForInput)
        {
            case InputToWatch.Sprint:
                return PlayerInputManager.Instance.SprintPressed;

            case InputToWatch.Climb:
                return PlayerInputManager.Instance.InteractPressed;

            case InputToWatch.Crouch:
                return PlayerInputManager.Instance.CrouchPressed;

            case InputToWatch.CameraToggle:
                return PlayerInputManager.Instance.ToggleCameraViewPressed;

            case InputToWatch.None:
            default:
                return false; // Timeout uniquement
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // On ne cache plus au OnTriggerExit
        // Le prompt reste affiche jusqu'a input ou timeout
    }
}