// Ghostly "shadow clone" look for the Shadow Storm: a dark translucent body with a bright fresnel rim.
// A depth-only pre-pass keeps the character's overlapping parts from showing through each other.
Shader "Proto/ShadowClone"
{
    Properties
    {
        _Color ("Body Color", Color) = (0.12, 0.04, 0.22, 0.7)
        _RimColor ("Rim Color", Color) = (0.75, 0.35, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.2
        _RimStrength ("Rim Strength", Range(0, 4)) = 1.8
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            ZWrite On
            ColorMask 0
        }

        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half _Fade;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half rim = pow(1.0 - saturate(dot(normalize(i.normal), normalize(i.viewDir))), _RimPower) * _RimStrength;
                fixed3 col = _Color.rgb + _RimColor.rgb * rim;
                half alpha = saturate(_Color.a + rim * _RimColor.a * 0.5) * _Fade;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
