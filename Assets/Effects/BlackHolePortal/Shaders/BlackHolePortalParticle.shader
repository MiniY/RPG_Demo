Shader "RPGDemo/BlackHolePortal/Particle"
{
    Properties
    {
        _Intensity ("Intensity", Range(0.25, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+10"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            float _Intensity;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 pixelUv = (floor(input.uv * 7.0) + 0.5) / 7.0;
                float2 particlePoint = abs(pixelUv * 2.0 - 1.0);
                float diamond = particlePoint.x + particlePoint.y * 0.82;
                float mask = 1.0 - smoothstep(0.68, 1.03, diamond);
                mask *= 1.0 - smoothstep(0.72, 1.0, max(particlePoint.x, particlePoint.y));
                return fixed4(input.color.rgb * _Intensity, input.color.a * mask);
            }
            ENDCG
        }
    }

    Fallback Off
}
