// =====================================================================================
//  Greystone/Water
//  ---------------------------------------------------------------------------------
//  Ημιδιαφανές νερό ποταμού.
//   * Vertex shader: μικρά κύματα (άθροισμα ημιτόνων) που μετακινούν τις κορυφές.
//   * Fragment: οι κάθετοι (normals) διαταράσσονται με θόρυβο που «ρέει» κατά μήκος
//     της κοίτης (_FlowDir), οπότε το νερό φαίνεται να κυλά.
//   * Fresnel: σε μικρή γωνία θέασης το νερό ανακλά περισσότερο (πιο αδιαφανές, πιο
//     φωτεινό), ενώ από πάνω φαίνεται πιο διάφανο — όπως στη φύση.
// =====================================================================================
Shader "Greystone/Water"
{
    Properties
    {
        _Deep    ("Βαθύ νερό", Color) = (0.07,0.16,0.18,1)
        _Shallow ("Ρηχό νερό", Color) = (0.2,0.33,0.3,1)
        _FlowDir ("Κατεύθυνση ροής (xz)", Vector) = (0,-1,0,0)
        _FlowSpeed ("Ταχύτητα ροής", Float) = 0.7
        _Alpha   ("Αδιαφάνεια", Range(0,1)) = 0.78
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert
        #pragma target 3.0
        #include "GreystoneCommon.cginc"

        fixed4 _Deep, _Shallow;
        float4 _FlowDir;
        float  _FlowSpeed;
        float  _Alpha;

        struct Input
        {
            float3 worldPos;
            float3 viewDir;
        };

        void vert (inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float w = sin(wp.x * 0.8 + _Time.y * 1.3) * 0.025 + sin(wp.z * 0.55 - _Time.y * 1.7) * 0.03;
            v.vertex.y += w;
        }

        // Ύψος «κυματισμού» σε σημείο p (για υπολογισμό κάθετου με πεπερασμένες διαφορές)
        float ripple(float2 p)
        {
            float2 flow = _FlowDir.xy * _Time.y * _FlowSpeed;
            float a = gs_noise2(p * 0.9 + flow);
            float b = gs_noise2(p * 2.3 + flow * 1.6 + 5.1);
            float c = gs_noise2(p * 6.1 - flow * 0.3 + float2(_Time.y * 0.2, 0));
            return a * 0.5 + b * 0.35 + c * 0.15;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 p = IN.worldPos.xz;
            float e = 0.08;
            float h0 = ripple(p);
            float hx = ripple(p + float2(e, 0));
            float hz = ripple(p + float2(0, e));
            // Κάθετος σε tangent space (το επίπεδο του νερού είναι οριζόντιο)
            float3 n = normalize(float3((h0 - hx) * 2.2, (h0 - hz) * 2.2, 1.0));
            o.Normal = n;

            float fres = pow(1.0 - saturate(dot(normalize(IN.viewDir), float3(0, 0, 1))), 3.0);
            float3 col = lerp(_Shallow.rgb, _Deep.rgb, saturate(h0 * 1.3));
            // Αφρός σε κορυφές κυματισμών
            col += smoothstep(0.78, 0.95, h0) * 0.12;

            o.Albedo = col;
            o.Smoothness = 0.92 - _GS_Wetness * 0.25;   // η βροχή «θολώνει» την επιφάνεια
            o.Metallic = 0.0;
            o.Alpha = saturate(lerp(_Alpha, 0.96, fres));
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
