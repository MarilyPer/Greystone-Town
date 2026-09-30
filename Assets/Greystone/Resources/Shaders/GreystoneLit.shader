// =====================================================================================
//  Greystone/Lit
//  ---------------------------------------------------------------------------------
//  Γενικής χρήσης φωτιζόμενος (PBR) surface shader του Built-in Render Pipeline.
//  Χρησιμοποιείται για ξύλο, μέταλλο, ύφασμα, σοβά, γυαλί παραθύρων κ.λπ.
//
//  Χαρακτηριστικά:
//   * Διαδικαστική παραλλαγή χρώματος με θόρυβο σε world space (_NoiseScale/_NoiseAmount)
//     ώστε οι επιφάνειες να μη φαίνονται «πλαστικές» χωρίς χρήση υφών.
//   * _Stretch: τεντώνει τον θόρυβο σε έναν άξονα -> «νερά» ξύλου.
//   * _NightGlow = 1: η εκπομπή φωτός (emission) πολλαπλασιάζεται με την καθολική τιμή
//     _GS_NightGlow, άρα ΟΛΑ τα παράθυρα/φανάρια ανάβουν τη νύχτα με ΜΙΑ εντολή C#.
//   * GPU instancing (multi_compile_instancing) για χρήση με Graphics.RenderMeshInstanced.
// =====================================================================================
Shader "Greystone/Lit"
{
    Properties
    {
        _Color       ("Βασικό χρώμα", Color) = (1,1,1,1)
        _Color2      ("Χρώμα παραλλαγής", Color) = (0.8,0.8,0.8,1)
        _NoiseScale  ("Κλίμακα θορύβου", Float) = 2
        _NoiseAmount ("Ποσότητα παραλλαγής", Range(0,1)) = 0.3
        _Stretch     ("Τέντωμα θορύβου (xyz)", Vector) = (1,1,1,0)
        _Glossiness  ("Λείανση (smoothness)", Range(0,1)) = 0.2
        _Metallic    ("Μεταλλικότητα", Range(0,1)) = 0
        _EmissionColor ("Εκπομπή", Color) = (0,0,0,1)
        _NightGlow   ("Εκπομπή μόνο τη νύχτα (0/1)", Float) = 0
        _WetResponse ("Απόκριση στη βροχή", Range(0,1)) = 1
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
        float  _NoiseScale;
        float  _NoiseAmount;
        float4 _Stretch;
        half   _Glossiness;
        half   _Metallic;
        fixed4 _EmissionColor;
        float  _NightGlow;
        float  _WetResponse;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Θόρυβος σε world space: ίδιο «μοτίβο» ανεξάρτητα από την κλίμακα του αντικειμένου.
            float3 p = IN.worldPos * _NoiseScale / max(_Stretch.xyz, 0.001);
            float n = gs_noise3(p) * 0.6 + gs_noise3(p * 3.1) * 0.4;
            float3 albedo = lerp(_Color.rgb, _Color2.rgb, n * _NoiseAmount * 2.0 - _NoiseAmount * 0.5);

            float smooth = _Glossiness;
            gs_apply_wetness(albedo, smooth, _WetResponse, IN.worldNormal.y);

            o.Albedo = saturate(albedo);
            o.Metallic = _Metallic;
            o.Smoothness = smooth;
            // Εκπομπή: είτε σταθερή, είτε ελεγχόμενη από τη νύχτα (lerp ανάμεσα στις δύο).
            float glow = lerp(1.0, _GS_NightGlow, saturate(_NightGlow));
            o.Emission = _EmissionColor.rgb * glow;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
