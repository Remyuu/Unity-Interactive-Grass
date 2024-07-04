struct GrassOutput
{
    float3 position;
    float height;
    float width;
    float4 quaternion;
    float fade;
    float2 padding;
};

RWStructuredBuffer<GrassOutput> _OutputBuffer;

float4x4 quaternion_to_matrix(float4 quat, float3 _Position)
{
    float4x4 m = float4x4(float4(0, 0, 0, 0), float4(0, 0, 0, 0), float4(0, 0, 0, 0), float4(0, 0, 0, 0));
    // float4x4 m = float4x4(float4(0, 0, 0, 1), float4(0, 0, 0, 1), float4(0, 0, 0, 1), float4(0, 0, 0, 1));
            
    float x = quat.x, y = quat.y, z = quat.z, w = quat.w;
    float x2 = x + x, y2 = y + y, z2 = z + z;
    float xx = x * x2, xy = x * y2, xz = x * z2;
    float yy = y * y2, yz = y * z2, zz = z * z2;
    float wx = w * x2, wy = w * y2, wz = w * z2;

    m[0][0] = 1.0 - (yy + zz);
    m[0][1] = xy - wz;
    m[0][2] = xz + wy;

    m[1][0] = xy + wz;
    m[1][1] = 1.0 - (xx + zz);
    m[1][2] = yz - wx;

    m[2][0] = xz - wy;
    m[2][1] = yz + wx;
    m[2][2] = 1.0 - (xx + yy);

    m[0][3] = _Position.x;
    m[1][3] = _Position.y;
    m[2][3] = _Position.z;
    m[3][3] = 1.0;

    return m;
}

//get the data from the compute shader
void GetComputeData_float(float unity_InstanceID,
    out float3 _Position, out float4x4 _Matrix,
    out float _Height, out float _Width, out float _Fade)
{
    // 获取Compute Shader计算结果
    GrassOutput blade;
    blade = _OutputBuffer[unity_InstanceID];
    _Position = blade.position; // 先设置位置
    _Matrix = quaternion_to_matrix(blade.quaternion, _Position); // 设置最终转转矩阵
    _Height = blade.height; // 设置草高度
    _Width = blade.width; // 设置草宽度
    _Fade = blade.fade; // 设置明暗
}