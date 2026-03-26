//using UnityEngine;

//public class CameraFollowTarget : MonoBehaviour
//{
//    [SerializeField] private Transform targetBone;
//    [SerializeField] private Vector3 offset = new Vector3(0f, 1.2f, 0f);

//    [SerializeField] private float horizontalSmooth = 20f;
//    [SerializeField] private float verticalSmooth = 8f;

//    private Vector3 lockedMoveDirection;
//    private Vector2 lastMoveInput;
//    private bool hasLockedMoveDirection;

//    public Vector3 LockedMoveDirection => lockedMoveDirection;

//    private void LateUpdate()
//    {
//        if (targetBone == null) return;

//        Vector3 targetPos = targetBone.position + offset;
//        Vector3 current = transform.position;

//        float x = Mathf.Lerp(current.x, targetPos.x, horizontalSmooth * Time.deltaTime);
//        float z = Mathf.Lerp(current.z, targetPos.z, horizontalSmooth * Time.deltaTime);
//        float y = Mathf.Lerp(current.y, targetPos.y, verticalSmooth * Time.deltaTime);

//        transform.position = new Vector3(x, y, z);
//    }

//    public Vector3 GetCameraRelativeDirection(Vector2 input)
//    {
//        if (cameraTransform == null)
//            return Vector3.zero;

//        Vector3 forward = cameraTransform.forward;
//        Vector3 right = cameraTransform.right;

//        forward.y = 0f;
//        right.y = 0f;

//        forward.Normalize();
//        right.Normalize();

//        Vector3 dir = forward * input.y + right * input.x;

//        if (dir.sqrMagnitude > 1f)
//            dir.Normalize();

//        return dir;
//    }
//}
