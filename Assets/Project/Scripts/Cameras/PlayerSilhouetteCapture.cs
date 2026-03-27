using UnityEngine;
using UnityEngine.UI;

public class PlayerSilhouetteCapture : MonoBehaviour
{
    [SerializeField] private Camera silhouetteCamera;
    [SerializeField] private RawImage displayImage;

    public void Capture()
    {
        silhouetteCamera.enabled = true;
        silhouetteCamera.Render();
        displayImage.rectTransform.sizeDelta = new Vector2(
            silhouetteCamera.targetTexture.width * 4f,
            silhouetteCamera.targetTexture.height * 4f
        );
        displayImage.texture = silhouetteCamera.targetTexture;
        
        displayImage.gameObject.SetActive(true);

        silhouetteCamera.enabled = false;
    }
}