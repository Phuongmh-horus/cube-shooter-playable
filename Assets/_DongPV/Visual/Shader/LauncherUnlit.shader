Shader "Horus/Unlit/LauncherUnlit"
{
    Properties
    {
        _ZWrite ("Depth Write", Float) = 1.0
        _SrcBlend ("Blending Source", Float) = 1.0
        _DstBlend ("Blending Destination", Float) = 0.0

        [Header(Base Material)]
        _BaseColor ("Color", Color) = (1,1,1,1)
        _BaseMap ("Albedo", 2D) = "white" {}
        [Header(Shadow)]
        _ShadowColor ("Shadow Color", Color) = (0.2, 0.2, 0.2, 1)
        _ShadowLightingIntensity ("Shadow Lighting Intensity", Range(0, 5)) = 1.0

        [Header(Specular)]
        [Toggle(_SPECULAR_ON)] _UseSpecular ("Enable Specular", Float) = 1
        [HDR] _SpecularColor ("Specular Color", Color) = (0.35,0.35,0.35,1)
        _SpecularLightingIntensity ("Specular Lighting Intensity", Range(0, 5)) = 1.0
        _SpecularToonSize ("Size", Range(0.001,1)) = 0.1
        _SpecularToonSmoothness ("Smoothing", Range(0,1)) = 1.0

        [Header(Normal Mapping)]
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Scale", Range(-1,1)) = 0.7

        [Header(MatCap Reflection)]
        [Toggle(_MATCAP_ON)] _UseMatCap ("Enable MatCap", Float) = 0
        _MatCapTex ("MatCap Texture", 2D) = "black" {}
        _MatCapColor ("MatCap Color", Color) = (1,1,1,1)
        _MatCapIntensity ("MatCap Intensity", Range(0, 5)) = 1.0

        [Header(Dissolve Effect)]
        _DissolveMap ("Dissolve Map (Noise)", 2D) = "white" {}
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0.0
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0, 0.5)) = 0.05
        [HDR] _DissolveEdgeColor ("Dissolve Edge Color", Color) = (1, 0.3, 0, 1)

        [Header(Color Adjustment)]
        _ColorBrightness ("Brightness", Range(0, 2)) = 1.0
        _ColorContrast ("Contrast", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Cull Back

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma instancing_options maxcount:256
            #pragma multi_compile _ USE_CONSTANT_BUFFER
            #pragma multi_compile_fwdbase

            // Shader features
            #pragma shader_feature_local _SPECULAR_ON
            #pragma shader_feature_local _MATCAP_ON

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            #define fixed4 half4
            #define fixed3 half3
            #define fixed half

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            sampler2D _BumpMap;
            float4 _BumpMap_ST;
            sampler2D _MatCapTex;
            float4 _MatCapTex_ST;

            sampler2D _DissolveMap;
            float4 _DissolveMap_ST;

            // Global (non-instanced) properties
            float4 _ShadowColor;
            float _ShadowLightingIntensity;
            float _BumpScale;
            float _UseSpecular;
            float4 _SpecularColor;
            float _SpecularLightingIntensity;
            float _UseMatCap;
            float4 _MatCapColor;
            float _MatCapIntensity;
            float _DissolveEdgeWidth;
            float4 _DissolveEdgeColor;
            float _ColorBrightness;
            float _ColorContrast;


            // Instanced properties (only what C# updates at runtime)
            UNITY_INSTANCING_BUFFER_START(LauncherProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _SpecularToonSize)
                UNITY_DEFINE_INSTANCED_PROP(float, _SpecularToonSmoothness)
                UNITY_DEFINE_INSTANCED_PROP(float, _DissolveAmount)
            UNITY_INSTANCING_BUFFER_END(LauncherProps)

            struct v2f
            {
                float2 uv           : TEXCOORD0;
                float2 uvDissolve   : TEXCOORD6;
                float3 normalWS     : TEXCOORD1;
                float3 tangentWS    : TEXCOORD3;
                float3 binormalWS   : TEXCOORD4;
                float3 viewDirWS    : TEXCOORD5;
                float3 posWS        : TEXCOORD8;
                SHADOW_COORDS(7)
                float4 pos          : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

#ifdef USE_CONSTANT_BUFFER
                v.vertex = skinning(v);
#endif

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _BaseMap);
                o.uvDissolve = TRANSFORM_TEX(v.texcoord, _DissolveMap);

                o.posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                o.tangentWS = UnityObjectToWorldDir(v.tangent.xyz);
                float tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                o.binormalWS = cross(o.normalWS, o.tangentWS) * tangentSign;
                o.viewDirWS = UnityWorldSpaceViewDir(o.posWS);

                TRANSFER_SHADOW(o);

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                // --- DISSOLVE EFFECT ---
                float dissolveAmount = saturate(UNITY_ACCESS_INSTANCED_PROP(LauncherProps, _DissolveAmount)) * 0.8;
                float dissolveEdgeWidth = _DissolveEdgeWidth;
                float4 dissolveEdgeColor = _DissolveEdgeColor;

                float4 dissolveNoise = tex2D(_DissolveMap, i.uvDissolve);
                float noiseVal = dissolveNoise.r;
                float clipVal = noiseVal - dissolveAmount;
                clip(clipVal);
                
                // --- BASE COLORING ---
                half4 albedo = tex2D(_BaseMap, i.uv) * UNITY_ACCESS_INSTANCED_PROP(LauncherProps, _BaseColor);
                
                float3 worldNormal = normalize(i.normalWS);
                
                // Normal Mapping
                float tangentSq = dot(i.tangentWS, i.tangentWS);
                float binormalSq = dot(i.binormalWS, i.binormalWS);
                half3 normalTS = half3(0, 0, 1);
                
                float bumpScale = _BumpScale;
                if (tangentSq > 0.00001 && binormalSq > 0.00001)
                {
                    normalTS = UnpackNormal(tex2D(_BumpMap, i.uv));
                    normalTS.xy *= bumpScale;
                    normalTS.z = sqrt(1.0 - saturate(dot(normalTS.xy, normalTS.xy)));
                }

                float3 tangent = i.tangentWS * rsqrt(tangentSq);
                float3 binormal = i.binormalWS * rsqrt(binormalSq);
                half3x3 tangentToWorld = half3x3(tangent, binormal, worldNormal);
                float3 normalWS = normalize(mul(normalTS, tangentToWorld));

                // 2. Directions
                float3 viewDir = normalize(i.viewDirWS);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                
                // Shadows
                UNITY_LIGHT_ATTENUATION(shadowAtten, i, i.posWS);
                
                // 3. Diffuse Shading (Lambert + Ambient with Custom Shadow Color & Intensity)
                float NdotL = saturate(dot(normalWS, lightDir));
                float lightFactor = NdotL * shadowAtten;
                
                half3 mainLightColor = _LightColor0.rgb;
                half3 clampedLightColor = min(mainLightColor, 1.0);
                
                // Shadow Color and Intensity
                half4 shadowColor = _ShadowColor;
                float shadowLightingIntensity = _ShadowLightingIntensity;

                // Ambient lighting
                half3 ambient = ShadeSH9(half4(normalWS, 1.0));
                
                // Blend shadowed side and lit side
                half3 diffuseLight = lerp(shadowColor.rgb * shadowLightingIntensity, clampedLightColor, lightFactor) + ambient;
                half3 litColor = albedo.rgb * diffuseLight;

                // 4. Specular Highlight (Stylized Toon Specular)
                #if defined(_SPECULAR_ON)
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normalWS, halfDir));
                
                float specSize = clamp(1.0 - UNITY_ACCESS_INSTANCED_PROP(LauncherProps, _SpecularToonSize), 0.0, 0.999);
                float nh = saturate(NdotH * (1.0 / (1.0 - specSize)) - (specSize / (1.0 - specSize)));
                float spec = smoothstep(0.0, max(0.0001, UNITY_ACCESS_INSTANCED_PROP(LauncherProps, _SpecularToonSmoothness)), nh);
                
                half3 specularHighlight = spec * _SpecularColor.rgb * shadowAtten * clampedLightColor * _SpecularLightingIntensity;
                litColor += specularHighlight;
                #endif

                // 5. MatCap Reflection (Wrapped in feature toggle)
                #if defined(_MATCAP_ON)
                half4 matcapColor = _MatCapColor;
                float matcapIntensity = _MatCapIntensity;

                float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, normalWS);
                float2 matcapUV = viewNormal.xy * 0.5 + 0.5;
                half3 matcap = tex2D(_MatCapTex, matcapUV).rgb * matcapColor.rgb * matcapIntensity;
                litColor += matcap * albedo.rgb;
                #endif

                // Compute edge glow
                float edge = 1.0 - smoothstep(0.0, dissolveEdgeWidth, clipVal);
                edge *= step(0.001, dissolveAmount); // Only glow when dissolve is active
                float3 edgeGlow = dissolveEdgeColor.rgb * edge * dissolveEdgeColor.a;
                litColor += edgeGlow;

                // 6. Color Adjustment (Brightness and Contrast)
                litColor = litColor * _ColorBrightness;
                litColor = lerp(half3(0.5, 0.5, 0.5), litColor, _ColorContrast);

                return half4(litColor, albedo.a);
            }
            ENDCG
        }

        // Support standard shadow casting pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ USE_CONSTANT_BUFFER
            #pragma multi_compile_shadowcaster

            #include "UnityCG.cginc"
            
            struct v2f
            {
                V2F_SHADOW_CASTER;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

#ifdef USE_CONSTANT_BUFFER
                v.vertex = skinningShadow(v);
#endif

                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
