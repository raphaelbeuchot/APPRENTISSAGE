using UnityEngine;
using TMPro;

public class WheelCell : MonoBehaviour
{
    public ModifierType modifierType;
    private TextMeshProUGUI label;

    void Awake()
    {
        label = GetComponent<TextMeshProUGUI>();
    }

    public string GetLabel()
    {
        if (label != null) return label.text;
        return "";
    }

    public void SetColor(Color color)
    {
        if (label != null) label.color = color;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
    public void SetAlpha(float alpha)
    {
        if (label == null) return;
        Color c = label.color;
        c.a = alpha;
        label.color = c;
    }
}