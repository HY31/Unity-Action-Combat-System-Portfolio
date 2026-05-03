using UnityEngine;
using DG.Tweening;

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
        Debug.Log($"{name} 피격!");

        Transform root = OwnerRoot;

        Vector3 dir = (root.position - Camera.main.transform.position).normalized;
        dir.y = 0;

        root.DOMove(root.position + dir * 1.5f, 0.2f)
            .SetEase(Ease.OutQuad);
    }
}
