using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    [Header("Animation")]
    public string attackAnim;
    public string endAnim;

    [Header("Timing")]
    [Range(0, 1)] public float startUpEnd = 0.3f;
    [Range(0, 1)] public float activeEnd = 0.6f;
    [Range(0f, 1f)] public float comboInputOpenTime = 0.55f;

    [Header("Combo")]
    public int nextComboIndex = -1;

    [Header("Forward Move")]
    public float forwardMoveSpeed = 3f;
    [Range(0, 1)] public float moveStart = 0.1f;
    [Range(0, 1)] public float moveEnd = 0.4f;

    [Header("Auto Aim")]
    public bool useAutoAim = true;
    [Min(0f)] public float autoAimRadius = 4f;
    [Range(0f, 180f)] public float autoAimMaxAngle = 70f;
    [Min(0f)] public float autoAimRotationMultiplier = 2f;
    [Range(0f, 1f)] public float autoAimRotateUntil = 0.2f;
    public bool steerMoveToTarget = true;
}
