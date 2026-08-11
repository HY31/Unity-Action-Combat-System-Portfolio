using UnityEngine;

[System.Serializable]
public struct HitBoxShape
{
    [Tooltip("공격자 루트 기준 히트박스 중심 위치다.")]
    public Vector3 center;

    [Tooltip("히트박스의 가로, 세로, 깊이다.")]
    public Vector3 size;

    public bool HasValidSize =>
        size.x > 0f &&
        size.y > 0f &&
        size.z > 0f;

    public static HitBoxShape Default => new HitBoxShape
    {
        center = new Vector3(0f, 1f, 1f),
        size = new Vector3(2f, 2f, 2f)
    };

    public HitBoxShape Sanitized()
    {
        return new HitBoxShape
        {
            center = center,
            size = new Vector3(
                Mathf.Max(0.01f, Mathf.Abs(size.x)),
                Mathf.Max(0.01f, Mathf.Abs(size.y)),
                Mathf.Max(0.01f, Mathf.Abs(size.z)))
        };
    }
}
