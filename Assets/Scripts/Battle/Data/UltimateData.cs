using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Ultimate Data")]

public class UltimateData : ScriptableObject
{
    [Header("Animation")]
    public string ultStartAnim;
    public string ultHitAnim;
    public string ultEndAnim;

    [Header("Resource")]
    public float decibelCost = 3000f;

    [Header("Damage")]
    public float damageMultiplier = 1f;
    public float impactMultiplier = 1f;

    [Header("Hit")]
    public HitPayload hitPayload;

    [Header("HitBox Shape")]
    public HitBoxShape hitBoxShape = HitBoxShape.Default;

    [Header("Movement")]
    public float forwardMoveSpeed = 4f;
    [Tooltip("설정한 정규화 시간 구간에 걸쳐 지정된 거리를 이동한다.")]
    public bool useDistanceBasedMovement;
    [Min(0f)] public float forwardMoveDistance = 2f;

    [Header("Auto Aim")]
    public bool useAutoAim = true;
    [Min(0f)] public float autoAimRadius = 5f;
    [Range(0f, 180f)] public float autoAimMaxAngle = 120f;
    [Min(0f)] public float autoAimRotationMultiplier = 2f;
    [Range(0f, 1f)] public float autoAimRotateUntil = 0.2f;
    [Min(0f)] public float autoAimStopDistance = 0.8f;
    public bool steerMoveToTarget = true;

    [Header("HitBox")]
    public HitWindow[] hitWindows =
    {
        new HitWindow{ start = 0.15f, end = 0.20f},
        new HitWindow{ start = 0.30f, end = 0.35f},
        new HitWindow{ start = 0.48f, end = 0.55f}
    };

    [Header("Timing")]
    [Range(0f, 1f)] public float moveStart = 0.2f;
    [Range(0f, 1f)] public float moveEnd = 0.5f;

    [Header("Feedback")]
    public HitFeedbackData hitFeedback = HitFeedbackData.Default;
}
