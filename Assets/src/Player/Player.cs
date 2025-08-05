using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Player : MonoBehaviour
{
    [SerializeField] private PlayerCharacter playerCharacter;
    [SerializeField] private PlayerCamera playerCamera;
    
    private StateDisplay stateDisplay;

    private PlayerInputActions _inputActions;
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        _inputActions = new PlayerInputActions();
        _inputActions.Enable();

        playerCharacter.Initialize();
        playerCamera.Initialize(playerCharacter.GetCameraTarget());

        StartCoroutine(FindStateDisplayWhenReady());
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
            Crouch = input.Crouch.IsPressed(),
            Sprint = input.Sprint.IsPressed()
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
        playerCamera.LeanUpdate(Time.deltaTime, state.Stance is Stance.Slide, playerCharacter.IsSprinting(), state.Acceleration, cameraTarget.up);

        if (stateDisplay != null)
        {
            if (playerCharacter.IsSprinting() && state.Stance is not Stance.Slide && state.Stance is not Stance.Crouch)
            {
                stateDisplay.SetText("Sprinting");
            }
            else if (state.Stance is Stance.Stand)
            {
                stateDisplay.SetText("Walking");
            }
            else
            {
                stateDisplay.SetText(state.Stance.ToString());
            }
        }
        else
        {
           // Debug.Log("no statedisplay");
        }
            
            
    }

    private IEnumerator FindStateDisplayWhenReady()
    {
        while (!UnityEngine.SceneManagement.SceneManager.GetSceneByName("GameUI").isLoaded)
            yield return null;

        stateDisplay = FindFirstObjectByType<StateDisplay>();

        if (stateDisplay == null)
            Debug.LogWarning("StateDisplay still not found after UI scene loaded");
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
