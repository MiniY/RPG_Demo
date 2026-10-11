Shader "RPGDemo/BlackHolePortal/Body"
{
    Properties
    {
        _MainColor ("Deep Blue", Color) = (0.008, 0.025, 0.09, 1)
        _SecondaryColor ("Energy Blue", Color) = (0.018, 0.18, 0.68, 1)
        _HighlightColor ("Highlight", Color) = (0.15, 0.62, 1, 1)
        _PortalTime ("Portal Time", Float) = 0
        _FlowSpeed ("Flow Speed", Float) = 0.72
        _Distortion ("Distortion", Range(0, 1)) = 0.34
        _DetailDensity ("Detail Density", Range(0.25, 3)) = 1.15
        _DetailStrength ("Detail Strength", Range(0, 2)) = 1
        _PixelGrid ("Pixel Grid", Vector) = (172, 326, 0, 0)
        _CoreRatio ("Core Ratio", Vector) = (0.55, 0.70, 0, 0)
        _EdgeThickness ("Edge Thickness", Range(0.02, 0.30)) = 0.11
        _GlowStrength ("Glow Strength", Range(0, 1)) = 0.36
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
            fixed4 _HighlightColor;
            float _PortalTime;
            float _FlowSpeed;
            float _Distortion;
            float _DetailDensity;
            float _DetailStrength;
            float4 _PixelGrid;
            float4 _CoreRatio;
            float _EdgeThickness;
            float _GlowStrength;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float PeriodicNoise(float angle, float radius, float time)
            {
                float first = sin(angle * 3.0 + radius * 13.0 - time * 0.37);
                float second = sin(angle * 7.0 - radius * 19.0 + time * 0.23);
                float third = cos(angle * 11.0 + radius * 31.0 - time * 0.14);
                return first * 0.52 + second * 0.31 + third * 0.17;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 grid = max(_PixelGrid.xy, float2(8.0, 8.0));
                float2 uv = (floor(input.uv * grid) + 0.5) / grid;
                float2 portalPoint = (uv - 0.5) * 2.0;
                float radius = length(portalPoint);
                float angle = atan2(portalPoint.y, portalPoint.x);
                float time = _PortalTime * _FlowSpeed;

                float continuousNoise = PeriodicNoise(angle, radius, _PortalTime);
                float distortedRadius = radius + continuousNoise * 0.018 * _Distortion;
                float2 safeRatio = max(_CoreRatio.xy, float2(0.08, 0.08));
                float innerRadius = length(portalPoint / safeRatio);

                float outerMask = 1.0 - smoothstep(0.965, 1.025, distortedRadius);
                float innerMask = smoothstep(0.965, 1.045, innerRadius + continuousNoise * 0.012 * _Distortion);
                float bodyMask = saturate(outerMask * innerMask);

                float radiusSpeed = 1.0 + (radius - 0.55) * 0.28;
                float flowAngle = angle + time * radiusSpeed;
                float warp = PeriodicNoise(flowAngle, radius, _PortalTime * 0.41) * _Distortion;

                // Angular harmonic counts stay integral so atan2's -PI/PI wrap remains seamless.
                float broadPhase = flowAngle * 5.0
                    - radius * (22.0 * _DetailDensity)
                    + warp * 1.35;
                float broadBand = 0.5 + 0.5 * sin(broadPhase);
                broadBand = smoothstep(0.20, 0.90, broadBand);

                float finePhase = flowAngle * 13.0
                    - radius * (47.0 * _DetailDensity)
                    - warp * 1.8;
                float fineBand = pow(saturate(0.5 + 0.5 * sin(finePhase)), 7.0);

                float glintPhase = flowAngle * 19.0
                    + radius * 67.0
                    + sin(flowAngle * 4.0 - radius * 15.0);
                float glints = pow(saturate(0.5 + 0.5 * sin(glintPhase)), 18.0);
                glints *= smoothstep(0.02, 0.55, abs(continuousNoise));

                float guidePhase = flowAngle * 3.0
                    - radius * (14.0 * _DetailDensity)
                    + warp * 0.85;
                float guideBand = pow(saturate(0.5 + 0.5 * sin(guidePhase)), 10.0);

                float edgeWidth = max(0.015, _EdgeThickness);
                float outerRim = 1.0 - smoothstep(edgeWidth * 0.35, edgeWidth, abs(distortedRadius - 0.92));
                float innerRim = 1.0 - smoothstep(edgeWidth * 0.35, edgeWidth * 1.2, abs(innerRadius - 1.035));
                float rim = saturate(max(outerRim, innerRim));

                float depthShade = smoothstep(0.48, 1.0, radius);
                float energy = saturate((broadBand * 0.70 + fineBand * 0.48 + guideBand * 0.40 + rim * 0.30) * _DetailStrength);
                float highlight = saturate((fineBand * 0.34 + glints * 0.82 + guideBand * 0.24 + rim * 0.22) * _DetailStrength);

                float3 color = _MainColor.rgb;
                color = lerp(color, _SecondaryColor.rgb, energy * (0.58 + depthShade * 0.28));
                color = lerp(color, _HighlightColor.rgb, highlight * 0.62);

                float irregularAlpha = saturate(0.86 + broadBand * 0.10 + fineBand * 0.07);
                float bodyAlpha = bodyMask * irregularAlpha;

                float haloDistance = abs(distortedRadius - 0.985);
                float halo = (1.0 - smoothstep(0.025, 0.16, haloDistance)) * _GlowStrength;
                halo *= smoothstep(0.88, 1.04, innerRadius);
                float haloAlpha = halo * (0.055 + fineBand * 0.075);

                float alpha = saturate(bodyAlpha + haloAlpha);
                float3 haloColor = lerp(_SecondaryColor.rgb, _HighlightColor.rgb, fineBand * 0.36);
                color = lerp(color, haloColor, saturate(halo * 0.32));

                return fixed4(color, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
