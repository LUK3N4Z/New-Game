// Camera-facing twinkling star glint for interactive pickups (litter, sea glass, shells).
// Put a material using this shader on a Quad child of the item. The quad is billboarded in the
// vertex shader, so its own rotation doesn't matter. Each item twinkles on its own rhythm
// (phase is hashed from its world position). Additive, unlit, no textures: cheap on mobile.
Shader "OceanGuardians/SparkleGlint"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.95, 0.7, 1)
        _Size ("Size (metres)", Float) = 0.7
        _Speed ("Twinkle Speed", Float) = 2.2
        _BaseGlow ("Glow Between Twinkles", Range(0, 1)) = 0.3
        _RayThinness ("Ray Thinness", Float) = 14
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True" // billboarding needs the object's own pivot
        }

        Pass
        {
            Name "Glint"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Size;
                float _Speed;
                float _BaseGlow;
                float _RayThinness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float twinkle : TEXCOORD1;
                float spin : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float phase = frac(sin(dot(centerWS.xz, float2(12.9898, 78.233))) * 43758.5453) * 6.2832;
                float t = _Time.y * _Speed + phase;
                // Mostly a soft glow, with a brief bright flash each cycle.
                float flash = pow(saturate(sin(t) * 0.5 + 0.5), 6);
                o.twinkle = lerp(_BaseGlow, 1, flash);
                o.spin = t * 0.35;

                float2 corner = v.uv * 2 - 1;
                float3 centerVS = TransformWorldToView(centerWS);
                centerVS.xy += corner * (_Size * 0.5 * (0.6 + 0.4 * flash));
                o.positionCS = TransformWViewToHClip(centerVS);
                o.uv = corner;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float s, c;
                sincos(i.spin, s, c);
                float2 p = float2(c * i.uv.x - s * i.uv.y, s * i.uv.x + c * i.uv.y);
                float r = length(p);
                float fade = saturate(1 - r);

                float core = pow(saturate(1 - r * 3), 2);
                float rays = pow(saturate(1 - abs(p.x * p.y) * _RayThinness), 6) * fade;
                float2 q = float2(p.x + p.y, p.x - p.y) * 0.7071;
                float diag = pow(saturate(1 - abs(q.x * q.y) * _RayThinness * 2), 6) * saturate(1 - r * 1.8);
                float glow = fade * fade * 0.3;

                float a = (core + rays + diag * 0.5 + glow) * i.twinkle;
                return half4(_Color.rgb * a, 1);
            }
            ENDHLSL
        }
    }
}
