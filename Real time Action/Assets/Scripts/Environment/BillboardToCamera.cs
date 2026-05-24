using UnityEngine;

public class BillboardToCamera : MonoBehaviour
{
    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}
