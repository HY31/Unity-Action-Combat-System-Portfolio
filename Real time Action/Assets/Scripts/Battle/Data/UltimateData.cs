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

    [Header("Movement")]
    public float forwardMoveSpeed = 4f;

    [Header("Auto Aim")]
    public bool useAutoAim = true;
    [Min(0f)] public float autoAimRadius = 5f;
    [Range(0f, 180f)] public float autoAimMaxAngle = 120f;
    [Min(0f)] public float autoAimRotationMultiplier = 2f;
    [Range(0f, 1f)] public float autoAimRotateUntil = 0.2f;
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
    public float hitStopDuration = 0.06f;
    public float cameraShakeDuration = 0.15f;
    public float cameraShakeStrength = 0.1f;
}
