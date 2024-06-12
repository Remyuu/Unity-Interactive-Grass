using UnityEngine;

public class CircularMotion : MonoBehaviour
{
    public Vector3 center = new Vector3(0, 0, 0);  // 圆心位置
    public float radius = 5.0f;  // 圆的半径
    public float speed = 1.0f;  // 运动速度

    private float angle = 0.0f;  // 角度初始化

    void Update()
    {
        // 计算圆周运动的新位置
        angle += speed * Time.deltaTime;  // 随时间增加角度
        float x = center.x + radius * Mathf.Cos(angle);  // X位置
        float z = center.z + radius * Mathf.Sin(angle);  // Z位置

        // 设置物体的位置，Y轴保持不变
        transform.position = new Vector3(x, center.y, z);
    }
}