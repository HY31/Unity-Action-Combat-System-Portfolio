using UnityEngine;
using DG.Tweening;

public class HitStop : MonoBehaviour
{
    public static void DoHitStop(float duration = 0.05f)
    {
        Time.timeScale = 0f;

        DOVirtual.DelayedCall(duration, () =>
        {
            Time.timeScale = 1f;
        }).SetUpdate(true);
    }
}
