using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform followTarget; // CameraFollowTarget
    [SerializeField] private Transform pivot;

    [SerializeField] private float mouseSensitivity = 300f;
    [SerializeField] private float followSmooth = 15f;

    private float yaw;
    private float pitch;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked; 
    }

    private void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -30f, 60f);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        pivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void LateUpdate()
    {
        if (followTarget == null) return;

        transform.position = Vector3.Lerp(
            transform.position,
            followTarget.position,
            followSmooth * Time.deltaTime
        );
    }
}
