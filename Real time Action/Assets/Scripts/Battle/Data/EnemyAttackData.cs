using UnityEngine;

public enum WarningType
{
    None,
    Yellow,
    Red
}

[CreateAssetMenu(menuName = "Combat(Enemy)/Enemy Attack Data")]

public class EnemyAttackData : ScriptableObject
{
    [Header("Animation")]
    public string attackAnim;
    public string endAnim;

    [Header("Warning")]
    public WarningType warningType = WarningType.None;
    [Range(0f, 1f)] public float warningStart = 0.15f;
    [Range(0f, 1f)] public float warningEnd = 0.28f;

    [Header("Timing")]
    [Range(0f, 1f)] public float startUpEnd = 0.3f;
    [Range(0f, 1f)] public float activeEnd = 0.5f;

    [Range(0f, 1f)] public float reactionStart = 0.15f;
    [Range(0f, 1f)] public float reactionEnd = 0.4f;

    [Header("Movement")]
    public float forwardMoveSpeed = 4f;
    [Range(0f, 1f)] public float moveStart = 0f;
    [Range(0f, 1f)] public float moveEnd = 0.2f;

    [Header("Combat")]
    public float damage = 10f;
}
