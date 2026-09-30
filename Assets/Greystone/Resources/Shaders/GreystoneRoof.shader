// =====================================================================================
//  Greystone/Roof
//  ---------------------------------------------------------------------------------
//  Διαδικαστικές πέτρινες πλάκες στέγης (Cotswold stone slates) ή άχυρο (thatch).
//
//  Οι σειρές των πλακών είναι οριζόντιες, άρα αρκεί το ύψος (worldPos.y) για να
//  ορίσουμε τη σειρά, ανεξάρτητα από τον προσανατολισμό της στέγης. Η στήλη ορίζεται
//  από το άθροισμα x+z (λειτουργεί για στέγες στραμμένες ανά 90°). Με _Thatch = 1 το
//  μοτίβο γίνεται ινώδες (άχυρο) αντί για πλάκες.
// =====================================================================================
Shader "Greystone/Roof"
{
    Properties
    {
        _Color      ("Χρώμα", Color) = (0.45,0.43,0.4,1)
        _Color2     ("Χρώμα παραλλαγής", Color) = (0.36,0.35,0.3,1)
        _RowHeight  ("Ύψος σειράς", Float) = 0.16
        _SlateWidth ("Πλάτος πλάκας", Float) = 0.42
        _Thatch     ("Άχυρο (0/1)", Float) = 0
        _Glossiness ("Λείανση", Range(0,1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #pragma multi_compile_instancing
        #include "GreystoneCommon.cginc"

        fixed4 _Color;
        fixed4 _Color2;
        float  _RowHeight;
        float  _SlateWidth;
        float  _Thatch;
        half   _Glossiness;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float along = IN.worldPos.x + IN.worldPos.z;
            float row = floor(IN.worldPos.y / _RowHeight);
            float fy = frac(IN.worldPos.y / _RowHeight);

            // --- Πλάκες ---
            float x = along + fmod(abs(row), 2.0) * _SlateWidth * 0.5;
            float2 cell = float2(floor(x / _SlateWidth), row);
            float fx = frac(x / _SlateWidth);
            float h = gs_hash21(cell);
            float3 slate = lerp(_Color.rgb, _Color2.rgb, h) * lerp(0.8, 1.1, gs_hash21(cell + 9.1));
            // Σκιά στο κάτω χείλος κάθε σειράς (οι πλάκες επικαλύπτονται)
            float lip = smoothstep(0.0, 0.25, fy);
            float gap = smoothstep(0.0, 0.05, min(fx, 1.0 - fx));
            slate *= lerp(0.55, 1.0, lip) * lerp(0.7, 1.0, gap);
            // Βρύα/λειχήνες στις παλιές στέγες
            float moss = smoothstep(0.55, 0.8, gs_fbm2(IN.worldPos.xz * 0.8 + along * 0.3));
            slate = lerp(slate, float3(0.33, 0.38, 0.2), moss * 0.45);

            // --- Άχυρο ---
            float fibre = gs_noise2(float2(along * 38.0, IN.worldPos.y * 3.0));
            float3 thatch = lerp(_Color.rgb, _Color2.rgb, fibre) * lerp(0.7, 1.0, smoothstep(0.0, 0.6, fy));

            float3 albedo = lerp(slate, thatch, saturate(_Thatch));
            float smooth = _Glossiness;
            gs_apply_wetness(albedo, smooth, 1.0, IN.worldNormal.y);

            o.Albedo = albedo;
            o.Smoothness = smooth;
            o.Metallic = 0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
