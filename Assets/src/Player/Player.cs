using UnityEngine;
using UnityEngine.InputSystem;


public class Player : MonoBehaviour
{
    [SerializeField] private PlayerCharacter playerCharacter;
    [SerializeField] private PlayerCamera playerCamera;
    [Space]

    private PlayerInputActions _inputActions;
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        _inputActions = new PlayerInputActions();
        _inputActions.Enable();

        playerCharacter.Initialize();
        playerCamera.Initialize(playerCharacter.GetCameraTarget());
    }

    void Update()
    {
        var input = _inputActions.Movement;
        var deltaTime = Time.deltaTime;

        var cameraInput = new CameraInput { Look = input.Look.ReadValue<Vector2>() };
        playerCamera.UpdateRotation(cameraInput);

        var characterInput = new CharacterInput
        {
            Rotation = playerCamera.transform.rotation,
            Move = input.Move.ReadValue<Vector2>(),
            Jump = input.Jump.WasPressedThisFrame(),
            JumpSustain = input.Jump.IsPressed(),
            Crouch = input.Crouch.WasPressedThisFrame() ? CrouchInput.Toggle : CrouchInput.None
        };

        playerCharacter.UpdateInput(characterInput);
        playerCharacter.UpdateBody(deltaTime);

        #if UNITY_EDITOR
            if (Keyboard.current.fKey.wasPressedThisFrame) {
                var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
                if (Physics.Raycast(ray, out var hit)) {
                    Teleport(hit.point);
                }
            }
        #endif
    }

    void LateUpdate()
    {
        var cameraTarget = playerCharacter.GetCameraTarget();
        var state = playerCharacter.GetState();
        playerCamera.UpdatePosition(cameraTarget);
        playerCamera.SpringUpdate(Time.deltaTime, cameraTarget.up);
        playerCamera.LeanUpdate(Time.deltaTime, state.Acceleration, cameraTarget.up);
    }

    public void Teleport(Vector3 position)
    {
        playerCharacter.SetPosition(position);
    }

    void OnDestroy()
    {
        _inputActions.Dispose();
    }
}
