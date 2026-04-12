using UnityEngine;

public class HitBox : MonoBehaviour
{
    private Collider hitCollider;
    private bool active;

    [SerializeField] private Transform ownerRoot;

    private void Awake()
    {
        hitCollider = GetComponent<Collider>();
        SetActive(false);
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

        Debug.Log("Àû ¸ÂÀ½!");
        hurtBox.TakeHit();
    }
}
