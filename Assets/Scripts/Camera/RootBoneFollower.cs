using UnityEngine;

public class RootBoneFollower : MonoBehaviour
{
    public Transform rootBone; // Bip001

    Vector3 lastPos;

    void Start()
    {
        lastPos = rootBone.position;
    }

    void LateUpdate()
    {
        // 애니메이션이 만든 루트 본의 프레임 이동량만 추적 오브젝트에 누적한다.
        Vector3 delta = rootBone.position - lastPos;

        transform.position += delta;

        lastPos = rootBone.position;
    }
}
