Shader "MiniJam/DoubleSidedStone"
{
    Properties
    {
        _Color ("Color", Color) = (0.35,0.39,0.44,1)
        _Metallic ("Metallic", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        struct Input { float3 worldPos; };
        fixed4 _Color;
        half _Metallic;
        half _Glossiness;
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _Color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
