using UnityEngine;

public enum DecibelRewardType
{
    None,
    NormalAttack,
    Skill
}

public class HitBox : MonoBehaviour
{
    private Collider hitCollider;
    private bool active;

    [SerializeField] private Transform ownerRoot;
    private ThirdPersonCameraController camController;

    private DecibelRewardType rewardType = DecibelRewardType.None;

    private void Awake()
    {
        hitCollider = GetComponent<Collider>();
        SetActive(false);

        camController = Camera.main.GetComponentInParent<ThirdPersonCameraController>();
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

    private void OnTriggerEnter(Collider other)
    {
        if (!active) return;

        Debug.Log(other);

        HurtBox hurtBox = other.GetComponent<HurtBox>();
        Debug.Log($"hurtBox = {hurtBox}");

        if (hurtBox == null)
            hurtBox = other.GetComponentInParent<HurtBox>();

        if (hurtBox == null)
            return;

        if (ownerRoot != null && hurtBox.OwnerRoot == ownerRoot)
            return;

        // 데시벨 얻는 부분
        PlayerController ownerPlayer = ownerRoot != null? ownerRoot.GetComponent<PlayerController>() : null;

        switch (rewardType)
        {
            case DecibelRewardType.NormalAttack:
                ownerPlayer?.GrantDecibelForNormalHit();
                break;

            case DecibelRewardType.Skill:
                ownerPlayer?.GrantDecibelForSkillHit();
                break;
        }

        camController?.Shake();

        hurtBox.TakeHit();
        HitStop.DoHitStop(0.05f);
    }
}
