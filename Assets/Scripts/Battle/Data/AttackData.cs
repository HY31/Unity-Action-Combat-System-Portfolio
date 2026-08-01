using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    [Header("Animation")]
    public string attackAnim;
    public string endAnim;
    [Min(0.01f)] public float playbackSpeed = 1f;

    [Header("Timing")]
    [Range(0, 1)] public float startUpEnd = 0.3f;
    [Range(0, 1)] public float activeEnd = 0.6f;
    [Range(0f, 1f)] public float comboInputOpenTime = 0.55f;
    [Range(0f, 1f)] public float endTransitionTime = 0.8f;
    [Range(0f, 1f)] public float locomotionRecoverTime = 0.2f;

    [Header("Cancel Timing")]
    [Tooltip("이 시점부터 회피 입력이 현재 공격을 취소할 수 있다.")]
    [Range(0f, 1f)] public float dodgeCancelOpenTime = 0f;

    [Tooltip("이 시점부터 스킬 입력이 현재 공격을 취소할 수 있다.")]
    [Range(0f, 1f)] public float skillCancelOpenTime = 0.45f;

    [Tooltip("이 시점부터 이동 입력이 현재 공격의 후속 동작을 취소할 수 있다.")]
    [Range(0f, 1f)] public float locomotionCancelOpenTime = 0.7f;

    [Header("Damage")]
    public float damageMultiplier = 1f;
    public float impactMultiplier = 1f;

    [Header("Hit")]
    public HitPayload hitPayload;

    [Header("Combo")]
    public int nextComboIndex = -1;

    [Header("Forward Move")]
    public float forwardMoveSpeed = 3f;
    [Tooltip("초당 이동 속도 대신 애니메이션 구간에 걸쳐 지정된 거리를 이동한다.")]
    public bool useDistanceBasedMovement;
    [Min(0f)] public float forwardMoveDistance = 1f;
    [Range(0, 1)] public float moveStart = 0.1f;
    [Range(0, 1)] public float moveEnd = 0.4f;

    [Header("Auto Aim")]
    public bool useAutoAim = true;
    [Min(0f)] public float autoAimRadius = 4f;
    [Range(0f, 180f)] public float autoAimMaxAngle = 70f;
    [Min(0f)] public float autoAimRotationMultiplier = 2f;
    [Range(0f, 1f)] public float autoAimRotateUntil = 0.2f;
    [Min(0f)] public float autoAimStopDistance = 0.8f;
    public bool steerMoveToTarget = true;
}


