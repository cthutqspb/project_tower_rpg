Shader "Custom/OutlineComposite"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.6, 0, 1)
        _OutlineThickness ("Thickness", Range(1, 10)) = 2.0
    }
    SubShader
    {
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        ENDHLSL

        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        ZWrite Off 
        Cull Off

        Pass
        {
            Name "OutlineComposite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 _OutlineColor;
            float _OutlineThickness;

            float4 Frag (Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                
                // Читаем центральный пиксель из маски
                float centerMask = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).r;
                
                // Если центр белый (сам объект) — пропускаем
                if (centerMask > 0.5) return half4(0,0,0,0);

                // Вычисляем размер пикселя
                float2 texelSize = _BlitScaleBias.xy;
                float thickness = _OutlineThickness;

                float maxMask = 0;
                
                // Ищем соседей (границы)
                maxMask = max(maxMask, SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(texelSize.x * thickness, 0)).r);
                maxMask = max(maxMask, SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv - float2(texelSize.x * thickness, 0)).r);
                maxMask = max(maxMask, SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(0, texelSize.y * thickness)).r);
                maxMask = max(maxMask, SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv - float2(0, texelSize.y * thickness)).r);

                // Если нашли соседа — рисуем контур
                if (maxMask > 0.5)
                {
                    return _OutlineColor;
                }

                return half4(0,0,0,0); 
            }
            
            ENDHLSL
        }
    }
}
