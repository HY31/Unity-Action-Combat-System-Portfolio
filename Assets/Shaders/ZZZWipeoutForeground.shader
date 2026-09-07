Shader "Hidden/ZZZ/WipeoutForeground"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Contrast ("Contrast", Range(0.5, 2.0)) = 1.15
        _PosterizeSteps ("Posterize Steps", Range(2, 16)) = 8
        _GrayFloor ("Gray Floor", Range(0, 0.5)) = 0.16
        _GrayCeiling ("Gray Ceiling", Range(0.5, 1)) = 0.94
        _Opacity ("Opacity", Range(0, 1)) = 0.85
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
            float _Contrast;
            float _PosterizeSteps;
            float _GrayFloor;
            float _GrayCeiling;
            float _Opacity;

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
                fixed4 source = tex2D(_MainTex, input.uv);
                float luminance = dot(source.rgb, fixed3(0.299, 0.587, 0.114));
                luminance = saturate((luminance - 0.5) * _Contrast + 0.5);
                luminance = smoothstep(0.035, 0.965, luminance);

                float steps = max(2.0, _PosterizeSteps);
                luminance = floor(luminance * steps + 0.5) / steps;
                luminance = lerp(_GrayFloor, _GrayCeiling, luminance);

                fixed3 monochrome = luminance.xxx;
                fixed3 tinted = monochrome * input.color.rgb;
                return fixed4(tinted, source.a * input.color.a * _Opacity);
            }
            ENDCG
        }
    }
}
