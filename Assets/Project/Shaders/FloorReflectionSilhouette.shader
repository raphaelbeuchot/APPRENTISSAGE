// Silhouette en aplat des personnages, dessinee par FloorCharacterReflection dans la texture du reflet.
// Pas d'eclairage, pas de culling (le miroir inverse le sens des faces), pas de profondeur (un aplat).
// Coupe ce qui est sous le plan de la glace (_FloorReflectionPlane) pour qu'il ne remonte pas dans le reflet.
Shader "Hidden/FloorReflectionSilhouette"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _FloorReflectionPlane; // xyz = normale, w = -dot(normale, point du plan)
            float4x4 _FloorReflectionVP;  // vue miroir x projection, donnee par le script (pas les matrices d'URP)

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = mul(_FloorReflectionVP, float4(o.positionWS, 1.0));
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(dot(_FloorReflectionPlane.xyz, input.positionWS) + _FloorReflectionPlane.w);
                return half4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }
}
