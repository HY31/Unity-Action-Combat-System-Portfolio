using UnityEngine;

public class SupportPointManager : MonoBehaviour
{
    private int maxSupportPoint = 6;
    private int currentSupportPoint = 6;
    public int MaxSupportPoint => maxSupportPoint;
    public int CurrentSupportPoint => currentSupportPoint;

    void Start()
    {
        
    }

    // 매 프레임 지원 포인트 UI를 현재 값과 동기화한다.
    void Update()
    {
        
    }

    public bool HasEnoughSupportPoint(int cost = 1)
    {
        return currentSupportPoint >= cost;
    }

    public bool TryUseSupportPoint(int cost = 1)
    {
        if (currentSupportPoint < cost)
            return false;

        currentSupportPoint -= cost;
        return true;
    }

    public void GainSupportPoint(int amount = 1)
    {
        currentSupportPoint = Mathf.Clamp(
            currentSupportPoint + amount,
            0,
            maxSupportPoint
        );
    }
}
