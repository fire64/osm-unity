Shader "Custom/ShallowWater" {
    Properties {
        _ShallowColor ("Shallow Water Color", Color) = (0.0, 0.5, 0.8, 0.5)
        _DeepColor ("Deep Water Color", Color) = (0.0, 0.2, 0.4, 0.8)
        _WaveStrength ("Wave Strength", Range(0, 0.1)) = 0.03
        _WaveSpeed ("Wave Speed", Range(0, 2)) = 0.5
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _Smoothness ("Smoothness", Range(0, 1)) = 0.8
        _DepthFactor ("Depth Factor", Range(0, 5)) = 1.0
    }

    SubShader {
        Tags { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
        }
        LOD 200

        GrabPass { "_WaterGrabTexture" }

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 grabPos : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
            };

            sampler2D _NormalMap;
            float4 _NormalMap_ST;
            fixed4 _ShallowColor;
            fixed4 _DeepColor;
            float _WaveStrength;
            float _WaveSpeed;
            float _Smoothness;
            float _DepthFactor;
            sampler2D _WaterGrabTexture;
            sampler2D _CameraDepthTexture;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _NormalMap);
                o.grabPos = ComputeGrabScreenPos(o.vertex);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // Анимация волн
                float2 waveOffset = float2(
                    _Time.y * _WaveSpeed * 0.1,
                    _Time.y * _WaveSpeed * 0.1
                );
                
                // Нормалы из нормал мапы
                half3 normal1 = UnpackNormal(tex2D(_NormalMap, i.uv + waveOffset));
                half3 normal2 = UnpackNormal(tex2D(_NormalMap, i.uv * 1.5 - waveOffset * 0.8));
                half3 normal = normalize(normal1 + normal2);

                // Искажение UV для преломления
                float2 distort = normal.xy * _WaveStrength;
                i.grabPos.xy += distort;

                // Цвет преломления
                fixed4 refractedColor = tex2Dproj(_WaterGrabTexture, i.grabPos);

                // Расчет глубины
                float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));
                float surfaceDepth = i.screenPos.w;
                float waterDepth = depth - surfaceDepth;
                float depthFactor = saturate(waterDepth * _DepthFactor);

                // Френелевский эффект
                float fresnel = 1.0 - saturate(dot(normalize(i.viewDir), normal));
                fresnel = pow(fresnel, 2);

                // Смешивание цветов на основе глубины
                fixed4 waterColor = lerp(_ShallowColor, _DeepColor, depthFactor);
                
                // Смешивание с преломленным цветом
                fixed4 finalColor = lerp(refractedColor, waterColor, waterColor.a);
                
                // Добавление отражений через френель
                finalColor.rgb += fresnel * 0.2;
                
                // Установка прозрачности
                finalColor.a = _Smoothness;

                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}