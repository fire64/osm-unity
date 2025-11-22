Shader "Custom/ProceduralSky"
{
    Properties
    {
        _SunDirection ("Sun Direction (Use Script)", Vector) = (0, 1, 0) // Не используется в Inspector, устанавливается скриптом
        _DayProgress ("Day Progress (0-1)", Range(0, 1)) = 0.25
        _SunIntensity ("Sun Intensity", Range(0, 5)) = 1.0
        _StarThreshold ("Star Threshold", Range(0, 1)) = 0.946
        _StarBrightness ("Star Brightness", Range(0, 2)) = 0.49

        // --- НОВОЕ: Добавляем цвета ---
        _NightSkyColor ("Night Sky Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _DawnDuskColor ("Dawn/Dusk Sky Color", Color) = (0.4, 0.3, 0.2, 1.0)
        _DaySkyColor ("Day Sky Color", Color) = (0.4, 0.7, 1.0, 1.0)
    }
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "PreviewType"="Skybox" }
        LOD 100
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0; // Это направление для Skybox
            };

            struct v2f
            {
                float3 worldDirection : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            // --- Properties ---
            float4 _SunDirection;
            float _DayProgress;
            float _SunIntensity;
            float _StarThreshold;
            float _StarBrightness;
            fixed4 _NightSkyColor;
            fixed4 _DawnDuskColor;
            fixed4 _DaySkyColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // texcoord уже содержит направление из центра объекта (камеры)
                o.worldDirection = v.texcoord;
                return o;
            }

            // --- Simple 3D Noise для звёзд ---
            float rand(float3 coord)
            {
                return frac(sin(dot(coord, float3(12.9898, 78.233, 45.164))) * 43758.5453);
            }

            float noise(float3 coord)
            {
                float3 i = floor(coord);
                float3 f = frac(coord);

                float a = rand(i);
                float b = rand(i + float3(1, 0, 0));
                float c = rand(i + float3(0, 1, 0));
                float d = rand(i + float3(1, 1, 0));
                float e = rand(i + float3(0, 0, 1));
                float f_noise = rand(i + float3(1, 0, 1));
                float g = rand(i + float3(0, 1, 1));
                float h = rand(i + float3(1, 1, 1));

                float3 u = f * f * (3 - 2 * f);
                return lerp(
                    lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y),
                    lerp(lerp(e, f_noise, u.x), lerp(g, h, u.x), u.y),
                    u.z
                );
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 direction = normalize(i.worldDirection);
                float3 sunDir = normalize(_SunDirection.xyz);

                // --- 1. Цвет неба в зависимости от времени суток ---
                // Используем _DayProgress и цвета из Properties
                fixed3 nightSkyColor = _NightSkyColor.rgb;
                fixed3 dawnDuskColor = _DawnDuskColor.rgb;
                fixed3 daySkyColor = _DaySkyColor.rgb;

                // Приближение: ночь (0-0.25 и 0.75-1), рассвет/закат (0.25-0.35 и 0.65-0.75), день (0.35-0.65)
                float timeOfDay = _DayProgress;
                float t;
                fixed3 skyColor;

                if (timeOfDay < 0.25 || timeOfDay > 0.75)
                { // Ночь
                    float nightBlend = timeOfDay < 0.25 ? timeOfDay / 0.25 : (1 - timeOfDay) / 0.25;
                    skyColor = lerp(nightSkyColor, dawnDuskColor, nightBlend);
                }
                else if (timeOfDay < 0.35 || timeOfDay > 0.65)
                { // Рассвет/Закат
                    float dawnDuskBlend = timeOfDay < 0.35 ? (timeOfDay - 0.25) / 0.1 : (0.75 - timeOfDay) / 0.1;
                    skyColor = lerp(dawnDuskColor, daySkyColor, dawnDuskBlend);
                }
                else
                { // День
                    skyColor = daySkyColor;
                }

                // --- 2. Рэлеевское рассеяние ---
                float y = direction.y; // Высота над горизонтом
                float rayleighFactor = pow(1.0 - saturate(y), 2.0); // Рассеяние
                skyColor = lerp(skyColor, fixed3(0.7, 0.9, 1.0), rayleighFactor * 0.3);

                // --- 3. Влияние солнца (яркость возле солнца) ---
                float sunDot = dot(direction, sunDir);
                float sunFactor = pow(saturate(sunDot), 100); // Острая яркость
                skyColor += sunFactor * _SunIntensity * 0.5;

                // --- 4. Звёзды ---
                // Появляются только ночью (например, когда DayProgress > 0.75 ИЛИ < 0.25)
                bool isNight = (_DayProgress > 0.75 || _DayProgress < 0.25);
                if (isNight)
                {
                    float starNoise = noise(direction * 100); // Масштаб шума для детализации
                    float stars = step(_StarThreshold, starNoise);
                    skyColor += stars * _StarBrightness;
                }

                return fixed4(skyColor, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Skybox/6 Sided"
}