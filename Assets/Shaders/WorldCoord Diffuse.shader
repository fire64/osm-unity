Shader "Custom/WorldCoord Diffuse"
{
       Properties{
           _Color("Main Color",Color)=(1,1,1,1)
           _MainTex("Base (RGB)",2D)="white"{}
           _NormTex("Normal (RGB)",2D)="bump"{}
           _Scale("Texture Scale",Vector)=(1.0,1.0,0,0)
           _BumpInfluence("Bump Influence",Float)=1.0
       }
       SubShader{
           Tags{"RenderType"="Opaque"}
           LOD 200
           CGPROGRAM
               #pragma surface surf Lambert
               sampler2D _MainTex;
               sampler2D _NormTex;
               fixed4 _Color;
               float4 _Scale;
               float _BumpInfluence/*,_Dimension*/;
               struct Input{
                   float3 worldNormal;
                   float3 worldPos;
                   float2 uv_MainTex;
                   float2 uv_NormTex;
                   INTERNAL_DATA
               };
               void surf(Input IN,inout SurfaceOutput o){
                   fixed4 texXY = tex2D(_MainTex, IN.worldPos.xy * _Scale.z);//IN.uv_MainTex);
                   fixed4 texXZ = tex2D(_MainTex, IN.worldPos.xz * _Scale.y);//IN.uv_MainTex);
                   fixed4 texYZ = tex2D(_MainTex, IN.worldPos.yz * _Scale.x);//IN.uv_MainTex);
                   fixed3 mask = fixed3(
                       dot(IN.worldNormal, fixed3(0, 0, 1)),
                       dot(IN.worldNormal, fixed3(0, 1, 0)),
                       dot(IN.worldNormal, fixed3(1, 0, 0)));
                   fixed4 tex =
                       texXY * abs(mask.x) +
                       texXZ * abs(mask.y) +
                       texYZ * abs(mask.z);
                   fixed4 c = tex * _Color;
                   o.Albedo = c.rgb;
  
                   texXY = tex2D(_NormTex, IN.worldPos.xy * _Scale.z);//IN.uv_MainTex);
                   texXZ = tex2D(_NormTex, IN.worldPos.xz * _Scale.y);//IN.uv_MainTex);
                   texYZ = tex2D(_NormTex, IN.worldPos.yz * _Scale.x);//IN.uv_MainTex);
                   mask = fixed3(
                       dot(IN.worldNormal, fixed3(0, 0, 1)),
                       dot(IN.worldNormal, fixed3(0, 1, 0)),
                       dot(IN.worldNormal, fixed3(1, 0, 0)));
                   tex =
                       texXY * abs(mask.x) +
                       texXZ * abs(mask.y) +
                       texYZ * abs(mask.z);
               }
           ENDCG
       }
       Fallback "Legacy Shaders/VertexLit"
    }