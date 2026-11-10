Shader "SebraeLab/Piso de madeira" {
 Properties { _MainTex ("Madeira", 2D) = "white" {} _Color ("Cor", Color) = (1,1,1,1) }
 SubShader {
  Tags { "RenderType"="Opaque" }
  CGPROGRAM
  #pragma surface surf Lambert vertex:vert
  sampler2D _MainTex;
  fixed4 _Color;
  struct Input { float2 uv_MainTex; };
  void vert(inout appdata_full v) {
   v.vertex.xyz += mul((float3x3)unity_WorldToObject,float3(0,0.003,0));
  }
  void surf(Input i, inout SurfaceOutput o) { o.Albedo = tex2D(_MainTex,i.uv_MainTex).rgb * _Color.rgb; }
  ENDCG
 }
 FallBack "Diffuse"
}
