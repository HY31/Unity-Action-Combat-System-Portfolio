using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Animation")]
    public string skillAnim;
    public string endAnim;

    [Header("Resource")]
    public float requiredEntryEnergy = 0f;
    public float energyCost = 40f;

    [Header("Timing")]
    [Range(0f, 1f)] public float moveStart = 0.2f;
    [Range(0f, 1f)] public float moveEnd = 0.5f;
    [Range(0f, 1f)] public float hitStart = 0.3f;
    [Range(0f, 1f)] public float hitEnd = 0.6f;
    [Range(0f, 1f)] public float chainInputOpenTime = 0.55f;

    [Header("Chain")] // 엘렌은 강화 특수 스킬 이후 추가적으로 에너지를 조금 써서 스킬 : 샤크나미 사용 가능 하기에 콤보 구현
    public SkillData nextSkill;

    [Header("HitBox")]
    public int hitBoxSlotIndex = -1;

    [Header("Movement")]
    public float forwardMoveSpeed = 4f;

    [Header("Feedback")]
    public float hitStopDuration = 0.06f;
    public float cameraShakeDuration = 0.15f;
    public float cameraShakeStrength = 0.1f;

    [Header("Auto Aim")]
    public bool useAutoAim = true;
    [Min(0f)] public float autoAimRadius = 4f;
    [Range(0f, 180f)] public float autoAimMaxAngle = 70f;
    [Min(0f)] public float autoAimRotationMultiplier = 2f;
    [Range(0f, 1f)] public float autoAimRotateUntil = 0.2f;
    public bool steerMoveToTarget = true;
}

