using UnityEngine;
using KinematicCharacterController;

public enum CrouchInput
{
    None, Toggle
}

public enum Stance
{
    Stand, Crouch, Slide
}

public struct CharacterState
{
    public bool Grounded;
    public Stance Stance;
    public Vector3 Velocity;
    public Vector3 Acceleration;
}

public struct CharacterInput
{
    public Quaternion Rotation;
    public Vector2 Move;
    public bool Jump;
    public bool JumpSustain;
    public CrouchInput Crouch;
}

public class PlayerCharacter : MonoBehaviour, ICharacterController
{
    [SerializeField] private KinematicCharacterMotor motor;
    [SerializeField] private Transform root;
    [SerializeField] private Transform cameraTarget;
    [Space]
    [SerializeField] private float walkSpeed = 13f;
    [SerializeField] private float crouchSpeed = 3f;
    [SerializeField] private float walkResponse = 10f;
    [SerializeField] private float crouchResponse = 15f;
    [Space]
    [SerializeField] private float airSpeed = 7f;
    [SerializeField] private float airAccel = 20f;
    [Space]
    [SerializeField] private float jumpSpeed = 15f;
    [SerializeField] private float coyoteTime = 0.2f;
    [Range(0f, 1f)]
    [SerializeField] private float jumpSustainGravity = 0.6f;
    [SerializeField] private float gravity = -90f;
    [Space]
    [SerializeField] private float slideStartSpeed = 15f;
    [SerializeField] private float slideEndSpeed = 2.5f;
    [SerializeField] private float slideFriction = 0.8f;
    [SerializeField] private float slideSteerAccel = 5f;
    [SerializeField] private float slideGravity = -90f;
    [Space]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchHeightResponse = 15f;
    [Range(0f, 1f)]
    [SerializeField] private float standCameraTargetHeight = 0.9f;
    [Range(0f, 1f)]
    [SerializeField] private float crouchCameraTargetHeight = 0.7f;

    private CharacterState _state;
    private CharacterState _lastState;
    private CharacterState _tempState;

    private Quaternion _requestedRotation;
    private Vector3 _requestedMovement;
    private bool _requestedJump;
    private bool _requestedSustainedJump;
    private bool _requestedCrouch;
    private bool _requestedAirCrouch;

    private float _timeSinceUngrounded;
    private float _timeSinceJumpRequest;
    private bool _ungroundedJump;

    private Collider[] _uncrouchOverResults;

    public void Initialize()
    {
        _state.Stance = Stance.Stand;
        _lastState = _state;
        _uncrouchOverResults = new Collider[8];

        motor.CharacterController = this;
    }

    public void UpdateInput(CharacterInput input)
    {
        _requestedRotation = input.Rotation;

        _requestedMovement = new Vector3(input.Move.x, 0f, input.Move.y);
        _requestedMovement = Vector3.ClampMagnitude(_requestedMovement, 1f);
        _requestedMovement = input.Rotation * _requestedMovement;

        var wasRequestingJump = _requestedJump;
        _requestedJump = _requestedJump || input.Jump;
        if (_requestedJump && !wasRequestingJump) _timeSinceJumpRequest = 0f;
        _requestedSustainedJump = input.JumpSustain;

        var wasRequestingCrouch = _requestedCrouch;
        _requestedCrouch = input.Crouch switch
        {
            CrouchInput.Toggle => !_requestedCrouch,
            CrouchInput.None => _requestedCrouch,
            _ => _requestedCrouch
        };

        if (_requestedCrouch && !wasRequestingCrouch) _requestedAirCrouch = !_state.Grounded;
        else if (!_requestedCrouch && wasRequestingCrouch) _requestedAirCrouch = false;
    }

