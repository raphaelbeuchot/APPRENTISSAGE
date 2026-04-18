using UnityEngine;
using TMPro;

public class TextFadeByDistance : MonoBehaviour
{
    [SerializeField] private float distance = 8f;
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private bool startVisible = true;

    private TextMeshPro tmp;
    private Transform player;
    private float currentAlpha;
    private bool playerEnteredRange = false;

    void Start()
    {
        tmp = GetComponent<TextMeshPro>();
        currentAlpha = startVisible ? 1f : 0f;
        tmp.alpha = currentAlpha;

        GameObject playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
            player = playerGO.transform;
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (startVisible && !playerEnteredRange)
        {
            if (dist <= distance)
                playerEnteredRange = true;
            return;
        }

        float targetAlpha = dist <= distance ? 1f : 0f;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime / fadeDuration);
        tmp.alpha = currentAlpha;
    }
}