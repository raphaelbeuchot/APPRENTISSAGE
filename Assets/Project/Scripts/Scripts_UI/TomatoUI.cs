using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TomatoUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Image tomatoIcon;

    private TomatoThrowSystem throwSystem;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            throwSystem = playerObj.GetComponent<TomatoThrowSystem>();
            if (throwSystem != null)
                throwSystem.OnTomatoCountChanged += UpdateUI;
        }
        else
        {
            Debug.LogWarning("[TomatoUI] Player introuvable");
        }

        UpdateUI(0, 0);
    }

    private void UpdateUI(int current, int max)
    {
        if (countText != null)
            countText.text = "x" + current.ToString();
    }

    private void OnDestroy()
    {
        if (throwSystem != null)
            throwSystem.OnTomatoCountChanged -= UpdateUI;
    }
}