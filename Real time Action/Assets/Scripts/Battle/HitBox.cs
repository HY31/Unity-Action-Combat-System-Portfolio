using UnityEngine;

public class HitBox : MonoBehaviour
{
    private Collider hitCollider;
    private bool active;

    [SerializeField] private Transform ownerRoot;
    private ThirdPersonCameraController camController;

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

        Debug.Log("공격 적중!");
        camController?.Shake();

        hurtBox.TakeHit();
        HitStop.DoHitStop(0.05f);
    }
}
