using UnityEngine;

public enum CombatHitRewardType
{
    None,
    NormalAttack,
    Skill
}

/// <summary>
/// 상태가 구성한 런타임 히트 정보를 Trigger 충돌에 적용하고, 적중 연출과 전투 보상을 전달한다.
/// 공격 종류별 계산은 하지 않고 전달받은 CombatHitData만 소비한다.
/// </summary>
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

    private CombatHitRewardType rewardType = CombatHitRewardType.None;
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

    public void SetChainSkillTriggerEnabled(bool enabled)
    {
        // 같은 공격의 다단 타격 사이에서도 마지막 타격 구간만 강공격으로 바꿀 수 있게 한다.
        hitData.canTriggerChainSkill = enabled;
    }

    public void SetActive(bool value)
    {
        active = value;

        if (hitCollider != null)
            hitCollider.enabled = value;
    }

    public void SetManualActive(bool value)
    {
        active = value;

        if (hitCollider != null)
            hitCollider.enabled = false;
    }

    public void SetRewardType(CombatHitRewardType type)
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
        TryHit(other, ResolveImpactPoint(other));
    }

    private Vector3 ResolveImpactPoint(Collider other)
    {
        if (other == null)
            return transform.position;

        bool supportsClosestPoint =
            other is BoxCollider ||
            other is SphereCollider ||
            other is CapsuleCollider ||
            other is MeshCollider meshCollider && meshCollider.convex;

        // 비볼록 메시와 지형 콜라이더는 Collider.ClosestPoint를 지원하지 않으므로 Bounds 근사값을 사용한다.
        return supportsClosestPoint
            ? other.ClosestPoint(transform.position)
            : other.bounds.ClosestPoint(transform.position);
    }

    public bool TryHit(Collider other, Vector3 impactPoint)
    {
        if (!active || other == null)
            return false;

        HurtBox hurtBox = other.GetComponent<HurtBox>();

        if (hurtBox == null)
            hurtBox = other.GetComponentInParent<HurtBox>();

        if (hurtBox == null)
            return false;

        if (ownerRoot != null && hurtBox.OwnerRoot == ownerRoot)
            return false;

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

            // 공격의 실제 적중이 확정된 뒤에만 공격 종류에 맞는 전투 자원을 지급한다.
            PlayerController ownerPlayer = ownerRoot != null ? ownerRoot.GetComponent<PlayerController>() : null;

            switch (rewardType)
            {
                case CombatHitRewardType.NormalAttack:
                    ownerPlayer?.GrantResourcesForNormalHit();
                    break;

                case CombatHitRewardType.Skill:
                    ownerPlayer?.GrantResourcesForSkillHit();
                    break;
            }

            return true;
        }

        return false;
    }
}
