using UnityEngine;

public class TutorialTrigger : MonoBehaviour
{
    [Header("Tutorial Settings")]
    [SerializeField] private Sprite tutorialImage;
    [SerializeField] private string tipID = "Tuto_Default";
    [SerializeField] private bool showOnce = true;

    [Header("References")]
    [SerializeField] private TutorialPromptUI promptUI;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (hasTriggered && showOnce)
            return;

        if (showOnce && PlayerPrefs.GetInt(tipID, 0) == 1)
        {
            Debug.Log("[TutorialTrigger] Tip " + tipID + " deja vu, skip");
            return;
        }

        ShowTutorial();
    }

    private void ShowTutorial()
    {
        if (promptUI == null)
        {
            Debug.LogError("[TutorialTrigger] PromptUI non assigne sur " + gameObject.name);
            return;
        }

        if (tutorialImage == null)
        {
            Debug.LogError("[TutorialTrigger] TutorialImage non assignee sur " + gameObject.name);
            return;
        }

        Debug.Log("[TutorialTrigger] Affichage tutorial: " + tipID);

        promptUI.Show(tutorialImage);

        hasTriggered = true;

        if (showOnce)
        {
            PlayerPrefs.SetInt(tipID, 1);
            PlayerPrefs.Save();
        }
    }
}