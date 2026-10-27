using UnityEngine;

// Marque un sol avec son SurfaceType. FootstepController le cherche sur le collider touche puis dans ses parents :
// le poser sur un prefab marque toutes ses instances, le poser sur un parent marque tout ce qu'il contient.
[DisallowMultipleComponent]
public class Surface : MonoBehaviour
{
    [SerializeField] private SurfaceType type;

    public SurfaceType Type => type;
}
