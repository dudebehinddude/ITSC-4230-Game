Shader "UI/PlayerDarknessMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0, 0, 0, 1)
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _Aspect ("Screen Aspect", Float) = 1.78
        _Radius ("Clear Radius", Range(0, 1)) = 0.62
        _Softness ("Softness", Range(0.01, 1)) = 0.55
        _DarknessAlpha ("Darkness Alpha", Range(0, 1)) = 0
        _CenterAlpha ("Center Alpha", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float4 _Center;
            float _Aspect;
            float _Radius;
            float _Softness;
            float _DarknessAlpha;
            float _CenterAlpha;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 delta = input.uv - _Center.xy;
                delta.x *= _Aspect;

                float dist = length(delta);
                float mask = smoothstep(_Radius, _Radius + _Softness, dist);
                float alpha = lerp(_CenterAlpha, _DarknessAlpha, mask) * _Color.a;
                return fixed4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }
}
