using UnityEngine;

public struct CameraInput
{
    public Vector2 Look;
}

public class PlayerCamera : MonoBehaviour
{
    [Header("Look Settings")]
    [SerializeField] private float sensitivity = 0.1f;

    [Header("Spring Settings")]
    [SerializeField] private Transform targetTransform;
    [Min(0.01f)]
    [SerializeField] private float halfLife = 0.075f;
    [SerializeField] private float frequency = 18f;
    [SerializeField] private float angularDisplacement = 2f;
    [SerializeField] private float linearDisplacement = 0.05f;

    [Header("Lean Settings")]
    [SerializeField] private Transform leanTransform;
    [SerializeField] private float attackDamping = 0.5f;
    [SerializeField] private float decayDamping = 0.3f;
    [SerializeField] private float leanWalkStrength = 0.175f;
    [SerializeField] private float leanSlideStrength = 0.375f;
    [SerializeField] private float leanSprintStrength = 0.775f;
    [SerializeField] private float leanStrengthResponse = 5f;
    private Vector3 _dampedAcceleration;
    private Vector3 _dampedAccelVelocity;

    private Vector3 _eulerAngles;
    private Vector3 _springPosition;
    private Vector3 _springVelocity;

    private float _leanStrength;

    public void Initialize(Transform target)
    {
        transform.position = target.position;
        transform.rotation = target.rotation;

        transform.eulerAngles = _eulerAngles = target.eulerAngles;
        _springPosition = transform.position;
        _springVelocity = Vector3.zero;

        _leanStrength = leanWalkStrength;
    }

    public void UpdateRotation(CameraInput input)
    {
        _eulerAngles += new Vector3(-input.Look.y, input.Look.x) * sensitivity;
        _eulerAngles.x = Mathf.Clamp(_eulerAngles.x, -89f, 89f);
        transform.eulerAngles = _eulerAngles;
    }

    public void UpdatePosition(Transform target)
    {
        transform.position = target.position;
    }

    public void SpringUpdate(float deltaTime, Vector3 up)
    {
        targetTransform.localPosition = Vector3.zero;
        Spring.Step(ref _springPosition, ref _springVelocity, targetTransform.position, halfLife, frequency, deltaTime);

        var localSpringPosition = _springPosition - targetTransform.position;
        var springHeight = Vector3.Dot(localSpringPosition, up);

        targetTransform.localEulerAngles = new Vector3(-springHeight * angularDisplacement, 0f, 0f);
        targetTransform.localPosition = localSpringPosition * linearDisplacement;
    }

    public void LeanUpdate(float deltaTime, bool isSliding, bool isSprinting, Vector3 acceleration, Vector3 up)
    {

        var planeAcceleration = Vector3.ProjectOnPlane(acceleration, up);
        var damping = planeAcceleration.magnitude > _dampedAcceleration.magnitude ? attackDamping : decayDamping;

        _dampedAcceleration = Vector3.SmoothDamp(
            current: _dampedAcceleration,
            target: planeAcceleration,
            currentVelocity: ref _dampedAccelVelocity,
            smoothTime: damping,
            maxSpeed: float.PositiveInfinity,
            deltaTime: deltaTime
        );

        var leanAxis = Vector3.Cross(_dampedAcceleration.normalized, up).normalized;
        leanTransform.localRotation = Quaternion.identity;

        var leanStrength = isSliding ? leanSlideStrength : leanWalkStrength;

        if (leanStrength == leanWalkStrength && isSprinting)
        {
            leanStrength = leanSprintStrength;
        }

        _leanStrength = Mathf.Lerp(_leanStrength, leanStrength, 1f - Mathf.Exp(-leanStrengthResponse * deltaTime));
        leanTransform.rotation = Quaternion.AngleAxis(-_dampedAcceleration.magnitude * _leanStrength, leanAxis) * leanTransform.rotation;
    }
}
