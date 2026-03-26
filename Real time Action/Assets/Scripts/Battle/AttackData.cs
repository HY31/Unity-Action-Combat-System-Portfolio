using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    public string animationName;

    [Range(0, 1)] public float startUpEnd = 0.3f;
    [Range(0, 1)] public float activeEnd = 0.6f;

    public int nextComboIndex = -1;

    [Header("Forward Move")]
    public float forwardMoveSpeed = 3f;
    [Range(0, 1)] public float moveStart = 0.2f;
    [Range(0, 1)] public float moveEnd = 0.5f;
}
