using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class MultiTestMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private PlayerInput playerInput;
    private InputAction moveAction;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Movement"];
    }

    private void OnEnable()
    {
        Debug.Log($"[MultiTest] Player {playerInput.playerIndex} joined with device: {playerInput.devices[0].displayName}");

        if (MultiTestCameraRig.Instance != null)
            MultiTestCameraRig.Instance.AssignFollow(playerInput.playerIndex, transform);
    }

    private void Update()
    {
        Vector2 move = moveAction.ReadValue<Vector2>();
        transform.position += new Vector3(move.x, 0f, move.y) * moveSpeed * Time.deltaTime;
    }
}
