Shader "SebraeLab/Feixe" {
 Properties {
  _Cor ("Cor", Color) = (1,0.68,0.14,1)
  _Intensidade ("Intensidade", Range(0,1)) = 0.55
  _Brilho ("Brilho (pulso e esmaecimento)", Range(0,1)) = 1
 }
 SubShader {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Lighting Off Cull Off ZWrite Off ZTest LEqual
  Blend One One
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   fixed4 _Cor;
   float _Intensidade;
   float _Brilho;
   struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f { float4 vertex : SV_POSITION; float altura : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
   v2f vert (appdata v)
   {
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.altura = v.uv.y;
    return o;
   }
   fixed4 frag (v2f i) : SV_Target
   {
    float forca = pow(saturate(1 - i.altura), 1.5) * _Intensidade * _Brilho;
    return fixed4(_Cor.rgb * forca, 1);
   }
   ENDCG
  }
 }
}
