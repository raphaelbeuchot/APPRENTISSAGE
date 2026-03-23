using UnityEngine;

public class PupitreTuto : MonoBehaviour
{
    [Header("Tutorial Settings")]
    [SerializeField] private Sprite tutorialImage;
    [SerializeField] private string tipID = "Tuto_Default";
    [SerializeField] private bool showOnce = true;
    [SerializeField] private float interactRange = 2f;

    [Header("References")]
    [SerializeField] private TutorialPromptUI promptUI;

    private Transform player;
    private bool hasTriggered = false;

    void Start()
    {
        PlayerPhysicsMovement p = FindObjectOfType<PlayerPhysicsMovement>();
        if (p != null) player = p.transform;
    }

    void Update()
    {
        if (player == null) return;
        if (hasTriggered && showOnce) return;
        if (showOnce && PlayerPrefs.GetInt(tipID, 0) == 1) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > interactRange) return;

        if (PlayerInputManager.Instance != null && PlayerInputManager.Instance.InteractPressed)
        {
            if (promptUI.IsVisible) return;
            ShowTutorial();
        }
    }

    void ShowTutorial()
    {
        if (promptUI == null)
        {
            Debug.LogError("[PupitreTuto] PromptUI non assigne sur " + gameObject.name);
            return;
        }
        if (tutorialImage == null)
        {
            Debug.LogError("[PupitreTuto] TutorialImage non assignee sur " + gameObject.name);
            return;
        }
        GetComponent<InteractBubble>()?.Hide();


        promptUI.Show(tutorialImage);
        hasTriggered = true;

        if (showOnce)
        {
            PlayerPrefs.SetInt(tipID, 1);
            PlayerPrefs.Save();
        }
    }
}