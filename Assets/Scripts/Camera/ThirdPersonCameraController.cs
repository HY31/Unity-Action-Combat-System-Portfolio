using UnityEngine;
using DG.Tweening;

public class ThirdPersonCameraController : MonoBehaviour
{
    public static ThirdPersonCameraController Active { get; private set; }
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

    [Header("Parry Presentation")]
    [SerializeField] private Vector3 parryCameraOffset = new Vector3(0.42f, -0.32f, 0.15f);
    [SerializeField] private float parryDistanceOffset = -1.45f;
    [SerializeField] private float parryPitch = -2f;
    [SerializeField] private float parryYawOffset = -5f;
    [SerializeField, Min(1f)] private float parryRightYawMultiplier = 1.6f;
    [SerializeField] private float parryEnterDuration = 0.16f;
    [SerializeField] private float parryHoldDuration = 0.65f;
    [SerializeField] private float parryReturnDuration = 0.7f;
    [SerializeField, Range(0f, 1f)] private float parryAimStrength = 0.95f;
    [SerializeField] private float parryFieldOfViewDelta = -8f;
    [SerializeField] private float parryImpactFieldOfViewDelta = -3f;
    [SerializeField] private float parryImpactZoomInDuration = 0.035f;
    [SerializeField] private float parryImpactZoomOutDuration = 0.2f;

    private float yaw;
    private float pitch;
    private float targetDistance;
    private float currentDistance;

    private Tween shakeTween;
    private Tween impactRotationTween;
    private Tween fovTween;
    private Tween parryCameraTween;
    private float baseFieldOfView;

