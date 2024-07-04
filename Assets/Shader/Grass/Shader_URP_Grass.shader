Shader "Custom/URPGrassBlades"
{
    Properties
    {
        [MainColor] _BaseColor("BaseColor", Color) = (1,1,1,1)
        _BottomColor("_BottomColor", Color) = (1,1,1,1)
        _TopColor("_TopColor", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(1, 100)) = 2
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _Fade ("Fade", Range(0, 1)) = 1.0
        _AABB ("_AABB", Vector) = (0,0,0,0)
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Cull Off
            ZTest Less
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma instancing_options
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fog
            // -------------------------------------
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct GrassOutput
            {
                float3 position;
                float height;
                float width;
                float4 quaternion;
                float fade;
                float2 padding;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
                float4 _BottomColor;
                float4 _TopColor;
                StructuredBuffer<GrassOutput> _OutputBuffer;
                float4 _AABB;
            CBUFFER_END

            sampler2D _MainTex;
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                half3 color         : COLOR;
                
            };

            float4x4 quaternion_to_matrix(float4 quat, float3 pos)
            {
                float4x4 m = float4x4(float4(0, 0, 0, 0), float4(0, 0, 0, 0), float4(0, 0, 0, 0), float4(0, 0, 0, 0));
                
                float x = quat.x, y = quat.y, z = quat.z, w = quat.w;
                float x2 = x + x, y2 = y + y, z2 = z + z;
                float xx = x * x2, xy = x * y2, xz = x * z2;
                float yy = y * y2, yz = y * z2, zz = z * z2;
                float wx = w * x2, wy = w * y2, wz = w * z2;

                m[0][0] = 1.0 - (yy + zz); m[0][1] = xy - wz; m[0][2] = xz + wy;
                m[1][0] = xy + wz; m[1][1] = 1.0 - (xx + zz); m[1][2] = yz - wx;
                m[2][0] = xz - wy; m[2][1] = yz + wx; m[2][2] = 1.0 - (xx + yy);
                m[0][3] = pos.x; m[1][3] = pos.y; m[2][3] = pos.z;
                m[3][3] = 1.0;
                return m;
            }

            float3x3 transpose(float4x4 m)
            {
                return float3x3(
                    float3(m[0][0], m[1][0], m[2][0]), // Column 1
                    float3(m[0][1], m[1][1], m[2][1]), // Column 2
                    float3(m[0][2], m[1][2], m[2][2])  // Column 3
                );
            }

            half3 ApplySingleDirectLight(Light light, half3 N, half3 V, half3 albedo, half positionOSY)
            {
                half3 H = normalize(light.direction + V);

                //direct diffuse 
                half directDiffuse = dot(N, light.direction) * 0.5 + 0.5; //half lambert, to fake grass SSS

                //direct specular
                float directSpecular = saturate(dot(N,H));
                //pow(directSpecular,8)
                directSpecular *= directSpecular;
                directSpecular *= directSpecular;
                directSpecular *= directSpecular;
                //directSpecular *= directSpecular; //enable this line = change to pow(directSpecular,16)

                //add direct directSpecular to result
                directSpecular *= 0.1 * positionOSY;//only apply directSpecular to grass's top area, to simulate grass AO

                half3 lighting = light.color * (light.shadowAttenuation * light.distanceAttenuation);
                half3 result = (albedo * directDiffuse + directSpecular) * lighting;
                return result; 
            }

            
            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                GrassOutput blade = _OutputBuffer[instanceID];
                float3 bladePosWS = blade.position; // 先设置位置
                float4x4 _Matrix = quaternion_to_matrix(blade.quaternion, bladePosWS); // 设置最终转转矩阵
                float bladeHeight = blade.height; // 设置草高度
                float bladeWidth = blade.width; // 设置草宽度
                float bladeFade = blade.fade; // 设置明暗
                
                float4 positionOS = IN.positionOS;
                positionOS.x *= bladeWidth;
                positionOS.y *= bladeHeight;
                positionOS = mul(_Matrix,positionOS);
                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                positionWS -=  _AABB.xyz;
            
                Varyings OUT;
                
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                
                //lighting data
                Light mainLight;
                #if _MAIN_LIGHT_SHADOWS
                    mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
                #else
                    mainLight = GetMainLight();
                #endif
                
                half3 albedo = lerp(_BottomColor.rgb, _TopColor.rgb, IN.positionOS.y);
                
                half3 N = normalize(mul((float3x3)transpose(_Matrix), half3(0,1,0)));

                float3 viewWS = _WorldSpaceCameraPos - bladePosWS;
                float ViewWSLength = length(viewWS);
                half3 V = viewWS / ViewWSLength;
                
                // 间接光
                half3 lightingResult = SampleSH(0);
                lightingResult += ApplySingleDirectLight(mainLight, N, V, albedo, positionOS.y);
                #if _ADDITIONAL_LIGHTS
                    int additionalLightsCount = GetAdditionalLightsCount();
                    for (int i = 0; i < additionalLightsCount; ++i)
                    {
                        Light light = GetAdditionalLight(i, positionWS);
                        lightingResult += ApplySingleDirectLight(light, N, V, albedo, positionOS.y);
                    }
                #endif
                lightingResult *= albedo * tex2Dlod(_MainTex, float4(TRANSFORM_TEX(positionWS.xz,_MainTex),0,0)).rgb * _BaseColor.rgb;
                
                OUT.color = lightingResult;
                return OUT;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                return half4(IN.color,1);
            }
            ENDHLSL
        }

        // Shadow Pass
        Pass {
            Name "ShadowCaster"
            Tags{ "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma instancing_options
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fog
            // -------------------------------------
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct GrassOutput
            {
                float3 position;
                float height;
                float width;
                float4 quaternion;
                float fade;
                float2 padding;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
                float4 _BottomColor;
                float4 _TopColor;
                StructuredBuffer<GrassOutput> _OutputBuffer;
                float4 _AABB;
            CBUFFER_END

            sampler2D _MainTex;
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
            };

            float4x4 quaternion_to_matrix(float4 quat, float3 pos)
            {
                float4x4 m = float4x4(float4(0, 0, 0, 0), float4(0, 0, 0, 0), float4(0, 0, 0, 0), float4(0, 0, 0, 0));
                
                float x = quat.x, y = quat.y, z = quat.z, w = quat.w;
                float x2 = x + x, y2 = y + y, z2 = z + z;
                float xx = x * x2, xy = x * y2, xz = x * z2;
                float yy = y * y2, yz = y * z2, zz = z * z2;
                float wx = w * x2, wy = w * y2, wz = w * z2;

                m[0][0] = 1.0 - (yy + zz); m[0][1] = xy - wz; m[0][2] = xz + wy;
                m[1][0] = xy + wz; m[1][1] = 1.0 - (xx + zz); m[1][2] = yz - wx;
                m[2][0] = xz - wy; m[2][1] = yz + wx; m[2][2] = 1.0 - (xx + yy);
                m[0][3] = pos.x; m[1][3] = pos.y; m[2][3] = pos.z;
                m[3][3] = 1.0;
                return m;
            }
            
            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                GrassOutput blade = _OutputBuffer[instanceID];
                float3 bladePosWS = blade.position; // 先设置位置
                float4x4 _Matrix = quaternion_to_matrix(blade.quaternion, bladePosWS); // 设置最终转转矩阵
                float bladeHeight = blade.height; // 设置草高度
                float bladeWidth = blade.width; // 设置草宽度
                float bladeFade = blade.fade; // 设置明暗
                
                float4 positionOS = IN.positionOS;
                positionOS.x *= bladeWidth;
                positionOS.y *= bladeHeight;
                positionOS = mul(_Matrix,positionOS);
                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                positionWS -=  _AABB.xyz;
            
                Varyings OUT;
                
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                return OUT;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                return half4(1,1,1,1);
            }
            ENDHLSL
        }
    }
}
