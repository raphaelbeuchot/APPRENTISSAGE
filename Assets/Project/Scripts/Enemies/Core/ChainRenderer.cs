using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class ChainRenderer : MonoBehaviour
{
    [Header("References")]
    public Transform anchor;
    public Transform zombieNeck;
    public ChainConstraint chainConstraint;

    [Header("Visual")]
    public int pointCount = 8;
    public float maxSag = 1.2f;
    public float groundY = 0f;

    private LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = pointCount;
        lr.useWorldSpace = true;
    }

    void LateUpdate()
    {
        if (anchor == null || zombieNeck == null || chainConstraint == null) return;

        float dist = chainConstraint.GetCurrentDistance();
        float chainLen = chainConstraint.GetChainLength();

        float tension = Mathf.Clamp01(dist / chainLen);
        float sag = (1f - tension) * maxSag;

        Vector3 start = anchor.position;
        Vector3 end = zombieNeck.position;

        for (int i = 0; i < pointCount; i++)
        {
            float t = (float)i / (pointCount - 1);

            Vector3 point = Vector3.Lerp(start, end, t);

            // Parabole vers le bas
            float parabola = 4f * t * (1f - t);
            point.y -= parabola * sag;

            // Clamp au sol
            point.y = Mathf.Max(point.y, groundY + 0.05f);

            lr.SetPosition(i, point);
        }
    }
}