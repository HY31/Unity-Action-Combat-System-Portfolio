using UnityEngine;

/// <summary>
/// 궁극기와 콤보 스킬이 공유하는 애니메이션, 영상, 다단 판정과 접근 이동 규칙을 정의한다.
/// </summary>
[CreateAssetMenu(menuName = "Combat/Ultimate Data")]

public class UltimateData : ScriptableObject
{
    [Header("Animation")]
    public string ultStartAnim;
    public string ultHitAnim;
    public string ultEndAnim;

    [Header("콤보 스킬 애니메이션")]
    [Tooltip("콤보 스킬 진입 시 궁극기 공격 애니메이션을 대신할 클립입니다.")]
    public AnimationClip chainSkillAnim;

    [Tooltip("콤보 스킬 공격 후 재생할 종료 애니메이션입니다.")]
    public AnimationClip chainSkillEndAnim;

    [Header("Cinematic")]
    [Tooltip("콤보 스킬이 아닌 실제 궁극기에서만 전체 화면으로 재생할 영상입니다.")]
    public UnityEngine.Video.VideoClip cinematicClip;

    [Header("Resource")]
    public float decibelCost = 3000f;

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

    [Tooltip("마지막 타격 이후 종료 애니메이션으로 넘어갈 공격 클립의 정규화 시점이다.")]
    [Range(0f, 1f)] public float hitEndTransitionTime = 0.68f;

    [Header("Feedback")]
    public HitFeedbackData hitFeedback = HitFeedbackData.Default;
}
