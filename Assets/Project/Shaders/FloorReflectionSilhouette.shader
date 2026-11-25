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
            float4 _FloorReflectionFade;  // x = hauteur ou le reflet disparait, y / z = distance camera debut / fin (0 = coupe)
            float4 _FloorReflectionCamPos;

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
                float height = dot(_FloorReflectionPlane.xyz, input.positionWS) + _FloorReflectionPlane.w;
                clip(height);

                // Attenuation avec la hauteur au-dessus de la glace : 1 aux pieds, 0 a Fade Height
                float value = 1.0;
                if (_FloorReflectionFade.x > 0.0)
                    value *= 1.0 - saturate(height / _FloorReflectionFade.x);

                // Attenuation avec la distance a la camera du point reflete (celui qu'on voit sur la glace)
                if (_FloorReflectionFade.z > 0.0)
                {
                    float3 reflected = input.positionWS - 2.0 * height * _FloorReflectionPlane.xyz;
                    float dist = distance(reflected, _FloorReflectionCamPos.xyz);
                    float range = max(_FloorReflectionFade.z - _FloorReflectionFade.y, 0.001);
                    value *= 1.0 - saturate((dist - _FloorReflectionFade.y) / range);
                }
                return half4(value, value, value, 1);
            }
            ENDHLSL
        }
    }
}
