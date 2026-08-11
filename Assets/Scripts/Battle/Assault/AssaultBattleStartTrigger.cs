using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class AssaultBattleStartTrigger : MonoBehaviour
{
    [SerializeField] private AssaultBattleController battleController;

    private BoxCollider triggerCollider;
    private Rigidbody triggerRigidbody;
    private bool triggered;

    private void Awake()
    {
        triggerCollider = GetComponent<BoxCollider>();
        triggerRigidbody = GetComponent<Rigidbody>();

        triggerCollider.isTrigger = true;
        triggerRigidbody.useGravity = false;
        triggerRigidbody.isKinematic = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered)
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        if (battleController == null)
            battleController = FindFirstObjectByType<AssaultBattleController>();

        if (battleController == null)
        {
            Debug.LogError("강습전 시작 실패: 전투 관리자를 찾을 수 없습니다.", this);
            return;
        }

        if (!battleController.BeginBattle())
            return;

        triggered = true;
        gameObject.SetActive(false);
    }

    public void Configure(AssaultBattleController controller)
    {
        battleController = controller;
    }

    private void Reset()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;

        Rigidbody rigidbodyComponent = GetComponent<Rigidbody>();
        rigidbodyComponent.useGravity = false;
        rigidbodyComponent.isKinematic = true;
    }
}
