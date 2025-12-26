Shader "Custom/SimpleWireframeOcclusion"
{
    Properties
    {
        _WireColor ("Wire Color", Color) = (1,1,1,1)
        _WireWidth ("Wire Width", Range(1, 50)) = 5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma require geometry
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _WireWidth;
            half4 _WireColor;
            struct Attributes
            {
                float4 positionOS : POSITION;
            };
            struct v2g
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };
            struct g2f
            {
                float4 positionCS : SV_POSITION;
                float3 bary : TEXCOORD0;
            };
            v2g vert(Attributes input)
            {
                v2g output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }
            [maxvertexcount(3)]
            void geom(triangle v2g input[3], inout TriangleStream<g2f> stream)
            {
                float3 p0 = input[0].positionWS;
                float3 p1 = input[1].positionWS;
                float3 p2 = input[2].positionWS;
                
                float d01 = length(p1 - p0);
                float d12 = length(p2 - p1);
                float d20 = length(p0 - p2);
                
                float maxDist = max(d01, max(d12, d20));
                
                g2f output;
                
                float maskZ = (d01 >= maxDist) ? 1.0 : 0.0;
                float maskX = (d12 >= maxDist) ? 1.0 : 0.0;
                float maskY = (d20 >= maxDist) ? 1.0 : 0.0;
                
                output.positionCS = input[0].positionCS;
                output.bary = float3(1, maskY, maskZ);
                stream.Append(output);
                
                output.positionCS = input[1].positionCS;
                output.bary = float3(maskX, 1, maskZ);
                stream.Append(output);
                
                output.positionCS = input[2].positionCS;
                output.bary = float3(maskX, maskY, 1);
                stream.Append(output);
            }
            half4 frag(g2f input) : SV_Target
            {
                float3 bary = input.bary;
                float minDist = min(bary.x, min(bary.y, bary.z));
                
                float delta = fwidth(minDist);
                float edge = smoothstep(delta * _WireWidth * 0.5, delta * (_WireWidth * 0.5 + 1), minDist);
                
                if (edge > 0.99) discard;
                
                return half4(_WireColor.rgb, (1 - edge) * _WireColor.a);
            }
            ENDHLSL
        }
    }
}