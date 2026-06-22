Shader "Custom/TwoColorRope"
{
    Properties
    {
        _Color1 ("Color 1 (Start)", Color) = (1,1,1,1)
        _Color2 ("Color 2 (End)", Color) = (1,1,1,1)
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5

        [Header(Color Adjustment)]
        _Saturation ("Saturation", Range(0, 3)) = 1.1
        _Brightness ("Brightness", Range(0, 3)) = 1.0

        [Header(Luna Correction)]
        [Toggle(LUNA_GAMMA_CORRECTION)] _LunaGamma ("Luna Gamma Correction", Float) = 1.0
        _LunaContrast ("Luna Gamma Contrast", Range(0, 3)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 100
        Cull Off
        Lighting Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ LUNA_GAMMA_CORRECTION

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MaskTex;
            float4 _MaskTex_ST;
            float _Cutoff;
            float _Saturation;
            float _Brightness;
            float _LunaContrast;
            float _LunaGamma;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color1)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color2)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MaskTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // Sample mask texture for cutout shape
                fixed4 maskCol = tex2D(_MaskTex, i.uv);
                clip(maskCol.a - _Cutoff);

                // Blend Color1 (start) → Color2 (end) using U coordinate (along rope length)
                fixed4 c1 = UNITY_ACCESS_INSTANCED_PROP(Props, _Color1);
                fixed4 c2 = UNITY_ACCESS_INSTANCED_PROP(Props, _Color2);
                fixed4 finalColor = lerp(c1, c2, i.uv.y);

                // Apply mask shading (AO/highlight from texture)
                finalColor.rgb *= maskCol.rgb;

                // Color Adjustment — same as LauncherUnlit & CubeUnlit
                float luminance = dot(finalColor.rgb, float3(0.2126, 0.7152, 0.0722));
                finalColor.rgb = lerp(float3(luminance, luminance, luminance), finalColor.rgb, _Saturation) * _Brightness;

                // Luna Gamma Correction — identical to LauncherUnlit/CubeUnlit pattern
                                if (_LunaGamma > 0.5)
                {
                    #if !defined(UNITY_COLORSPACE_GAMMA)
                    finalColor.rgb = LinearToGammaSpace(finalColor.rgb);
                    finalColor.rgb = ((finalColor.rgb - 0.5) * _LunaContrast) + 0.5;
                    #endif
                }

                return fixed4(finalColor.rgb, 1.0);
            }
            ENDCG
        }
    }
}
