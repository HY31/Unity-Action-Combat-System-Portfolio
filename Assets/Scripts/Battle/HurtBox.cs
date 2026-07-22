using UnityEngine;
using DG.Tweening;

public class HurtBox : MonoBehaviour
{
    [SerializeField] private Transform ownerRoot;
    private PlayerController ownerPlayer;
    public Transform OwnerRoot => ownerRoot;

    private EnemyController ownerEnemy;


    private void Reset()
    {
        ownerRoot = transform.root;
    }

    private void Awake()
    {
        if (ownerRoot != null)
        {
            // HurtBox는 피격 진입점만 공유하고 실제 상태 변경은 소유 컨트롤러에 위임한다.
            ownerPlayer = ownerRoot.GetComponent<PlayerController>();

            if (ownerPlayer == null)
                ownerEnemy = ownerRoot.GetComponent<EnemyController>();
        }
    }

    public bool TryTakeHit(CombatHitData hitData)
    {
        // 반환값은 HitBox가 후속 적중 연출과 보상을 실행해도 되는지를 뜻한다.
        if (ownerPlayer != null)
            return ownerPlayer.TryReceiveHit();

        if (ownerEnemy != null)
        {
            ownerEnemy.ReceiveHit(hitData);
            ApplyNockback();
            return true;
        }

        ApplyNockback();
        return true;
    }

    public void ApplyNockback()
    {
        Transform root = OwnerRoot;

        Vector3 dir = (root.position - Camera.main.transform.position).normalized;
        dir.y = 0;

        root.DOMove(root.position + dir * 1.5f, 0.2f)
            .SetEase(Ease.OutQuad);
    }
}
