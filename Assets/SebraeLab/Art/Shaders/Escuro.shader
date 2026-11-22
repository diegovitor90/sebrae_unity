Shader "SebraeLab/Escuro" {
 Properties {
  _Alfa ("Alfa", Range(0,1)) = 0
 }
 SubShader {
  Tags { "Queue"="Overlay" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Lighting Off Cull Front ZWrite Off ZTest Always
  Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float _Alfa;
   struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f { float4 vertex : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
   v2f vert (appdata v)
   {
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.vertex = UnityObjectToClipPos(v.vertex);
    return o;
   }
   fixed4 frag (v2f i) : SV_Target
   {
    return fixed4(0, 0, 0, _Alfa);
   }
   ENDCG
  }
 }
}
