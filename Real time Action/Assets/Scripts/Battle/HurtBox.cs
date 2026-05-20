using UnityEngine;
using DG.Tweening;

public class HurtBox : MonoBehaviour
{
    [SerializeField] private Transform ownerRoot;
    private PlayerController ownerPlayer;

    public Transform OwnerRoot => ownerRoot;


    private void Reset()
    {
        ownerRoot = transform.root;
    }

    private void Awake()
    {
        if (ownerRoot != null)
            ownerPlayer = ownerRoot.GetComponent<PlayerController>();
    }

    public void TakeHit()
    {
        if(ownerPlayer != null)
        {
            ownerPlayer.ReceiveHit();
            return;
        }

        Debug.Log($"{name} 피격!"); // 테스트용

        Transform root = OwnerRoot;

        Vector3 dir = (root.position - Camera.main.transform.position).normalized;
        dir.y = 0;

        root.DOMove(root.position + dir * 1.5f, 0.2f)
            .SetEase(Ease.OutQuad);
    }
}
