Shader "Hidden/PixelLight"
{
    Properties
    {
        // Number of brightness steps. Lower = chunkier light bands.
        _Bands ("Light Bands", Range(2, 32)) = 6
        // >1 snaps sampling to a coarse grid so the light edges look blocky.
        // Leave at 1 to keep sprites perfectly crisp and only band brightness.
        _PixelSize ("Light Pixel Size", Range(1, 16)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "PixelLight"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Blit.hlsl"

            float _Bands;
            float _PixelSize;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // Optional: snap sampling to a coarse pixel grid for blocky light edges.
                if (_PixelSize > 1.0)
                {
                    float2 grid = _ScreenParams.xy / _PixelSize;
                    uv = (floor(uv * grid) + 0.5) / grid;
                }

                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // Posterize by luminance: scales rgb so brightness lands on a band,
                // preserving hue. Dark world + lights means this bands the lighting.
                float lum = dot(col.rgb, half3(0.299, 0.587, 0.114));
                float banded = floor(lum * _Bands) / _Bands;
                float scale = lum > 1e-4 ? banded / lum : 0.0;
                col.rgb *= scale;

                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
