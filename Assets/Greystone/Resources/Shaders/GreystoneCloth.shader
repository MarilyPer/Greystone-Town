// =====================================================================================
//  Greystone/Cloth
//  ---------------------------------------------------------------------------------
//  Ριγέ ύφασμα (τέντες πάγκων αγοράς, σημαίες). Οι ρίγες προκύπτουν από το uv.x.
//  Ο vertex shader «κυματίζει» το ύφασμα με τον άνεμο: η μετατόπιση είναι μηδενική
//  στο στερεωμένο άκρο (uv.y = 0) και μέγιστη στο ελεύθερο (uv.y = 1).
// =====================================================================================
Shader "Greystone/Cloth"
{
    Properties
    {
        _Color  ("Χρώμα Α", Color) = (0.7,0.15,0.1,1)
        _Color2 ("Χρώμα Β", Color) = (0.9,0.85,0.7,1)
        _Stripes ("Αριθμός ριγών", Float) = 8
        _Flutter ("Κυμάτισμα", Float) = 0.08
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow
        #pragma target 3.0
        #include "GreystoneCommon.cginc"

        fixed4 _Color, _Color2;
        float _Stripes, _Flutter;

        struct Input { float2 clothUV; };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float free = v.texcoord.y;
            float w = sin(_Time.y * 3.1 + wp.x * 1.7 + wp.z * 1.3) * 0.6 + sin(_Time.y * 5.3 + wp.x * 3.1) * 0.4;
            v.vertex.y += w * _Flutter * free * (0.4 + _GS_WindStrength);
            o.clothUV = v.texcoord.xy;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float s = step(0.5, frac(IN.clothUV.x * _Stripes * 0.5));
            float3 col = lerp(_Color.rgb, _Color2.rgb, s);
            col *= lerp(0.85, 1.0, gs_noise2(IN.clothUV * 40.0));
            float smooth = 0.05;
            gs_apply_wetness(col, smooth, 0.8, 1.0);
            o.Albedo = col;
            o.Smoothness = smooth;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
