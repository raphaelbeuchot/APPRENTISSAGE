using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Multi : affiche le score MultiMatchScore en permanence, un chiffre par moitie d'ecran.
// A poser sur un GameObject vide dans la scene, comme le reste du HUD multi (recree a chaque
// rechargement de scene, relit juste les compteurs statiques courants).
// Rafraichi en continu (Update) plutot que sur un evenement precis, faute de savoir encore a
// quel moment exact declencher visuellement l'incrementation (cf. discussion) : le cout est
// negligeable (deux entiers) et le score ne change de toute facon qu'en toute fin de course.
public class MultiScoreDisplay : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset scoreFont;
    [SerializeField] private float fontSize = 60f;
    [SerializeField] private Color textColor = Color.white;

    private TextMeshProUGUI leftText;
    private TextMeshProUGUI rightText;

    private void Start()
    {
        GameObject canvasObj = new GameObject("ScoreCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        leftText = CreateScoreText(canvasObj.transform, false);
        rightText = CreateScoreText(canvasObj.transform, true);

        RefreshScores();
    }

    private void Update()
    {
        RefreshScores();
    }

    private void RefreshScores()
    {
        leftText.text = MultiMatchScore.GetScore(PlayerScreenSide.Side.Left).ToString();
        rightText.text = MultiMatchScore.GetScore(PlayerScreenSide.Side.Right).ToString();
    }

    private TextMeshProUGUI CreateScoreText(Transform parent, bool isRight)
    {
        GameObject go = new GameObject(isRight ? "ScoreRight" : "ScoreLeft");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = isRight ? new Vector2(0.5f, 1f) : new Vector2(0f, 1f);
        rect.anchorMax = isRight ? new Vector2(1f, 1f) : new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 100f);
        rect.anchoredPosition = new Vector2(0f, -20f);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (scoreFont != null) text.font = scoreFont;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = textColor;
        text.raycastTarget = false;
        return text;
    }
}
