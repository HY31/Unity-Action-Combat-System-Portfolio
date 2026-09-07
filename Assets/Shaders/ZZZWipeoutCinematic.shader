Shader "Hidden/ZZZ/WipeoutCinematic"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _ChromaticShift ("Chromatic Shift", Range(0, 0.02)) = 0.003
        _FailureBlend ("Failure Blend", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _ChromaticShift;
            float _FailureBlend;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 fromCenter = input.uv - 0.5;
                float2 splitDirection = normalize(fromCenter + float2(0.0001, 0.0001));
                float2 split = splitDirection * _ChromaticShift;

                fixed4 center = tex2D(_MainTex, input.uv);
                fixed redChannel = tex2D(_MainTex, input.uv + split).r;
                fixed blueChannel = tex2D(_MainTex, input.uv - split).b;
                fixed3 source = fixed3(redChannel, center.g, blueChannel);

                float luminance = dot(source, fixed3(0.299, 0.587, 0.114));
                luminance = floor(saturate(luminance) * 9.0) / 8.0;
                luminance = smoothstep(0.03, 0.94, luminance);

                fixed3 yellowShadow = fixed3(0.035, 0.03, 0.008);
                fixed3 yellowLight = fixed3(1.0, 0.83, 0.015);
                fixed3 failureShadow = fixed3(0.035, 0.005, 0.008);
                fixed3 failureLight = fixed3(1.0, 0.12, 0.025);
                fixed3 shadowColor = lerp(yellowShadow, failureShadow, _FailureBlend);
                fixed3 lightColor = lerp(yellowLight, failureLight, _FailureBlend);
                fixed3 posterized = lerp(shadowColor, lightColor, luminance);

                float edgeVignette = saturate(1.0 - dot(fromCenter, fromCenter) * 0.72);
                posterized *= lerp(0.82, 1.0, edgeVignette);

                float scanline = step(0.48, frac(input.uv.y * 540.0));
                posterized *= lerp(0.965, 1.0, scanline);
                return fixed4(saturate(posterized), center.a) * input.color;
            }
            ENDCG
        }
    }
}
