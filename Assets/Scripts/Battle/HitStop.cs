using UnityEngine;
using DG.Tweening;

public class HitStop : MonoBehaviour
{
    public static void DoHitStop(float duration = 0.05f)
    {
        Time.timeScale = 0f;

        // timeScale이 0인 동안에도 복구 콜백이 실행되도록 실제 시간 기준으로 갱신한다.
        DOVirtual.DelayedCall(duration, () =>
        {
            Time.timeScale = 1f;
        }).SetUpdate(true);
    }
}
