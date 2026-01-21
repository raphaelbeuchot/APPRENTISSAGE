using UnityEngine;
using UnityEditor;

public class CombineMeshes : EditorWindow
{
    [MenuItem("Tools/Combine Meshes")]
    static void CombineSelectedMeshes()
    {
        GameObject parent = Selection.activeGameObject;

        if (parent == null)
        {
            Debug.LogError("Selectionne le parent Floor d'abord !");
            return;
        }

        MeshFilter[] meshFilters = parent.GetComponentsInChildren<MeshFilter>();
        CombineInstance[] combine = new CombineInstance[meshFilters.Length];

        int i = 0;
        while (i < meshFilters.Length)
        {
            combine[i].mesh = meshFilters[i].sharedMesh;
            combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
            i++;
        }

        GameObject combined = new GameObject("FloorCombined");
        MeshFilter mf = combined.AddComponent<MeshFilter>();
        MeshRenderer mr = combined.AddComponent<MeshRenderer>();

        mf.mesh = new Mesh();
        mf.mesh.CombineMeshes(combine);

        mf.mesh.Optimize(); // AJOUTE ICI
        mf.mesh.RecalculateNormals(); // AJOUTE ICI

        mr.sharedMaterial = meshFilters[0].GetComponent<MeshRenderer>().sharedMaterial;
        // AJOUTE ICI
        MeshCollider mc = combined.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.mesh;
        mc.convex = false; // Convex = false pour terrain irregulier

        Debug.Log("Meshes combines avec MeshCollider : " + meshFilters.Length + " dalles !");
        Debug.Log("Meshes combines : " + meshFilters.Length + " dalles en 1 mesh !");
    }
}