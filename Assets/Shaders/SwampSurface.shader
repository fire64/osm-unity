Shader "Custom/SwampSurface"
{
    Properties
    {
        _WaterColor ("Water Color", Color) = (0,0.2,0.1,1)
        _DeepColor ("Deep Color", Color) = (0,0.05,0.05,1)
        _FoamColor ("Foam Color", Color) = (0.8,0.9,0.7,1)
        _RippleTex ("Ripple Texture", 2D) = "bump" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _DepthFactor ("Depth Factor", Float) = 1.0
        _RippleSpeed ("Ripple Speed", Float) = 0.5
        _RippleScale ("Ripple Scale", Float) = 2.0
        _FoamThreshold ("Foam Threshold", Float) = 0.3
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow
        #pragma target 3.0

        sampler2D _RippleTex;
        sampler2D _NoiseTex;
        fixed4 _WaterColor;
        fixed4 _DeepColor;
        fixed4 _FoamColor;
        float _DepthFactor;
        float _RippleSpeed;
        float _RippleScale;
        float _FoamThreshold;

        struct Input
        {
            float2 uv_RippleTex;
            float2 uv_NoiseTex;
            float3 worldPos;
            float depth;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.depth = -mul(UNITY_MATRIX_MV, v.vertex).z;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Анимированные рябь и шум
            float2 rippleUV = IN.uv_RippleTex * _RippleScale;
            float2 noiseUV = IN.uv_NoiseTex * _RippleScale;
            
            rippleUV.x += _Time.x * _RippleSpeed;
            rippleUV.y += _Time.x * _RippleSpeed * 0.8;
            
            noiseUV.x -= _Time.x * _RippleSpeed * 0.5;
            noiseUV.y += _Time.x * _RippleSpeed * 0.6;

            // Получаем нормали ряби
            half3 ripple = UnpackNormal(tex2D(_RippleTex, rippleUV));
            half3 noise = UnpackNormal(tex2D(_NoiseTex, noiseUV));
            
            // Смешиваем текстуры
            o.Normal = normalize(ripple + noise * 0.5);

            // Расчет глубины
            float depth = IN.depth * _DepthFactor;
            float depthFactor = saturate(depth / _DepthFactor);

            // Цвет воды с учетом глубины
            fixed4 waterColor = lerp(_WaterColor, _DeepColor, depthFactor);

            // Генерация пены
            float foam = saturate(ripple.y * noise.x * (1 - depthFactor));
            foam = step(_FoamThreshold, foam);

            // Финальный цвет
            o.Albedo = lerp(waterColor.rgb, _FoamColor.rgb, foam);
            o.Smoothness = waterColor.a;
            o.Metallic = 0.3;
        }
        ENDCG
    }
    FallBack "Diffuse"
}