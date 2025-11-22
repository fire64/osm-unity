Shader "Custom/BillboardClouds"
{
    Properties
    {
        _MainTex ("Cloud Texture", 2D) = "white" {}
        _CloudColor ("Cloud Color", Color) = (1,1,1,1)
        _Transparency ("Transparency", Range(0, 1)) = 0.5
        _SunDirection ("Sun Direction (Use Script)", Vector) = (0,1,0)
        _LightScattering ("Light Scattering", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
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
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _CloudColor;
            float _Transparency;
            float3 _SunDirection;
            float _LightScattering;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 texCol = tex2D(_MainTex, i.uv);
                float alpha = texCol.a * (1 - _Transparency);

                // ƒл€ Quad по умолчанию нормаль Ч (0, 0, 1) в локальном пространстве
                float3 worldNormal = float3(0, 0, 1);
                worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, worldNormal));

                // ќсвещение от солнца (светла€ сторона)
                float3 lightDir = normalize(_SunDirection);
                float NdotL = dot(worldNormal, lightDir);
                float lighting = saturate(0.5 + NdotL * 0.5);
                lighting = lerp(1.0, lighting, _LightScattering);

                fixed3 finalColor = _CloudColor.rgb * lighting;
                return fixed4(finalColor, alpha);
            }
            ENDCG
        }
    }
    Fallback "Transparent/Diffuse"
}