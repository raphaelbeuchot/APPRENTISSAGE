using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Splines;
using System.Collections.Generic;

public static class SplinePitMeshGenerator
{
    public static Mesh GenerateWallMesh(Vector3[] contour, float depth)
    {
        int count = contour.Length;
        Vector3[] vertices = new Vector3[count * 2];
        int[] triangles = new int[count * 12];

        for (int i = 0; i < count; i++)
        {
            vertices[i] = contour[i];
            vertices[i + count] = contour[i] + Vector3.down * depth;
        }

        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            int ti = i * 12;
            triangles[ti + 0] = i;
            triangles[ti + 1] = next;
            triangles[ti + 2] = i + count;
            triangles[ti + 3] = next;
            triangles[ti + 4] = next + count;
            triangles[ti + 5] = i + count;
            triangles[ti + 6] = i;
            triangles[ti + 7] = i + count;
            triangles[ti + 8] = next;
            triangles[ti + 9] = next;
            triangles[ti + 10] = i + count;
            triangles[ti + 11] = next + count;
        }

        Vector3 center = Vector3.zero;
        for (int i = 0; i < count; i++)
            center += contour[i];
        center /= count;

        Vector3[] normals = new Vector3[count * 2];
        for (int i = 0; i < count; i++)
        {
            Vector3 outward = contour[i] - center;
            outward.y = 0f;
            outward.Normalize();
            normals[i] = outward;
            normals[i + count] = outward;
        }

