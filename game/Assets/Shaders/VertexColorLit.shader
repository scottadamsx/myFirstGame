// URP lit shader: detail texture (siding/windows/grass) x vertex color (hue).
Shader "StJohns/VertexColorLit"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _BaseMap("Detail Texture", 2D) = "white" {}
        // 1 = facade mode: texture holds [ground-floor | upper-floor] cells,
        // uv.y counts storeys, uv.x < 0 marks roof planes (flat vertex color)
        _FacadeMode("Facade Mode", Float) = 0
        _WindWave("Wind Wave", Float) = 0
        // roads painted into the terrain: world-space projected splatmap
        _RoadMap("Road Splatmap", 2D) = "black" {}
        _RoadUV("Road UV Transform", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 color      : TEXCOORD3;
                float  fogCoord   : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RoadMap);
            SAMPLER(sampler_RoadMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _FacadeMode;
                float _WindWave;
                float4 _RoadUV;   // u = wp.x*x + z ; v = wp.z*y + w
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float ndl = saturate(dot(n, mainLight.direction));
                float3 lighting = mainLight.color * mainLight.shadowAttenuation * ndl + SampleSH(n);
                float3 detail;
                if (_FacadeMode > 0.5)
                {
                    if (IN.uv.x < -0.5)
                    {
                        detail = float3(1, 1, 1);              // roof plane: vertex color only
                    }
                    else
                    {
                        // atlas: x = ground|upper, y = residential|commercial
                        float xShift = IN.uv.y < 1.0 ? 0.0 : 0.5;      // ground floor gets door/storefront
                        float yShift = IN.color.a < 0.7 ? 0.5 : 0.0;   // wall alpha flags commercial
                        float2 cuv = frac(IN.uv);
                        detail = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap,
                                                  float2(cuv.x * 0.5 + xShift, cuv.y * 0.5 + yShift)).rgb;
                    }
                }
                else
                {
                    detail = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb;
                    float roadA = 0;
                    float3 roadCol = 0;
                    if (abs(_RoadUV.x) > 1e-8)
                    {
                        float2 ruv = float2(IN.positionWS.x * _RoadUV.x + _RoadUV.z,
                                            IN.positionWS.z * _RoadUV.y + _RoadUV.w);
                        if (all(ruv >= 0) && all(ruv <= 1))
                        {
                            float4 road = SAMPLE_TEXTURE2D(_RoadMap, sampler_RoadMap, ruv);
                            roadA = road.a;
                            roadCol = road.rgb;
                        }
                    }
                    if (_WindWave > 0.5)
                    {
                        // wind gusts sweeping across the grass (not the pavement)
                        float w = sin(IN.positionWS.x * 0.11 + IN.positionWS.z * 0.07 + _Time.y * 1.4)
                                + sin(IN.positionWS.x * 0.031 - IN.positionWS.z * 0.043 + _Time.y * 0.7);
                        detail *= 1.0 + 0.05 * w * (1.0 - roadA);
                    }
                    // roads are painted INTO the ground — override grass color
                    detail = lerp(detail * IN.color.rgb, roadCol, roadA) / max(IN.color.rgb, 0.0001);
                }
                float3 c = detail * IN.color.rgb * _BaseColor.rgb * lighting;
                c = MixFog(c, IN.fogCoord);
                return half4(c, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
