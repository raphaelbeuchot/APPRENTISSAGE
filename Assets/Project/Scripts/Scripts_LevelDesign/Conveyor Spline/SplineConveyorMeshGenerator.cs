using UnityEngine;
using UnityEngine.Splines;

public static class SplineConveyorMeshGenerator
{
    public static Mesh Generate(SplineContainer splineContainer, float width, int sampleCount, float splineLength)
    {
        Spline spline = splineContainer.Spline;

        int vertCount = sampleCount * 2;
        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] triangles = new int[(sampleCount - 1) * 6];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / (sampleCount - 1);

            Vector3 localPos = spline.EvaluatePosition(t);
            Vector3 localTangent = spline.EvaluateTangent(t);

            Vector3 worldPos = splineContainer.transform.TransformPoint(localPos);
            Vector3 worldTangent = splineContainer.transform.TransformDirection(localTangent).normalized;

            Vector3 worldUp = splineContainer.transform.TransformDirection(spline.EvaluateUpVector(t)).normalized;
            Vector3 right = Vector3.Cross(worldTangent, worldUp).normalized;

            Vector3 localCenter = splineContainer.transform.InverseTransformPoint(worldPos);
            Vector3 localRight = splineContainer.transform.InverseTransformDirection(right);

            vertices[i * 2] = localCenter - localRight * width * 0.5f;
            vertices[i * 2 + 1] = localCenter + localRight * width * 0.5f;

            float vCoord = t * splineLength / width;
            uvs[i * 2] = new Vector2(0f, vCoord);
            uvs[i * 2 + 1] = new Vector2(1f, vCoord);
        }

        int triIndex = 0;
        for (int i = 0; i < sampleCount - 1; i++)
        {
            int bl = i * 2;
            int br = i * 2 + 1;
            int tl = i * 2 + 2;
            int tr = i * 2 + 3;

            triangles[triIndex++] = bl;
            triangles[triIndex++] = br;
            triangles[triIndex++] = tl;

            triangles[triIndex++] = br;
            triangles[triIndex++] = tr;
            triangles[triIndex++] = tl;
        }

        Mesh mesh = new Mesh();
        mesh.name = "SplineConveyorMesh";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}