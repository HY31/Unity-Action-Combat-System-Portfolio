using System;
using UnityEngine;

public enum WarningType
{
    None,
    Yellow,
    Red
}

public enum EnemyHitDetectionMode
{
    BodyBox,
    WeaponSweep
}
[Serializable]
public struct EnemyAttackWindow
{
    [Range(0f, 1f)] public float start;
    [Range(0f, 1f)] public float end;
}

[Serializable]
public struct EnemyAttackSoundWindow
{
    [Tooltip("해당 HitActive 구간 직전에 재생할 Resources/Audio/SFX/Combat 음원 이름이다.")]
    public string swingClipName;

    [Tooltip("해당 HitActive 구간이 시작되는 순간 함께 재생할 음원 이름이다.")]
    public string hitActiveClipName;
}

[CreateAssetMenu(menuName = "Combat(Enemy)/Enemy Attack Data")]

public class EnemyAttackData : ScriptableObject
{
    [Header("Animation")]
    public string attackAnim;
    public string endAnim;

    [Tooltip("이 공격 애니메이션의 재생 배율이다.")]
    [Min(0.01f)] public float playbackSpeed = 1f;

    [Header("AI Selection")]
    [Tooltip("AI가 이 공격을 선택할 수 있는 최소 수평 거리다.")]
    [Min(0f)] public float minimumUseDistance;

    [Tooltip("AI가 이 공격을 선택할 수 있는 최대 수평 거리다.")]
    [Min(0f)] public float maximumUseDistance = 5f;

    [Tooltip("값이 클수록 같은 거리의 후보 중 선택될 확률이 높아지며, 0이면 선택되지 않는다.")]
    [Min(0f)] public float selectionWeight = 1f;

    [Tooltip("이 공격을 선택한 뒤 다시 선택 후보가 되기까지의 시간이다.")]
    [Min(0f)] public float selectionCooldown;

    [Tooltip("이 공격이 끝난 뒤 공통 공격 간격에 추가되는 재정비 시간이다.")]
    [Min(0f)] public float additionalRecoveryDelay;

    [Header("Warning")]
    public WarningType warningType = WarningType.None;
    [Range(0f, 1f)] public float warningStart = 0.15f;
    [Range(0f, 1f)] public float warningEnd = 0.28f;

    [Header("Timing")]
    [Range(0f, 1f)] public float startUpEnd = 0.3f;
    [Range(0f, 1f)] public float activeEnd = 0.5f;

    [Range(0f, 1f)] public float reactionStart = 0.15f;
    [Range(0f, 1f)] public float reactionEnd = 0.4f;

    [Header("Optional Multi-hit Timing")]
    [Tooltip("활성화하면 위 단일 구간 대신 아래 배열의 모든 구간을 사용한다.")]
    public bool useTimingWindows;

    public EnemyAttackWindow[] warningWindows;
    public EnemyAttackWindow[] activeWindows;
    public EnemyAttackWindow[] reactionWindows;

    [Header("Optional Follow-up Animation")]
    [Tooltip("준비 모션 뒤에 이어서 재생할 방출/후속 공격 클립이다.")]
    public string followUpAnim;

    [Min(0.01f)] public float followUpPlaybackSpeed = 1f;

    [Tooltip("주 공격 클립의 이 정규화 시점에서 후속 클립으로 전환한다.")]
    [Range(0f, 1f)] public float followUpStartNormalized = 1f;

    public EnemyAttackWindow[] followUpWarningWindows;
    public EnemyAttackWindow[] followUpActiveWindows;
    public EnemyAttackWindow[] followUpReactionWindows;

    [Header("Audio")]
    [Tooltip("기본 공격의 activeWindows와 같은 인덱스로 대응하는 공격음 설정이다.")]
    public EnemyAttackSoundWindow[] soundWindows;

    [Tooltip("후속 공격의 followUpActiveWindows와 같은 인덱스로 대응하는 공격음 설정이다.")]
    public EnemyAttackSoundWindow[] followUpSoundWindows;

    [Tooltip("휘두름 소리를 HitActive보다 먼저 재생할 정규화 시간이다.")]
    [Range(0f, 0.5f)] public float swingSoundLeadTime = 0.1f;

    [Header("Movement")]
    [Tooltip("이 공격이 보스 루트를 전진시키는지 결정한다.")]
    public bool useForwardMovement;

    [Tooltip("초당 속도 대신 공격 전체에서 이동할 총거리를 사용한다.")]
    public bool useDistanceBasedMovement = true;

    [Min(0f)] public float forwardMoveDistance = 2f;

    [Min(0f)]
    public float forwardMoveSpeed = 4f;
    [Range(0f, 1f)] public float moveStart = 0f;
    [Range(0f, 1f)] public float moveEnd = 0.2f;

    [Header("Hit Box Shape")]
    [Tooltip("이 패턴이 기본 공격 콜라이더 대신 전용 중심과 크기를 사용하는지 결정한다.")]
    public bool useCustomHitBoxShape;

    public Vector3 hitBoxCenter = new Vector3(0f, 1.5f, 2f);
    public Vector3 hitBoxSize = new Vector3(5f, 3f, 4f);

    [Tooltip("후속 공격 클립에서 별도의 콜라이더 모양을 사용하는지 결정한다.")]
    public bool useFollowUpHitBoxShape;

    public Vector3 followUpHitBoxCenter = new Vector3(0f, 1.5f, 2f);
    public Vector3 followUpHitBoxSize = new Vector3(5f, 3f, 4f);

    [Header("Hit Detection")]
    [Tooltip("Body Box는 기존 박스 Trigger를, Weapon Sweep은 지정 본의 프레임 간 궤적을 사용한다.")]
    public EnemyHitDetectionMode hitDetectionMode;

    [Tooltip("Weapon Sweep의 기준이 되는 Animator 하위 본 이름이다.")]
    public string weaponSweepBoneName;

    [Min(0.01f)] public float weaponSweepRadius = 0.65f;
    public Vector3 weaponSweepLocalStart;
    public Vector3 weaponSweepLocalEnd = Vector3.up;
    [Range(2, 12)] public int weaponSweepPathSamples = 5;

    [Tooltip("후속 클립이 별도의 판정 방식을 사용하는지 결정한다.")]
    public bool overrideFollowUpHitDetection;
    public EnemyHitDetectionMode followUpHitDetectionMode;
    public string followUpWeaponSweepBoneName;
    [Min(0.01f)] public float followUpWeaponSweepRadius = 0.65f;
    public Vector3 followUpWeaponSweepLocalStart;
    public Vector3 followUpWeaponSweepLocalEnd = Vector3.up;
    [Range(2, 12)] public int followUpWeaponSweepPathSamples = 5;

    [Header("Target Tracking")]
    public bool useTargetTracking;

    [Min(0f)] public float targetTrackingRotationSpeed = 420f;

    [Tooltip("추적 중 전진 방향도 현재 대상 쪽으로 갱신한다.")]
    public bool steerMovementWhileTracking = true;

    public EnemyAttackWindow[] targetTrackingWindows;
    public EnemyAttackWindow[] followUpTargetTrackingWindows;

    [Header("Feedback")]
    public HitFeedbackData hitFeedback = HitFeedbackData.Default;

    [Header("Combat")]
    public float damage = 10f;

    public bool CanUseAtDistance(float distance)
    {
        float minimum = Mathf.Max(0f, minimumUseDistance);
        float maximum = Mathf.Max(minimum, maximumUseDistance);

        return distance >= minimum && distance <= maximum;
    }
}
