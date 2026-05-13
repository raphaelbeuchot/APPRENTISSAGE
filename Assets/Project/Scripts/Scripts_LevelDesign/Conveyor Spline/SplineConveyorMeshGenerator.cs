using UnityEngine;
using UnityEngine.Splines;

public static class SplineConveyorMeshGenerator
{
    public static Mesh Generate(SplineContainer splineContainer, float width, int sampleCount, int widthSegments, float splineLength)
    {
        Spline spline = splineContainer.Spline;

        // Pre-pass arc-length : table dense pour retrouver t depuis distance reelle
        int prepassCount = sampleCount * 10;
        float[] prepassT = new float[prepassCount];
        float[] prepassDist = new float[prepassCount];
        float cumDist = 0f;
        Vector3 prevPos = (Vector3)spline.EvaluatePosition(0f);
        prepassT[0] = 0f;
        prepassDist[0] = 0f;
        for (int i = 1; i < prepassCount; i++)
        {
            float tt = (float)i / (prepassCount - 1);
            Vector3 pos = (Vector3)spline.EvaluatePosition(tt);
            cumDist += Vector3.Distance(prevPos, pos);
            prevPos = pos;
            prepassT[i] = tt;
            prepassDist[i] = cumDist;
        }
        float totalArcLength = cumDist;

        int vertsPerRing = widthSegments + 1;
        int vertCount = sampleCount * vertsPerRing;
        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] triangles = new int[(sampleCount - 1) * widthSegments * 6];

        for (int i = 0; i < sampleCount; i++)
        {
            float targetDist = (float)i / (sampleCount - 1) * totalArcLength;

            // Retrouver t correspondant a targetDist dans la table
            float t = 0f;
            for (int k = 1; k < prepassCount; k++)
            {
                if (prepassDist[k] >= targetDist)
                {
                    float ratio = (targetDist - prepassDist[k - 1]) / (prepassDist[k] - prepassDist[k - 1]);
                    t = Mathf.Lerp(prepassT[k - 1], prepassT[k], ratio);
                    break;
                }
            }
            if (i == sampleCount - 1) t = 1f;

            Vector3 localPos = (Vector3)spline.EvaluatePosition(t);
            Vector3 localTangent = (Vector3)spline.EvaluateTangent(t);
            Vector3 worldPos = splineContainer.transform.TransformPoint(localPos);
            Vector3 worldTangent = splineContainer.transform.TransformDirection(localTangent).normalized;
            Vector3 worldUp = splineContainer.transform.TransformDirection((Vector3)spline.EvaluateUpVector(t)).normalized;
            Vector3 right = Vector3.Cross(worldTangent, worldUp).normalized;
            Vector3 localCenter = splineContainer.transform.InverseTransformPoint(worldPos);
            Vector3 localRight = splineContainer.transform.InverseTransformDirection(right);

            float vCoord = targetDist / width;

            for (int j = 0; j <= widthSegments; j++)
            {
                float lateralT = (float)j / widthSegments;
                float lateralOffset = (lateralT - 0.5f) * width;
                vertices[i * vertsPerRing + j] = localCenter + localRight * lateralOffset;
                uvs[i * vertsPerRing + j] = new Vector2(lateralT, vCoord);
            }
        }

        int triIndex = 0;
        for (int i = 0; i < sampleCount - 1; i++)
        {
            for (int j = 0; j < widthSegments; j++)
            {
                int bl = i * vertsPerRing + j;
                int br = i * vertsPerRing + j + 1;
                int tl = (i + 1) * vertsPerRing + j;
                int tr = (i + 1) * vertsPerRing + j + 1;

                triangles[triIndex++] = bl;
                triangles[triIndex++] = br;
                triangles[triIndex++] = tl;
                triangles[triIndex++] = br;
                triangles[triIndex++] = tr;
                triangles[triIndex++] = tl;
            }
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