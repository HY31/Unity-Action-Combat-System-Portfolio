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
        TryBeginBattle(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // 영상 재생 중 트리거 안에 머물렀더라도 오프닝이 끝난 뒤 다시 시작을 시도한다.
        TryBeginBattle(other);
    }

    private void TryBeginBattle(Collider other)
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
