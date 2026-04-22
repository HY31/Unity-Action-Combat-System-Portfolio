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
        Debug.Log($"{name} ÇÇ°Ý!");

        Transform root = OwnerRoot;

        Vector3 dir = (root.position - Camera.main.transform.position).normalized;
        dir.y = 0;

        root.DOMove(root.position + dir * 1.5f, 0.2f)
            .SetEase(Ease.OutQuad);
    }
}
