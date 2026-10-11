Shader "RPGDemo/BlackHolePortal/PreviewBackdrop"
{
    Properties
    {
        _ColorA ("Color A", Color) = (0.12, 0.20, 0.14, 1)
        _ColorB ("Color B", Color) = (0.17, 0.27, 0.18, 1)
        _GridColor ("Grid Color", Color) = (0.08, 0.13, 0.10, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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

            fixed4 _ColorA;
            fixed4 _ColorB;
            fixed4 _GridColor;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 cell = floor(input.uv * float2(28.0, 18.0));
                float checker = fmod(cell.x + cell.y, 2.0);
                float3 color = lerp(_ColorA.rgb, _ColorB.rgb, checker);

                float2 localCell = frac(input.uv * float2(28.0, 18.0));
                float gridLine = step(localCell.x, 0.035) + step(localCell.y, 0.055);
                color = lerp(color, _GridColor.rgb, saturate(gridLine) * 0.34);
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
