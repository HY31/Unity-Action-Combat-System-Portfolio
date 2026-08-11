using UnityEngine;

public enum DecibelRewardType
{
    None,
    NormalAttack,
    Skill
}

[RequireComponent(typeof(BoxCollider))]
public class HitBox : MonoBehaviour
{
    private BoxCollider hitCollider;
    private bool active;

    [Header("AttackStats")]
    // 상태가 계산한 런타임 히트 정보를 실제 Trigger 충돌 시점까지 보관한다.
    private CombatHitData hitData;

    // ownerRoot는 공격자 스탯 조회가 아니라 자기 자신과의 충돌을 거르는 소유권 기준이다.
    [SerializeField] private Transform ownerRoot;
    private ThirdPersonCameraController camController;

    private DecibelRewardType rewardType = DecibelRewardType.None;
    private HitFeedbackData feedback = HitFeedbackData.Default;

    private void Awake()
    {
        hitCollider = GetComponent<BoxCollider>();
        SetActive(false);

        if (Camera.main != null)
            camController = Camera.main.GetComponentInParent<ThirdPersonCameraController>();
    }

    public void SetHitData(CombatHitData hitData)
    {
        this.hitData = hitData;
    }

    public void SetActive(bool value)
    {
        active = value;

        if (hitCollider != null)
            hitCollider.enabled = value;
    }

    public void SetRewardType(DecibelRewardType type)
    {
        rewardType = type;
    }

    public void SetFeedback(HitFeedbackData value)
    {
        feedback = value.Sanitized();
    }

    public void ConfigureShape(HitBoxShape value)
    {
        if (hitCollider == null)
            hitCollider = GetComponent<BoxCollider>();

        if (hitCollider == null)
            return;

        // 공격 데이터만 교체해 같은 오브젝트를 모든 공격 패턴에서 재사용한다.
        HitBoxShape shape = value.Sanitized();
        hitCollider.center = shape.center;
        hitCollider.size = shape.size;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!active) return;

        HurtBox hurtBox = other.GetComponent<HurtBox>();

        if (hurtBox == null)
            hurtBox = other.GetComponentInParent<HurtBox>();

        if (hurtBox == null)
            return;

        if (ownerRoot != null && hurtBox.OwnerRoot == ownerRoot)
            return;

        Vector3 sourcePosition = ownerRoot != null
            ? ownerRoot.position
            : transform.position;
        bool heavyReaction =
            feedback.hitStopDuration >= 0.07f ||
            feedback.vfxScale >= 1.25f;

        // 무적 등으로 피격이 거부되면 카메라·히트스톱·자원 보상도 발생시키지 않는다.
        if (hurtBox.TryTakeHit(hitData, sourcePosition, heavyReaction))
        {
            Vector3 targetPosition = hurtBox.OwnerRoot != null
                ? hurtBox.OwnerRoot.position
                : other.bounds.center;
            Vector3 hitDirection = targetPosition - sourcePosition;
            Vector3 impactPoint = other.ClosestPoint(transform.position);

            // 피격이 확정된 순간의 접촉 위치와 공격 속성만 연출 계층으로 전달한다.
            CombatHitVfx.Play(
                impactPoint,
                hitDirection,
                hitData.resolvedElement,
                feedback.vfxScale);
            CombatPresentationEffects.PlayHit(
                hitData.resolvedElement,
                feedback.vfxScale);

            camController?.ShakeImpact(
                hitDirection,
                feedback.cameraShakeDuration,
                feedback.cameraShakeStrength,
                feedback.cameraShakeVibrato);
            HitStop.DoHitStop(feedback.hitStopDuration);

            // 공격의 실제 적중이 확정된 뒤에만 공격 종류에 맞는 데시벨을 지급한다.
            PlayerController ownerPlayer = ownerRoot != null ? ownerRoot.GetComponent<PlayerController>() : null;

            switch (rewardType)
            {
                case DecibelRewardType.NormalAttack:
                    ownerPlayer?.GrantDecibelForNormalHit();
                    break;

                case DecibelRewardType.Skill:
                    ownerPlayer?.GrantDecibelForSkillHit();
                    break;
            }
        }
    }
}
