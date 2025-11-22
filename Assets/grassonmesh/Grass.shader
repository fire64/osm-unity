Shader "Custom/Grass" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _WindStrength ("Wind Strength", Range(0, 0.5)) = 0.1
        _WindSpeed ("Wind Speed", Range(0, 2)) = 0.5
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    }

    SubShader {
        Tags { 
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
        }
        LOD 100

        // Не пишем в буфер глубины для прозрачных частей, но тестируем глубину
        ZWrite On
        ZTest LEqual
        Cull Off

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _WindStrength;
            float _WindSpeed;
            float _Cutoff;

            v2f vert (appdata v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                // Анимация ветра - более плавная
                float windWave = sin(_Time.y * _WindSpeed + v.vertex.x * 5.0 + v.vertex.z * 3.0) * _WindStrength;
                windWave += cos(_Time.y * _WindSpeed * 0.7 + v.vertex.x * 3.0) * _WindStrength * 0.5;
                
                // Ветер сильнее влияет на верхушку травинки
                float windEffect = windWave * v.vertex.y;
                v.vertex.x += windEffect;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                
                // Альфа-тест для отсечения прозрачных частей
                clip(col.a - _Cutoff);
                
                return col;
            }
            ENDCG
        }
    }
    
    Fallback "Transparent/Cutout/VertexLit"
}