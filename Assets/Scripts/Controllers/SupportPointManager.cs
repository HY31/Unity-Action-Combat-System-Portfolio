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

    // Update is called once per frame
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
