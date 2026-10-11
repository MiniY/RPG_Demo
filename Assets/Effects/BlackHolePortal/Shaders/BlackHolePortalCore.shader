Shader "RPGDemo/BlackHolePortal/Core"
{
    Properties
    {
        _MainColor ("Core Black", Color) = (0.001, 0.003, 0.012, 1)
        _SecondaryColor ("Dark Flow Blue", Color) = (0.025, 0.09, 0.24, 1)
        _PortalTime ("Portal Time", Float) = 0
        _FlowSpeed ("Flow Speed", Float) = -0.48
        _Distortion ("Distortion", Range(0, 1)) = 0.24
        _DetailDensity ("Detail Density", Range(0.25, 3)) = 1
        _DetailStrength ("Detail Strength", Range(0, 2)) = 1
        _DarkFlowBrightness ("Dark Flow Brightness", Range(0, 1)) = 0.32
        _PixelGrid ("Pixel Grid", Vector) = (94, 228, 0, 0)
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

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _MainColor;
            fixed4 _SecondaryColor;
            float _PortalTime;
            float _FlowSpeed;
            float _Distortion;
            float _DetailDensity;
            float _DetailStrength;
            float _DarkFlowBrightness;
            float4 _PixelGrid;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float PeriodicNoise(float angle, float radius, float time)
            {
                return sin(angle * 4.0 + radius * 17.0 - time * 0.31) * 0.58
                    + cos(angle * 9.0 - radius * 29.0 + time * 0.19) * 0.29
                    + sin(angle * 14.0 + radius * 43.0) * 0.13;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 grid = max(_PixelGrid.xy, float2(8.0, 8.0));
                float2 uv = (floor(input.uv * grid) + 0.5) / grid;
                float2 portalPoint = (uv - 0.5) * 2.0;
                float radius = length(portalPoint);
                float angle = atan2(portalPoint.y, portalPoint.x);
                float time = _PortalTime * _FlowSpeed;

                float noise = PeriodicNoise(angle, radius, _PortalTime * 0.43);
                float distortedRadius = radius + noise * 0.016 * _Distortion;
                float mask = 1.0 - smoothstep(0.955, 1.01, distortedRadius);

                float radiusSpeed = 1.0 + radius * 0.30;
                float flowAngle = angle + time * radiusSpeed;
                float warp = PeriodicNoise(flowAngle, radius, _PortalTime * 0.29) * _Distortion;

                // Integral angular harmonics preserve continuity across atan2's wrap.
                float broadPhase = flowAngle * 4.0
                    - radius * (19.0 * _DetailDensity)
                    + warp * 1.6;
                float broad = pow(saturate(0.5 + 0.5 * sin(broadPhase)), 5.0);

                float finePhase = flowAngle * 9.0
                    - radius * (38.0 * _DetailDensity)
                    - warp * 1.2;
                float fine = pow(saturate(0.5 + 0.5 * sin(finePhase)), 10.0);

                float guidePhase = flowAngle * 3.0
                    - radius * (15.0 * _DetailDensity)
                    + warp * 0.70;
                float guide = pow(saturate(0.5 + 0.5 * sin(guidePhase)), 8.0);

                float edgeBias = smoothstep(0.22, 0.96, radius);
                float flow = saturate((broad * 0.48 + fine * 0.36 + guide * 0.62) * edgeBias * _DetailStrength);
                flow *= _DarkFlowBrightness;

                float3 color = _MainColor.rgb;
                color += _SecondaryColor.rgb * flow;
                color += _SecondaryColor.rgb * fine * edgeBias * _DarkFlowBrightness * 0.20;

                // The core remains opaque over most of its area so the world cannot show through.
                float centerOpacity = 1.0 - smoothstep(0.15, 0.70, radius);
                float alpha = mask * lerp(0.985, 1.0, centerOpacity);
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
