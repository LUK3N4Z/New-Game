Shader "OceanGuardians/StylizedWater"
{
    // Cozy stylized shoreline water: depth-based shallow->deep ramp, soft foam rim where the
    // seabed rises to meet the sand, and cheap animated caustics in the shallows.
    // Requires the URP depth texture (enabled on the pipeline assets by the scene builder).
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.45, 0.95, 0.95, 0.55)
        _DeepColor ("Deep Color", Color) = (0.05, 0.45, 0.75, 0.92)
        _DepthRange ("Depth Ramp (metres)", Float) = 2.2
        _FoamWidth ("Foam Width (metres)", Float) = 0.35
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _CausticsScale ("Caustics Scale", Float) = 1.6
        _CausticsSpeed ("Caustics Speed", Float) = 0.6
        _CausticsStrength ("Caustics Strength", Float) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "StylizedWater"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor, _DeepColor, _FoamColor;
                float _DepthRange, _FoamWidth, _CausticsScale, _CausticsSpeed, _CausticsStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = vpi.positionCS;
                o.screenPos = ComputeScreenPos(vpi.positionCS);
                o.worldPos = vpi.positionWS;
                return o;
            }

            // Cheap two-wave interference caustics (no textures, mobile-friendly).
            float Caustics(float2 p, float t)
            {
                float a = sin(p.x * 1.7 + t) + sin(p.y * 1.3 - t * 0.8);
                float b = sin((p.x + p.y) * 1.1 + t * 0.6);
                float c = (a + b) * 0.25 + 0.5;
                return pow(saturate(c), 3.0);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float waterDepth = max(sceneDepth - surfaceDepth, 0.0);

                float depthK = saturate(waterDepth / max(_DepthRange, 0.01));
                half4 color = lerp(_ShallowColor, _DeepColor, depthK);

                float caustics = Caustics(input.worldPos.xz * _CausticsScale, _Time.y * _CausticsSpeed * 6.28);
                color.rgb += caustics * _CausticsStrength * (1.0 - depthK);

                float foamBand = 1.0 - smoothstep(0.0, max(_FoamWidth, 0.01), waterDepth);
                float foamWobble = 0.75 + 0.25 * sin(input.worldPos.x * 2.1 + _Time.y * 1.8 + sin(input.worldPos.z * 1.7));
                float foam = saturate(foamBand * foamWobble * 1.6);
                color = lerp(color, _FoamColor, foam);

                return color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
