Shader "Custom/CloudParticleShader"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _CloudColor ("Cloud Color", Color) = (1,1,1,1)
        _CloudDensity ("Cloud Density", Range(0, 1)) = 0.5
        _SunDirection ("Sun Direction (Script)", Vector) = (0, 1, 0)
        _SunLightIntensity ("Sun Light Intensity", Range(0, 5)) = 1.0
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.5
        _CloudThickness ("Cloud Thickness", Range(0.1, 2.0)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off

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
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float3 worldPos : TEXCOORD1; // Для освещения
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _CloudColor;
            float _CloudDensity;
            float3 _SunDirection;
            float _SunLightIntensity;
            float _CloudCoverage;
            float _CloudThickness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Текстура и цвет облака
                fixed4 col = tex2D(_MainTex, i.uv) * _CloudColor * i.color;
                col.a *= _CloudDensity * _CloudCoverage;

                // Освещение от солнца (упрощенное)
                float3 lightDir = normalize(_SunDirection);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 normal = float3(0, 0, 1); // Нормаль частицы (может быть изменена для объемности)

                // Освещение на передней части облака
                float NdotL = saturate(dot(normal, lightDir));
                float frontLight = saturate(NdotL * 0.5 + 0.5); // Простое освещение

                // Освещение на задней части облака (для объема)
                float backLight = saturate(dot(-normal, lightDir));
                float volumeLight = lerp(backLight, frontLight, _CloudThickness);

                col.rgb *= (1.0 + volumeLight * _SunLightIntensity * 0.5); // Упрощенное объемное освещение

                // Учет прозрачности
                col.a = saturate(col.a);
                return col;
            }
            ENDCG
        }
    }
    Fallback "Transparent/VertexLit"
}