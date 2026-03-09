using UnityEngine;
using UnityEngine.SceneManagement;

public class DevManager : MonoBehaviour
{
    [Header("Dev Options")]
    public bool startFromZero = false;
    public bool devMode = false;

    void Awake()
    {
        

        if (startFromZero)
        {
            if (LevelProgressionManager.Instance != null)
                LevelProgressionManager.Instance.ResetProgression();
            PlayerPrefs.DeleteAll();
            Debug.Log("[DevManager] Progression resetee.");
        }
    }
}