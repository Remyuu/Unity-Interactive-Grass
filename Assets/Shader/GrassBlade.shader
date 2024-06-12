Shader "Custom/GrassBlades"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _BottomColor ("草根色", Color) = (1,1,1,1)
        _TopColor ("草尖色", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _AABB ("AABB", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags{ "RenderType"="Opaque" }
        
		LOD 200
		Cull Off
		
        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types   
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows
        #pragma instancing_options procedural:setup
        
        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        float _Height;
        float _Width;

        fixed4 _Color;
        fixed4 _BottomColor;
        fixed4 _TopColor;
        float _Fade;

        fixed4 _AABB;
        
        half _Glossiness;
        half _Metallic;

        float4x4 _Matrix;
        float3 _Position;

        struct GrassOutput
        {
            float3 position;
            float height;
            float width;
            float4 quaternion;
            float fade;
            float2 padding;
        };
        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
        StructuredBuffer<GrassOutput> _OutputBuffer;
        #endif
        
        
        float4x4 quaternion_to_matrix(float4 quat)
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

        float4x4 create_matrix(float3 pos, float theta){
            float c = cos(theta);
            float s = sin(theta);
            return float4x4(
                c,-s, 0, pos.x,
                s, c, 0, pos.y,
                0, 0, 1, pos.z,
                0, 0, 0, 1
            );
        }
        float3x3 transpose(float4x4 m)
        {
            return float3x3(
                float3(m[0][0], m[1][0], m[2][0]), // Column 1
                float3(m[0][1], m[1][1], m[2][1]), // Column 2
                float3(m[0][2], m[1][2], m[2][2])  // Column 3
            );
        }

        float4x4 AngleAxis4x4(float3 pos, float angle, float3 axis){
            float c, s;
            sincos(angle*2*3.14, s, c);

            float t = 1 - c;
            float x = axis.x;
            float y = axis.y;
            float z = axis.z;

            return float4x4(
                t * x * x + c    , t * x * y - s * z, t * x * z + s * y, pos.x,
                t * x * y + s * z, t * y * y + c    , t * y * z - s * x, pos.y,
                t * x * z - s * y, t * y * z + s * x, t * z * z + c    , pos.z,
                0,0,0,1
                );
        }
        

        void vert(inout appdata_full v, out Input data)
        {
            UNITY_INITIALIZE_OUTPUT(Input, data);
            // 应用模型顶点变换
            v.vertex.y = v.vertex.y * _Height;
            v.vertex.x = v.vertex.x * _Width;
            v.vertex = mul(_Matrix, v.vertex);
            v.vertex.xyz -= _AABB.xyz;
            // 计算逆转置矩阵用于法线变换
            v.normal = mul((float3x3)transpose(_Matrix), v.normal);
        }

        void setup()
        {
                // 获取Compute Shader计算结果
            GrassOutput blade;
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                blade = _OutputBuffer[unity_InstanceID];
                #endif
            _Position = blade.position; // 先设置位置
            _Matrix = quaternion_to_matrix(blade.quaternion); // 设置最终转转矩阵
            _Height = blade.height; // 设置草高度
            _Width = blade.width; // 设置草宽度
            _Fade = blade.fade; // 设置明暗
        }


        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color * _Fade * lerp(_BottomColor, _TopColor, IN.uv_MainTex.y);
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}