    public void UpdateBody(float deltaTime)
    {
        var currentHeight = motor.Capsule.height;
        var normalizedHeight = currentHeight / standHeight;
        var cameraTargetHeight = currentHeight * (
            _state.Stance is Stance.Stand ? standCameraTargetHeight : crouchCameraTargetHeight
        );

        var rootTargetScale = new Vector3(1f, normalizedHeight, 1f);
        var cameraTargetOffset = new Vector3(0f, cameraTargetHeight, 0f);
        cameraTarget.localPosition = Vector3.Lerp(
            a: cameraTarget.localPosition,
            b: cameraTargetOffset,
            t: 1f - Mathf.Exp(-crouchHeightResponse * deltaTime)
        );

        root.localScale = Vector3.Lerp(
            a: root.localScale,
            b: rootTargetScale,
            t: 1f - Mathf.Exp(-crouchHeightResponse * deltaTime)
        ); ;
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        var forward = Vector3.ProjectOnPlane
        (
            _requestedRotation * Vector3.forward,
            motor.CharacterUp
        );

        if (forward != Vector3.zero)
            currentRotation = Quaternion.LookRotation(forward, motor.CharacterUp);
    }
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        _state.Acceleration = Vector3.zero;
        if (motor.GroundingStatus.IsStableOnGround)
        {
            _timeSinceUngrounded = 0f;
            _ungroundedJump = false;

            var groundedMovement = motor.GetDirectionTangentToSurface(
                direction: _requestedMovement,
                surfaceNormal: motor.GroundingStatus.GroundNormal
            ) * _requestedMovement.magnitude;
            // slide

            var moving = groundedMovement.sqrMagnitude > 0f;
            var crouching = _state.Stance is Stance.Crouch;
            var wasStanding = _lastState.Stance is Stance.Stand;
            var wasAir = !_lastState.Grounded;
            if (moving && crouching && (wasStanding || wasAir))
            {
                _state.Stance = Stance.Slide;

                if (wasAir)
                {
                    currentVelocity = Vector3.ProjectOnPlane(
                        vector: _lastState.Velocity,
                        planeNormal: motor.GroundingStatus.GroundNormal
                    );
                }

                var effectiveSlideStartSpeed = slideStartSpeed;

                if (!_lastState.Grounded && !_requestedAirCrouch)
                {
                    effectiveSlideStartSpeed = 0f;
                    _requestedAirCrouch = false;
                }

                var slideSpeed = Mathf.Max(effectiveSlideStartSpeed, currentVelocity.magnitude);
                currentVelocity = motor.GetDirectionTangentToSurface(
                    direction: currentVelocity,
                    surfaceNormal: motor.GroundingStatus.GroundNormal
                ) * slideSpeed;
            }

            if (_state.Stance is Stance.Stand or Stance.Crouch)
            {
                var speed = _state.Stance is Stance.Stand ? walkSpeed : crouchSpeed;
                var response = _state.Stance is Stance.Stand ? walkResponse : crouchResponse;
                var velocity = groundedMovement * speed;

                var moveVelocity = Vector3.Lerp(
                    a: currentVelocity,
                    b: velocity,
                    t: 1f - Mathf.Exp(-response * deltaTime)
                );

                _state.Acceleration = (moveVelocity - currentVelocity) / deltaTime;
                currentVelocity = moveVelocity;
            }
            else
            {
                currentVelocity -= currentVelocity * (slideFriction * deltaTime);

                var force = Vector3.ProjectOnPlane(
                    vector: -motor.CharacterUp,
                    planeNormal: motor.GroundingStatus.GroundNormal
                ) * slideGravity;

                currentVelocity -= force * deltaTime;

                var speed = currentVelocity.magnitude;
                var targetVelocity = groundedMovement * speed;
                var steerVelocity = currentVelocity;
                var steerForce = (targetVelocity - steerVelocity) * slideSteerAccel * deltaTime;
                steerVelocity += steerForce;
                steerVelocity = Vector3.ClampMagnitude(steerVelocity, speed);

                _state.Acceleration = (steerVelocity - currentVelocity) / deltaTime;
                currentVelocity = steerVelocity;

                if (currentVelocity.magnitude < slideEndSpeed)
                    _state.Stance = Stance.Crouch;
            }
        }

