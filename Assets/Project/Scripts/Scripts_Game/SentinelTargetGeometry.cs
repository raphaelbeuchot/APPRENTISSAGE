using UnityEngine;

public static class SentinelTargetGeometry
{
    public static Vector3 GetTargetCenter(Collider col)
        => col.bounds.center;

    public static Vector3 GetTargetCenter(GameObject obj)
    {
        Collider col = obj.GetComponent<Collider>();
        return col != null ? col.bounds.center : obj.transform.position + Vector3.up * 1f;
    }

    public static Vector3 GetHeadPosition(Collider col)
    {
        float headHeight = col.bounds.min.y + (col.bounds.size.y * 0.9f);
        return new Vector3(col.bounds.center.x, headHeight, col.bounds.center.z);
    }

    public static Vector3 GetHeadPosition(GameObject obj)
    {
        Collider col = obj.GetComponent<Collider>();
        return col != null ? GetHeadPosition(col) : obj.transform.position + Vector3.up * 1.8f;
    }
}