        Mesh mesh = new Mesh();
        mesh.name = "SplinePitWalls";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = normals;
        return mesh;
    }

    public static Mesh GenerateFloorMeshFan(Vector3[] contour, float depth)
    {
        int count = contour.Length;

        Vector3 center = Vector3.zero;
        for (int i = 0; i < count; i++)
            center += contour[i];
        center /= count;
        center += Vector3.down * depth;

        Vector3[] vertices = new Vector3[count + 1];
        for (int i = 0; i < count; i++)
            vertices[i] = contour[i] + Vector3.down * depth;
        vertices[count] = center;

        List<int> tris = new List<int>();
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            tris.Add(count);
            tris.Add(i);
            tris.Add(next);
        }

        Vector3[] normals = new Vector3[vertices.Length];
        for (int i = 0; i < normals.Length; i++)
            normals[i] = Vector3.up;

        Mesh mesh = new Mesh();
        mesh.name = "SplinePitFloor";
        mesh.vertices = vertices;
        mesh.triangles = tris.ToArray();
        mesh.normals = normals;
        return mesh;
    }

    public static Mesh GenerateRimMesh(Vector3[] innerContour, float margin)
    {
        int innerCount = innerContour.Length;
        float y = innerContour[0].y;

        // Normalise en CCW vu d'en haut
        Vector3[] inner = new Vector3[innerCount];
        System.Array.Copy(innerContour, inner, innerCount);
        if (IsClockwiseXZ(inner))
            System.Array.Reverse(inner);

        // Centroide
        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < innerCount; i++) centroid += inner[i];
        centroid /= innerCount;
        centroid.y = y;

        // Rectangle englobant
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < innerCount; i++)
        {
            if (inner[i].x < minX) minX = inner[i].x;
            if (inner[i].x > maxX) maxX = inner[i].x;
            if (inner[i].z < minZ) minZ = inner[i].z;
            if (inner[i].z > maxZ) maxZ = inner[i].z;
        }
        minX -= margin; maxX += margin;
        minZ -= margin; maxZ += margin;

        // Coins du rectangle CCW vu d'en haut : BL, BR, TR, TL
        Vector3[] rectCorners = new Vector3[]
        {
        new Vector3(minX, y, minZ),
        new Vector3(maxX, y, minZ),
        new Vector3(maxX, y, maxZ),
        new Vector3(minX, y, maxZ),
        };

        // Intersection rayon centroide->inner[i] avec le rectangle
        Vector3[] outerPts = new Vector3[innerCount];
        int[] outerSides = new int[innerCount];
        for (int i = 0; i < innerCount; i++)
        {
            outerPts[i] = RayRectIntersect(centroid, inner[i], minX, maxX, minZ, maxZ, y);
            outerSides[i] = GetRectSide(outerPts[i], minX, maxX, minZ, maxZ);
        }

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        for (int i = 0; i < innerCount; i++)
        {
            int next = (i + 1) % innerCount;
            Vector3 iA = inner[i];
            Vector3 iB = inner[next];
            Vector3 oA = outerPts[i];
            Vector3 oB = outerPts[next];
            int sideA = outerSides[i];
            int sideB = outerSides[next];

            // Sequence du fan : oA, [coins eventuels], oB, iB
            // Triangles depuis iA : (iA, seq[j], seq[j+1])
            List<Vector3> fanSeq = new List<Vector3>();
            fanSeq.Add(oA);

            if (sideA != sideB)
            {
                int s = sideA;
                for (int guard = 0; guard < 4; guard++)
                {
                    int ns = (s + 1) % 4;
                    fanSeq.Add(rectCorners[ns]);
                    s = ns;
                    if (s == sideB) break;
                }
            }

            fanSeq.Add(oB);
            fanSeq.Add(iB);

            for (int j = 0; j < fanSeq.Count - 1; j++)
            {
                int baseIdx = verts.Count;
                verts.Add(iA);
                verts.Add(fanSeq[j]);
                verts.Add(fanSeq[j + 1]);
                tris.Add(baseIdx);
                tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 1);
            }
        }

        Vector3[] normals = new Vector3[verts.Count];
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;

        Mesh mesh = new Mesh();
        mesh.name = "SplinePitRim";
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.normals = normals;
        return mesh;
    }

    private static Vector3 RayRectIntersect(Vector3 from, Vector3 through,
        float minX, float maxX, float minZ, float maxZ, float y)
    {
        float dx = through.x - from.x;
        float dz = through.z - from.z;
        float tBest = float.MaxValue;
        Vector3 result = through;

        if (dx > 0.00001f)
        {
            float t = (maxX - from.x) / dx;
            float z = from.z + t * dz;
            if (t > 0f && z >= minZ && z <= maxZ && t < tBest)
            { tBest = t; result = new Vector3(maxX, y, z); }
        }
        if (dx < -0.00001f)
        {
            float t = (minX - from.x) / dx;
            float z = from.z + t * dz;
            if (t > 0f && z >= minZ && z <= maxZ && t < tBest)
            { tBest = t; result = new Vector3(minX, y, z); }
        }
        if (dz > 0.00001f)
        {
            float t = (maxZ - from.z) / dz;
            float x = from.x + t * dx;
            if (t > 0f && x >= minX && x <= maxX && t < tBest)
            { tBest = t; result = new Vector3(x, y, maxZ); }
        }
        if (dz < -0.00001f)
        {
            float t = (minZ - from.z) / dz;
            float x = from.x + t * dx;
            if (t > 0f && x >= minX && x <= maxX && t < tBest)
            { tBest = t; result = new Vector3(x, y, minZ); }
        }

        return result;
    }

    private static int GetRectSide(Vector3 p, float minX, float maxX, float minZ, float maxZ)
    {
        const float eps = 0.001f;
        if (Mathf.Abs(p.z - minZ) < eps) return 0;
        if (Mathf.Abs(p.x - maxX) < eps) return 1;
        if (Mathf.Abs(p.z - maxZ) < eps) return 2;
        if (Mathf.Abs(p.x - minX) < eps) return 3;
        return 0;
    }

    private static bool IsClockwiseXZ(Vector3[] points)
    {
        float sum = 0f;
        for (int i = 0; i < points.Length; i++)
        {
            int next = (i + 1) % points.Length;
            sum += (points[next].x - points[i].x) * (points[next].z + points[i].z);
        }
        return sum > 0f;
    }

    public static Vector3[] SampleSpline(SplineContainer splineContainer, int sampleCount)
    {
        Spline spline = splineContainer.Spline;
        Vector3[] points = new Vector3[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float3 localPos = spline.EvaluatePosition(t);
            Vector3 worldPos = splineContainer.transform.TransformPoint(
                new Vector3(localPos.x, localPos.y, localPos.z));
            worldPos.y = splineContainer.transform.position.y;
            points[i] = worldPos;
        }

        return points;
    }
    public static Mesh GenerateFillMesh(Vector3[] contour, float depth, float fillLevel)
    {
        int count = contour.Length;
        float fillY = contour[0].y - depth + fillLevel;

        Vector3 center = Vector3.zero;
        for (int i = 0; i < count; i++)
            center += contour[i];
        center /= count;
        center.y = fillY;

        Vector3[] vertices = new Vector3[count + 1];
        for (int i = 0; i < count; i++)
            vertices[i] = new Vector3(contour[i].x, fillY, contour[i].z);
        vertices[count] = center;

        int[] tris = new int[count * 3];
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            tris[i * 3 + 0] = count;
            tris[i * 3 + 1] = i;
            tris[i * 3 + 2] = next;
        }

        Vector3[] normals = new Vector3[vertices.Length];
        for (int i = 0; i < normals.Length; i++)
            normals[i] = Vector3.up;

        Mesh mesh = new Mesh();
        mesh.name = "SplinePitFill";
        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.normals = normals;
        return mesh;
    }
}