        else
        {
            _timeSinceUngrounded += deltaTime;
            if (_requestedMovement.sqrMagnitude > 0f)
            {
                var planeVelocity = Vector3.ProjectOnPlane(
                    vector: currentVelocity,
                    planeNormal: motor.CharacterUp
                );

                var planeMovement = Vector3.ProjectOnPlane(
                    vector: _requestedMovement,
                    planeNormal: motor.CharacterUp
                ) * _requestedMovement.magnitude;

                var movementForce = planeMovement * airAccel * deltaTime;

                if (planeVelocity.magnitude < airSpeed)
                {
                    var targetPlaneVelocity = planeVelocity + movementForce;
                    targetPlaneVelocity = Vector3.ClampMagnitude(targetPlaneVelocity, airSpeed);
                    movementForce = targetPlaneVelocity - planeVelocity;
                }
                else if (Vector3.Dot(planeVelocity, movementForce) > 0f)
                {
                    var fixedMovementForce = Vector3.ProjectOnPlane(
                        vector: movementForce,
                        planeNormal: planeVelocity.normalized
                    );

                    movementForce = fixedMovementForce;
                }

                if (motor.GroundingStatus.FoundAnyGround)
                {
                    if (Vector3.Dot(movementForce, currentVelocity + movementForce) > 0f)
                    {
                        var normal = Vector3.Cross(
                            motor.CharacterUp,
                            Vector3.Cross(
                                motor.CharacterUp,
                                motor.GroundingStatus.GroundNormal
                            )
                        ).normalized;

                        movementForce = Vector3.ProjectOnPlane(
                            movementForce,
                            normal
                        );
                    }
                }

                currentVelocity += movementForce;
            }

            var grav = gravity;
            var verticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
            if (_requestedSustainedJump && verticalSpeed > 0f) grav *= jumpSustainGravity;

            currentVelocity += motor.CharacterUp * grav * deltaTime;
        }

        if (_requestedJump)
        {
            var isGrounded = motor.GroundingStatus.IsStableOnGround;
            var canCoyoteJump = _timeSinceUngrounded < coyoteTime && !_ungroundedJump;

            if (isGrounded || canCoyoteJump)
            {
                _requestedJump = false;
                _requestedCrouch = false;
                _requestedAirCrouch = false;

                motor.ForceUnground(time: 0f);
                _ungroundedJump = true;

                var currentVerticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
                var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);
                currentVelocity += motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);
            }
            else
            {
                _timeSinceJumpRequest += deltaTime;
                _requestedJump = _timeSinceJumpRequest < coyoteTime;
            }
        }
    }
    public void BeforeCharacterUpdate(float deltaTime)
    {
        _tempState = _state;
        if (_requestedCrouch && _state.Stance is Stance.Stand)
        {
            _state.Stance = Stance.Crouch;
            motor.SetCapsuleDimensions(
                radius: motor.Capsule.radius,
                height: crouchHeight,
                yOffset: crouchHeight * 0.5f
            );


        }
    }
    public void AfterCharacterUpdate(float deltaTime)
    {
        if (!_requestedCrouch && _state.Stance is not Stance.Stand)
        {

            motor.SetCapsuleDimensions(
                radius: motor.Capsule.radius,
                height: standHeight,
                yOffset: standHeight * 0.5f
            );


            var pos = motor.TransientPosition;
            var rot = motor.TransientRotation;
            var mask = motor.CollidableLayers;
            if (motor.CharacterOverlap
            (
                pos,
                rot,
                _uncrouchOverResults,
                mask,
                QueryTriggerInteraction.Ignore
                ) > 0
            )
            {
                _requestedCrouch = true;
                motor.SetCapsuleDimensions(
                    radius: motor.Capsule.radius,
                    height: crouchHeight,
                    yOffset: crouchHeight * 0.5f
                );
            }
            else
            {
                _state.Stance = Stance.Stand; // error line
            }
        }

        _state.Grounded = motor.GroundingStatus.IsStableOnGround;
        _state.Velocity = motor.Velocity;
        _lastState = _tempState;
    }

    public void SetPosition(Vector3 position, bool killVelocity = true)
    {
        motor.SetPosition(position);
        if (killVelocity) motor.BaseVelocity = Vector3.zero;
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        if (!motor.GroundingStatus.IsStableOnGround && _state.Stance is Stance.Slide)
        {
            _state.Stance = Stance.Crouch;
        }
    }
    public bool IsColliderValidForCollisions(Collider coll)
    {
        return true;
    }
    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {

    }
    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {

    }
    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {

    }

    public void OnDiscreteCollisionDetected(Collider collider)
    {

    }

    public Transform GetCameraTarget() => cameraTarget;

    public CharacterState GetState() => _state;
    public CharacterState GetLastState() => _lastState;
}
