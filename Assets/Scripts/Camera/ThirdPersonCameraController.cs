using UnityEngine;
using DG.Tweening;

public class ThirdPersonCameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Transform yawPivot;
    [SerializeField] private Transform pitchPivot;
    [SerializeField] private Camera cam;
    public Transform YawPivot => yawPivot;

    [Header("Follow")]
    [SerializeField] private float horizontalFollowSmooth = 20f;
    [SerializeField] private float verticalFollowSmooth = 8f;

    [Header("Rotation")]
    [SerializeField] private float mouseSensitivity = 200f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;

    [Header("Zoom")]
    [SerializeField] private float defaultDistance = 4f;
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 6f;
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float zoomSmooth = 12f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask;
    [SerializeField] private float collisionRadius = 0.2f;
    [SerializeField] private float collisionOffset = 0.1f;

    private float yaw;
    private float pitch;
    private float targetDistance;
    private float currentDistance;

    private Tween shakeTween;

    private void Awake()
    {
        targetDistance = defaultDistance;
        currentDistance = defaultDistance;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Vector3 euler = yawPivot.rotation.eulerAngles;
        yaw = euler.y;
        pitch = pitchPivot.localRotation.eulerAngles.x;

        // Unity의 0~360도 표현을 Clamp에 사용할 수 있는 음수 각도 범위로 되돌린다.
        if (pitch > 180f)
            pitch -= 360f;
    }

    private void Update()
    {
        HandleRotationInput();
        HandleZoomInput();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // 캐릭터 이동이 끝난 뒤 추적 위치와 회전을 정하고, 마지막에 장애물 거리를 보정한다.
        FollowTarget();
        ApplyRotation();
        HandleCameraCollision();
    }

    private void HandleRotationInput()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        yaw += mouseX * mouseSensitivity * Time.deltaTime;
        pitch -= mouseY * mouseSensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void HandleZoomInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.001f)
        {
            targetDistance -= scroll * zoomSpeed;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }
    }

    private void FollowTarget()
    {
        Vector3 current = transform.position;
        Vector3 targetPos = target.position;

        float x = Mathf.Lerp(current.x, targetPos.x, horizontalFollowSmooth * Time.deltaTime);
        float z = Mathf.Lerp(current.z, targetPos.z, horizontalFollowSmooth * Time.deltaTime);
        float y = Mathf.Lerp(current.y, targetPos.y, verticalFollowSmooth * Time.deltaTime);

        transform.position = new Vector3(x, y, z);
    }

    public void SetTarget(Transform newTarget)
    {
        if (newTarget == null) return;

        target = newTarget;
    }

    private void ApplyRotation()
    {
        yawPivot.rotation = Quaternion.Euler(0f, yaw, 0f);
        pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleCameraCollision()
    {
        Vector3 pivotPos = pitchPivot.position;
        Vector3 desiredCameraLocalPos = new Vector3(0f, 0f, -targetDistance);

        Vector3 desiredWorldPos = pitchPivot.TransformPoint(desiredCameraLocalPos);
        Vector3 direction = desiredWorldPos - pivotPos;
        float distance = direction.magnitude;

        direction.Normalize();

        float finalDistance = targetDistance;

        // 피벗에서 희망 카메라 위치까지 구를 쏴 벽에 닿으면 카메라만 앞으로 당긴다.
        if (Physics.SphereCast(
                pivotPos,
                collisionRadius,
                direction,
                out RaycastHit hit,
                distance,
                collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            finalDistance = hit.distance - collisionOffset;
            finalDistance = Mathf.Clamp(finalDistance, minDistance, targetDistance);
        }

        currentDistance = Mathf.Lerp(currentDistance, finalDistance, zoomSmooth * Time.deltaTime);
        cam.transform.localPosition = new Vector3(0f, 0f, -currentDistance);
    }

    public Vector3 GetCameraPlanarForward()
    {
        Vector3 forward = yawPivot.forward;
        forward.y = 0f;
        return forward.normalized;
    }

    public Vector3 GetCameraPlanarRight()
    {
        Vector3 right = yawPivot.right;
        right.y = 0f;
        return right.normalized;
    }

    public void Shake(float duration = 0.12f, float strength = 0.08f, int vibrato = 18)
    {
        if (pitchPivot == null) return;

        // 연속 피격 때 이전 Tween의 잔여 오프셋이 다음 흔들림에 누적되지 않게 초기화한다.
        shakeTween?.Kill();

        pitchPivot.localPosition = Vector3.zero;

        shakeTween = pitchPivot.DOShakePosition(duration, strength, vibrato)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                pitchPivot.localPosition = Vector3.zero;
            });
    }
}
