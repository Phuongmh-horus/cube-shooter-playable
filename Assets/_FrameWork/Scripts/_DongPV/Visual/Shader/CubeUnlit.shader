Shader "Horus/Unlit/CubeUnlit"
{
    Properties
    {
        [Enum(Front, 2, Back, 1, Both, 0)] _Cull ("Render Face", Float) = 2.0
        _ZWrite ("Depth Write", Float) = 1.0
        _SrcBlend ("Blending Source", Float) = 1.0
        _DstBlend ("Blending Destination", Float) = 0.0

        [Header(Toony Colors Pro 2 Base)]
        _BaseColor ("Color", Color) = (1,1,1,1)
        _BaseMap ("Albedo", 2D) = "white" {}
        _HColor ("Highlight Color", Color) = (1,1,1,1)
        _SColor ("Shadow Color", Color) = (0.2,0.2,0.2,1)
        _ShadowColorLightAtten ("Main Light affects Shadow Color", Float) = 1

        [Header(Toony Colors Pro 2 Ramp Shading)]
        _RampThreshold ("Threshold", Range(0.01,1)) = 0.55
        _RampSmoothing ("Smoothing", Range(0,1)) = 0.2

        [Header(Toony Colors Pro 2 Normal Mapping)]
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Scale", Range(-1,1)) = 0.7

        [Header(Toony Colors Pro 2 Specular)]
        _SpecularColor ("Specular Color", Color) = (0.35,0.35,0.35,1)
        _SpecularToonSize ("Size", Range(0.001,1)) = 0.1
        _SpecularToonSmoothness ("Smoothing", Range(0,1)) = 1.0

        [Header(Fake Lighting)]
        _FakeLightDir ("Fake Light Direction (XYZ=Dir, W=Intensity)", Vector) = (0.4, 1.0, 0.6, 1.5)

        [Header(Color Adjustment)]
        _Saturation ("Saturation", Range(0, 3)) = 1.0
        _Brightness ("Brightness", Range(0, 3)) = 1.0

        [Header(Luna Correction)]
        [Toggle(LUNA_GAMMA_CORRECTION)] _LunaGamma ("Luna Gamma Correction", Float) = 1.0
        _LunaContrast ("Luna Gamma Contrast", Range(0, 3)) = 1.2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Cull [_Cull]

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ LUNA_GAMMA_CORRECTION

            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            sampler2D _BumpMap;
            
            float4 _BaseMap_ST;
            float4 _BumpMap_ST;

            float _BumpScale;
            float _RampThreshold;
            float _RampSmoothing;
            float _ShadowColorLightAtten;
            float4 _FakeLightDir;

            float4 _HColor;
            float4 _SColor;
            float4 _SpecularColor;
            float _Saturation;
            float _Brightness;
            float _LunaContrast;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _SpecularToonSize)
                UNITY_DEFINE_INSTANCED_PROP(float, _SpecularToonSmoothness)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldTangent : TEXCOORD3;
                float3 worldBinormal : TEXCOORD4;
                float3 worldViewDir : TEXCOORD5;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                float tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                o.worldBinormal = cross(o.worldNormal, o.worldTangent) * tangentSign;
                o.worldViewDir = UnityWorldSpaceViewDir(worldPos);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                // Color and Albedo
                fixed4 colorToon = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                fixed4 albedo = tex2D(_BaseMap, i.uv) * colorToon;
                
                float3 worldNormal = normalize(i.worldNormal);
                
                // Normal Mapping
                float tangentSq = dot(i.worldTangent, i.worldTangent);
                float binormalSq = dot(i.worldBinormal, i.worldBinormal);
                
                #if defined(SHADER_API_METAL) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN)
                if (tangentSq > 0.0001 && binormalSq > 0.0001)
                #else
                if (tangentSq > 0.00001 && binormalSq > 0.00001)
                #endif
                {
                    float3 tangentNormal = UnpackNormal(tex2D(_BumpMap, i.uv));
                    tangentNormal.xy *= _BumpScale;
                    tangentNormal.z = sqrt(1.0 - saturate(dot(tangentNormal.xy, tangentNormal.xy)));

                    float3x3 TBN = float3x3(i.worldTangent * rsqrt(tangentSq), i.worldBinormal * rsqrt(binormalSq), worldNormal);
                    worldNormal = normalize(mul(tangentNormal, TBN));
                }
                
                // Static Fake Lighting Setup
                float3 lightDir = normalize(_FakeLightDir.xyz);
                float3 lightColor = float3(1, 1, 1) * _FakeLightDir.w;
                
                // --- TOON SHADING ---
                float NdotL = dot(worldNormal, lightDir);
                float ndlWrapped = NdotL * 0.5 + 0.5;
                
                float rampSmooth = _RampSmoothing * 0.5;
                float3 ramp = smoothstep(_RampThreshold - rampSmooth, _RampThreshold + rampSmooth, ndlWrapped).xxx;
                
                float3 highlightColor = _HColor.rgb;
                float3 shadowColor = _SColor.rgb;

                if (_ShadowColorLightAtten < 0.5)
                {
                    highlightColor *= lightColor;
                }
                
                float3 toonRamp = lerp(shadowColor, highlightColor, ramp);
                if (_ShadowColorLightAtten >= 0.5)
                {
                    toonRamp *= lightColor;
                }
                
                float3 litColor = albedo.rgb * toonRamp;

                // --- STYLIZED SPECULAR ---
                float3 viewDir = normalize(i.worldViewDir);
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(worldNormal, halfDir));
                float specSize = clamp(1.0 - UNITY_ACCESS_INSTANCED_PROP(Props, _SpecularToonSize), 0.0, 0.999);
                float nh = saturate(NdotH * (1.0 / (1.0 - specSize)) - (specSize / (1.0 - specSize)));
                float spec = smoothstep(0.0, max(0.0001, UNITY_ACCESS_INSTANCED_PROP(Props, _SpecularToonSmoothness)), nh);
                float3 specColor = _SpecularColor.rgb;
                litColor += spec * specColor * lightColor;

                // Color Adjustment
                float saturation = _Saturation;
                float brightness = _Brightness;
                float luminance = dot(litColor, float3(0.2126, 0.7152, 0.0722));
                litColor = lerp(float3(luminance, luminance, luminance), litColor, saturation) * brightness;

                #if defined(LUNA_GAMMA_CORRECTION)
                #if !defined(UNITY_COLORSPACE_GAMMA)
                litColor = LinearToGammaSpace(litColor);
                litColor = ((litColor - 0.5) * 1.2) + 0.5;
                #endif
                #endif

                return fixed4(litColor, albedo.a);
            }
            ENDCG
        }
    }

    FallBack "Mobile/Diffuse"
}
