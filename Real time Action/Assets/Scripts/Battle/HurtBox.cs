using UnityEngine;

public class HurtBox : MonoBehaviour
{
    [SerializeField] private Transform ownerRoot;

    public Transform OwnerRoot => ownerRoot;

    private void Reset()
    {
        ownerRoot = transform.root;
    }

    public void TakeHit()
    {
        Debug.Log($"{name} ÇÇ°Ý!");
    }
}
