using UnityEngine;

[CreateAssetMenu(fileName = "LevelGenConfig", menuName = "1-2-3 Soleil/Level Generator Config")]
public class LevelGeneratorConfig : ScriptableObject
{
    [System.Serializable]
    public class PrefabEntry
    {
        public string name = "New Prefab";
        public GameObject prefab;
        public string parentFolder = "_Managers";
        public Vector3 position = Vector3.zero;
        public Vector3 rotation = Vector3.zero;
        public Vector3 scale = Vector3.one;
        public bool enabled = true;
    }

    [Header("Prefabs Recurrents")]
    [Tooltip("Ces prefabs seront instancies automatiquement dans chaque niveau")]
    public PrefabEntry[] prefabsToSpawn = new PrefabEntry[0];
}