    private Vector3 presentationCameraOffset;
    private float presentationDistanceOffset;
    private float presentationAimWeight;
    private float presentationTargetYaw;
    private float presentationTargetPitch;
    private float parryCameraSide = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Active = null;
    }

    private void Awake()
    {
        Active = this;
        targetDistance = defaultDistance;
        currentDistance = defaultDistance;
        baseFieldOfView = cam != null ? cam.fieldOfView : 60f;

        // 마스크를 비워 둔 씬에서도 최소한 기본 환경 지형과는 충돌하게 한다.
        if (collisionMask.value == 0)
            collisionMask = 1 << 0;
    }

    private void OnDestroy()
    {
        if (Active == this)
            Active = null;

        shakeTween?.Kill();
        impactRotationTween?.Kill();
        fovTween?.Kill();
        parryCameraTween?.Kill();
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
        float resolvedYaw = Mathf.LerpAngle(yaw, presentationTargetYaw, presentationAimWeight);
        float resolvedPitch = Mathf.Lerp(pitch, presentationTargetPitch, presentationAimWeight);

        yawPivot.rotation = Quaternion.Euler(0f, resolvedYaw, 0f);
        pitchPivot.localRotation = Quaternion.Euler(resolvedPitch, 0f, 0f);
    }

    private void HandleCameraCollision()
    {
        Vector3 pivotPos = pitchPivot.position;
        float desiredDistance = Mathf.Max(
            collisionRadius + collisionOffset,
            targetDistance + presentationDistanceOffset);
        Vector3 desiredCameraLocalPos = new Vector3(
            presentationCameraOffset.x,
            presentationCameraOffset.y,
            -desiredDistance + presentationCameraOffset.z);

        Vector3 desiredWorldPos = pitchPivot.TransformPoint(desiredCameraLocalPos);
        Vector3 direction = desiredWorldPos - pivotPos;
        float distance = direction.magnitude;
        Vector3 finalCameraLocalPos = desiredCameraLocalPos;

        // 패링 숄더 오프셋까지 포함한 실제 경로를 검사해 바닥이나 벽 앞에서 멈춘다.
        if (distance > 0.0001f && Physics.SphereCast(
                pivotPos,
                collisionRadius,
                direction / distance,
                out RaycastHit hit,
                distance,
                collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            float safeDistance = Mathf.Max(
                collisionRadius,
                hit.distance - collisionOffset);
            Vector3 safeWorldPosition = pivotPos + direction.normalized * safeDistance;
            finalCameraLocalPos = pitchPivot.InverseTransformPoint(safeWorldPosition);
        }

        float smoothing = 1f - Mathf.Exp(-zoomSmooth * Time.unscaledDeltaTime);
        cam.transform.localPosition = Vector3.Lerp(
            cam.transform.localPosition,
            finalCameraLocalPos,
            smoothing);
        currentDistance = -cam.transform.localPosition.z;
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
    public void ShakeImpact(
        Vector3 worldDirection,
        float duration = 0.12f,
        float strength = 0.08f,
        int vibrato = 18)
    {
        Shake(duration, strength, vibrato);

        if (cam == null || duration <= 0f || strength <= 0f)
            return;

        Vector3 direction = worldDirection.sqrMagnitude > 0.0001f
            ? worldDirection.normalized
            : transform.forward;
        Vector3 localDirection = cam.transform.InverseTransformDirection(direction);

        impactRotationTween?.Kill();
        cam.transform.localRotation = Quaternion.identity;

        Vector3 punchEuler = new Vector3(
            -strength * 24f,
            -localDirection.x * strength * 38f,
            localDirection.x * strength * 30f);

        impactRotationTween = cam.transform
            .DOPunchRotation(punchEuler, duration, Mathf.Max(1, vibrato), 0.55f)
            .SetUpdate(true)
            .OnComplete(() => cam.transform.localRotation = Quaternion.identity);
    }

    public void PunchFieldOfView(float delta, float duration = 0.3f)
    {
        if (cam == null || duration <= 0f)
            return;

        fovTween?.Kill();
        cam.fieldOfView = baseFieldOfView;

        float peakFieldOfView = Mathf.Clamp(baseFieldOfView + delta, 25f, 100f);
        float attackDuration = duration * 0.35f;
        float releaseDuration = duration - attackDuration;

        fovTween = DOTween.Sequence()
            .Append(DOTween.To(
                () => cam.fieldOfView,
                value => cam.fieldOfView = value,
                peakFieldOfView,
                attackDuration).SetEase(Ease.OutQuad))
            .Append(DOTween.To(
                () => cam.fieldOfView,
                value => cam.fieldOfView = value,
                baseFieldOfView,
                releaseDuration).SetEase(Ease.OutCubic))
            .SetUpdate(true);
    }

    public void PunchParryImpact()
    {
        if (cam == null)
            return;

        fovTween?.Kill(false);

        float parryFieldOfView = Mathf.Clamp(
            baseFieldOfView + parryFieldOfViewDelta,
            25f,
            100f);
        float impactFieldOfView = Mathf.Clamp(
            parryFieldOfView + parryImpactFieldOfViewDelta,
            25f,
            100f);

        fovTween = DOTween.Sequence()
            .Append(cam.DOFieldOfView(
                impactFieldOfView,
                Mathf.Max(0.01f, parryImpactZoomInDuration)).SetEase(Ease.OutQuad))
            .Append(cam.DOFieldOfView(
                parryFieldOfView,
                Mathf.Max(0.01f, parryImpactZoomOutDuration)).SetEase(Ease.OutCubic))
            .SetUpdate(true);
    }
    public void PlayParryCamera(Transform actor, Transform enemy)
    {
        if (actor == null || enemy == null || cam == null || pitchPivot == null)
            return;

        parryCameraTween?.Kill(false);
        fovTween?.Kill(false);

        Vector3 planarDirection = enemy.position - actor.position;
        planarDirection.y = 0f;

        // 교대 직전 카메라가 캐릭터의 어느 쪽에 있었는지 보존한다.
        // 이후 목표를 새 캐릭터로 바꿔도 같은 쪽 아래에서 패링을 보여 준다.
        Vector3 actorToCamera = cam.transform.position - actor.position;
        actorToCamera.y = 0f;
        float sideDot = Vector3.Dot(actorToCamera, actor.right);
        if (Mathf.Abs(sideDot) > 0.05f)
            parryCameraSide = Mathf.Sign(sideDot);

        float yawMagnitude = Mathf.Abs(parryYawOffset);
        if (parryCameraSide > 0f)
            yawMagnitude *= parryRightYawMultiplier;

        float signedYawOffset = -parryCameraSide * yawMagnitude;
        Vector3 signedCameraOffset = parryCameraOffset;
        signedCameraOffset.x = parryCameraSide * Mathf.Abs(parryCameraOffset.x);

        presentationTargetYaw = planarDirection.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(planarDirection.x, planarDirection.z) * Mathf.Rad2Deg + signedYawOffset
            : yaw;
        presentationTargetPitch = Mathf.Clamp(parryPitch, minPitch, maxPitch);

        float aimPeak = Mathf.Clamp01(parryAimStrength);
        float enterDuration = Mathf.Max(0.01f, parryEnterDuration);
        float targetFieldOfView = Mathf.Clamp(
            baseFieldOfView + parryFieldOfViewDelta,
            25f,
            100f);

        // 준비 구간에서 기억한 측면 구도로 회전하고, 낮은 숄더 이동과 거리/FOV 확대를 동시에 적용한다.
        parryCameraTween = DOTween.Sequence()
            .Append(DOTween.To(
                () => presentationCameraOffset,
                value => presentationCameraOffset = value,
                signedCameraOffset,
                enterDuration).SetEase(Ease.OutCubic))
            .Join(DOTween.To(
                () => presentationAimWeight,
                value => presentationAimWeight = value,
                aimPeak,
                enterDuration).SetEase(Ease.OutCubic))
            .Join(DOTween.To(
                () => presentationDistanceOffset,
                value => presentationDistanceOffset = value,
                parryDistanceOffset,
                enterDuration).SetEase(Ease.OutCubic))
            .Join(cam.DOFieldOfView(
                targetFieldOfView,
                enterDuration).SetEase(Ease.OutCubic))
            .SetUpdate(true);
    }

    public void ResolveParryCamera()
    {
        parryCameraTween?.Kill(false);

        float holdDuration = Mathf.Max(0f, parryHoldDuration);
        float returnDuration = Mathf.Max(0.01f, parryReturnDuration);

        // 충돌 프레임을 잠깐 붙잡은 뒤 모든 구도 값을 같은 시간축으로 복구한다.
        parryCameraTween = DOTween.Sequence()
            .AppendInterval(holdDuration)
            .Append(DOTween.To(
                () => presentationCameraOffset,
                value => presentationCameraOffset = value,
                Vector3.zero,
                returnDuration).SetEase(Ease.InOutCubic))
            .Join(DOTween.To(
                () => presentationAimWeight,
                value => presentationAimWeight = value,
                0f,
                returnDuration).SetEase(Ease.InOutCubic))
            .Join(DOTween.To(
                () => presentationDistanceOffset,
                value => presentationDistanceOffset = value,
                0f,
                returnDuration).SetEase(Ease.InOutCubic))
            .Join(cam.DOFieldOfView(
                baseFieldOfView,
                returnDuration).SetEase(Ease.InOutCubic))
            .SetUpdate(true)
            .OnComplete(ResetParryCamera);
    }

    public void EndParryCamera(float returnDuration = 0.12f)
    {
        parryCameraTween?.Kill(false);

        bool framingAlreadyReset =
            presentationCameraOffset.sqrMagnitude <= 0.000001f &&
            Mathf.Abs(presentationDistanceOffset) <= 0.001f &&
            presentationAimWeight <= 0.001f &&
            (cam == null || Mathf.Abs(cam.fieldOfView - baseFieldOfView) <= 0.01f);

        if (framingAlreadyReset)
        {
            ResetParryCamera();
            return;
        }

        float duration = Mathf.Max(0.01f, returnDuration);
        parryCameraTween = DOTween.Sequence()
            .Append(DOTween.To(
                () => presentationCameraOffset,
                value => presentationCameraOffset = value,
                Vector3.zero,
                duration).SetEase(Ease.OutCubic))
            .Join(DOTween.To(
                () => presentationAimWeight,
                value => presentationAimWeight = value,
                0f,
                duration).SetEase(Ease.OutCubic))
            .Join(DOTween.To(
                () => presentationDistanceOffset,
                value => presentationDistanceOffset = value,
                0f,
                duration).SetEase(Ease.OutCubic))
            .Join(cam.DOFieldOfView(
                baseFieldOfView,
                duration).SetEase(Ease.OutCubic))
            .SetUpdate(true)
            .OnComplete(ResetParryCamera);
    }

    private void ResetParryCamera()
    {
        presentationCameraOffset = Vector3.zero;
        presentationDistanceOffset = 0f;
        presentationAimWeight = 0f;
        presentationTargetYaw = yaw;
        presentationTargetPitch = pitch;

        if (cam != null)
            cam.fieldOfView = baseFieldOfView;
    }
}