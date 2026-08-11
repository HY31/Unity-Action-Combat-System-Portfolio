using UnityEngine;

/// <summary>
/// 물리 콜라이더 대신 활성 캐릭터와 보스 사이의 최소 수평 거리를 유지한다.
/// 캐릭터의 높이는 변경하지 않으므로 공중으로 튀거나 보스 위에 착지하지 않는다.
/// </summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class EnemyBodyBlocker : MonoBehaviour
{
    [SerializeField] private PartyManager partyManager;
    [SerializeField, Min(0.1f)] private float horizontalRadius = 2f;
    [SerializeField] private Collider legacyBodyCollider;

    private void Awake()
    {
        ResolveReferences();
        DisableLegacyCollider();
    }

    private void OnEnable()
    {
        ResolveReferences();
        DisableLegacyCollider();
    }

    private void LateUpdate()
    {
        if (partyManager == null)
            partyManager = FindFirstObjectByType<PartyManager>();

        PlayerController player = partyManager != null
            ? partyManager.GetCurrentCharacter()
            : null;
        if (player == null || !player.gameObject.activeInHierarchy)
            return;

        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;

        float radius = Mathf.Max(0.1f, horizontalRadius);
        float distanceSquared = offset.sqrMagnitude;
        if (distanceSquared >= radius * radius)
            return;

        Vector3 direction = distanceSquared > 0.0001f
            ? offset.normalized
            : -transform.forward;
        direction.y = 0f;
        direction.Normalize();

        float distance = Mathf.Sqrt(distanceSquared);
        Vector3 correction = direction * (radius - distance);
        correction.y = 0f;

        // CharacterController에 수평 변위만 전달해 기존 중력과 지면 판정은 그대로 유지한다.
        CharacterController characterController = player.Controller;
        if (characterController != null && characterController.enabled)
            characterController.Move(correction);
        else
            player.transform.position += correction;
    }

    public void Configure(PartyManager manager, float radius)
    {
        partyManager = manager;
        horizontalRadius = Mathf.Max(0.1f, radius);
        ResolveReferences();
        DisableLegacyCollider();
    }

    private void ResolveReferences()
    {
        if (partyManager == null)
            partyManager = FindFirstObjectByType<PartyManager>();

        if (legacyBodyCollider == null)
        {
            Transform body = transform.Find("BodyCollision");
            if (body != null)
                legacyBodyCollider = body.GetComponent<Collider>();
        }
    }

    private void DisableLegacyCollider()
    {
        if (legacyBodyCollider != null)
            legacyBodyCollider.enabled = false;
    }
}
