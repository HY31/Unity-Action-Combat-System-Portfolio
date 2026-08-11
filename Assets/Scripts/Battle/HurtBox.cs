using UnityEngine;

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

    public bool TryTakeHit(
        CombatHitData hitData,
        Vector3 sourcePosition,
        bool heavyReaction)
    {
        // 반환값은 HitBox가 후속 적중 연출과 보상을 실행해도 되는지를 뜻한다.
        if (ownerPlayer != null)
            return ownerPlayer.TryReceiveHit(hitData, sourcePosition, heavyReaction);

        if (ownerEnemy != null)
        {
            // 피해, 그로기, 이상 축적, 경직 누적은 처리하지만
            // 보스의 월드 위치는 이동시키지 않는다.
            ownerEnemy.ReceiveHit(hitData);
            return true;
        }

        return true;
    }
}
