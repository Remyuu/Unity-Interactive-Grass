using UnityEngine;

public class SmoothCameraFollow : MonoBehaviour
{
    public Transform target; // 目标对象，摄像机需要跟随的对象
    public float smoothSpeed = 0.125f; // 平滑移动的速度
    public Vector3 offset; // 相对于目标对象的偏移

    void LateUpdate()
    {
        // 目标位置加上偏移
        Vector3 desiredPosition = target.position + offset;
        // 使用线性插值平滑过渡当前位置到目标位置
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        // 更新摄像机位置
        transform.position = smoothedPosition;

        // 让摄像机始终面向目标对象
        transform.LookAt(target);
    }
}