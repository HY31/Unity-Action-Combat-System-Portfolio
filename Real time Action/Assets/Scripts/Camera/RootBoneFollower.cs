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
        Vector3 delta = rootBone.position - lastPos;

        transform.position += delta;

        lastPos = rootBone.position;
    }
}