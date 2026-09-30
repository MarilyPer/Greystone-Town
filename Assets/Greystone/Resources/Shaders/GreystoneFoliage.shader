// =====================================================================================
//  Greystone/Foliage
//  ---------------------------------------------------------------------------------
//  Shader βλάστησης (γρασίδι, στάχυα, φύλλωμα δέντρων) με:
//
//   * Κίνηση από τον άνεμο στον vertex shader: οι κορυφές μετατοπίζονται ανάλογα με το
//     ύψος τους (οι ρίζες μένουν ακίνητες) και με τις καθολικές τιμές _GS_WindStrength /
//     _GS_WindDir. Η φάση εξαρτάται από τη θέση στον κόσμο, ώστε να «κυματίζει» το χωράφι.
//   * GPU instancing: τα χιλιάδες φυτά ζωγραφίζονται με ελάχιστα draw calls
//     (Graphics.RenderMeshInstanced). Η παραλλαγή χρώματος ανά φυτό προκύπτει από hash
//     της θέσης του instance -> δεν χρειάζονται δεδομένα ανά instance από τη CPU.
//   * Cull Off: τα φύλλα/λεπίδες φαίνονται και από τις δύο πλευρές.
//   * _UpNormals: οι κάθετοι γίνονται «προς τα πάνω», ώστε το γρασίδι να φωτίζεται όπως
//     το έδαφος (τεχνική που αποφεύγει το «σκληρό» φωτισμό σε λεπτές λεπίδες).
// =====================================================================================
Shader "Greystone/Foliage"
{
    Properties
    {
        _Color      ("Χρώμα βάσης", Color) = (0.2,0.3,0.1,1)
        _TipColor   ("Χρώμα κορυφής", Color) = (0.45,0.55,0.2,1)
        _Height     ("Ύψος φυτού (τοπικό)", Float) = 1
        _Sway       ("Ποσότητα ταλάντωσης", Float) = 0.15
        _Variation  ("Παραλλαγή χρώματος", Range(0,1)) = 0.25
        _UpNormals  ("Κάθετοι προς τα πάνω (0/1)", Float) = 1
        _Glossiness ("Λείανση", Range(0,1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing
        #include "GreystoneCommon.cginc"

        fixed4 _Color;
        fixed4 _TipColor;
        float  _Height;
        float  _Sway;
        float  _Variation;
        float  _UpNormals;
        half   _Glossiness;

        struct Input
        {
            float  heightT;     // 0 στη ρίζα, 1 στην κορυφή
            float3 instPos;     // θέση του instance στον κόσμο (για παραλλαγή χρώματος)
            float3 worldNormal;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);

            float3 origin = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float h = saturate(v.vertex.y / _Height);

            // Δύο ημίτονα διαφορετικής συχνότητας = ακανόνιστες ριπές ανέμου
            float phase = _Time.y * 1.6 + wp.x * 0.21 + wp.z * 0.17;
            float gust = sin(phase) * 0.6 + sin(phase * 2.37 + 1.3) * 0.3 + 0.5;
            float2 dir = normalize(_GS_WindDir.xy + 1e-4);
            float amount = _GS_WindStrength * _Sway * gust * h * h;
            float3 offW = float3(dir.x, 0, dir.y) * amount;
            // Μικρό τρέμουλο κάθετα στον άνεμο (φύλλα)
            offW.xz += float2(-dir.y, dir.x) * sin(_Time.y * 4.3 + wp.y * 3.0 + origin.x) * amount * 0.25;

            v.vertex.xyz += mul((float3x3)unity_WorldToObject, offW);

            if (_UpNormals > 0.5)
                v.normal = normalize(mul((float3x3)unity_WorldToObject, float3(0, 1, 0)));

            o.heightT = h;
            o.instPos = origin;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float r = gs_hash21(IN.instPos.xz * 0.731);
            float3 col = lerp(_Color.rgb, _TipColor.rgb, IN.heightT);
            col *= lerp(1.0 - _Variation, 1.0 + _Variation * 0.6, r);
            // Κιτρινωπή απόχρωση σε μερικά φυτά
            col = lerp(col, col * float3(1.2, 1.1, 0.6), step(0.85, r) * 0.5);
            // Σκουρότερο κοντά στη ρίζα (ψεύτικη απόφραξη φωτός)
            float ao = lerp(0.55, 1.0, IN.heightT);

            float smooth = _Glossiness;
            gs_apply_wetness(col, smooth, 0.6, 1.0);
            o.Albedo = col;
            o.Occlusion = ao;
            o.Smoothness = smooth;
            o.Metallic = 0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
