// =====================================================================================
//  Greystone/Stone
//  ---------------------------------------------------------------------------------
//  Διαδικαστική λιθοδομή (ashlar) τύπου Cotswold stone.
//
//  Πώς δουλεύει:
//   1. Τριεπίπεδη (triplanar) προβολή: ανάλογα με το ποιος άξονας κυριαρχεί στο κάθετο
//      διάνυσμα (normal), χρησιμοποιούμε ως 2D συντεταγμένες το (x,y), (z,y) ή (x,z)
//      της θέσης στον κόσμο. Έτσι το μοτίβο δεν «τεντώνεται» όπως κι αν κλιμακωθεί ο κύβος.
//   2. Σειρές λίθων με εναλλασσόμενη μετατόπιση (running bond), όπως στις πραγματικές
//      τοιχοποιίες. Κάθε λίθος παίρνει τυχαία απόχρωση μέσω hash του κελιού του.
//   3. Οι αρμοί (κονίαμα) υπολογίζονται από την απόσταση από τα όρια του κελιού.
//   4. Καιρικές φθορές: σκουρόχρωμες κηλίδες (fbm) στο κάτω μέρος των τοίχων.
// =====================================================================================
Shader "Greystone/Stone"
{
    Properties
    {
        _Color      ("Χρώμα λίθου", Color) = (0.72,0.66,0.55,1)
        _Color2     ("Δεύτερο χρώμα λίθου", Color) = (0.62,0.6,0.56,1)
        _Mortar     ("Χρώμα κονιάματος", Color) = (0.55,0.53,0.5,1)
        _BlockSize  ("Μέγεθος λίθου (πλάτος, ύψος)", Vector) = (0.62,0.28,0,0)
        _Glossiness ("Λείανση", Range(0,1)) = 0.12
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
        fixed4 _Mortar;
        float4 _BlockSize;
        half   _Glossiness;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        // Επιστρέφει χρώμα λίθου για 2D συντεταγμένες uv και βάρος αρμού (0..1).
        float3 ashlar(float2 uv, out float mortar)
        {
            float2 bs = _BlockSize.xy;
            float row = floor(uv.y / bs.y);
            // Κάθε δεύτερη σειρά μετατοπίζεται κατά μισό λίθο (running bond).
            // Μικρή τυχαία μεταβολή πλάτους ανά σειρά για πιο «χειροποίητη» όψη.
            float rowRand = gs_hash21(float2(row, 7.0));
            float w = bs.x * lerp(0.8, 1.3, rowRand);
            float x = uv.x + fmod(abs(row), 2.0) * w * 0.5 + rowRand * 3.0;
            float2 cell = float2(floor(x / w), row);
            float2 f = float2(frac(x / w), frac(uv.y / bs.y));
            float edge = min(min(f.x, 1.0 - f.x) * w, min(f.y, 1.0 - f.y) * bs.y);
            mortar = 1.0 - smoothstep(0.008, 0.022, edge);

            float h = gs_hash21(cell);
            float3 c = lerp(_Color.rgb, _Color2.rgb, h);
            c *= lerp(0.85, 1.08, gs_hash21(cell + 31.7));
            return c;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = abs(IN.worldNormal);
            float2 uv;
            if (n.y > n.x && n.y > n.z)      uv = IN.worldPos.xz;           // οριζόντια: πλάκες
            else if (n.x > n.z)              uv = IN.worldPos.zy;           // τοίχος προς ±x
            else                             uv = IN.worldPos.xy;           // τοίχος προς ±z

            float mortar;
            float3 stone = ashlar(uv, mortar);

            // Λεπτομέρεια επιφάνειας λίθου (πόροι του ωολιθικού ασβεστόλιθου)
            float grain = gs_noise2(uv * 23.0) * 0.5 + gs_noise2(uv * 61.0) * 0.5;
            stone *= lerp(0.9, 1.06, grain);

            // Βρωμιά/λειχήνες προς τη βάση των τοίχων
            float grime = gs_fbm2(IN.worldPos.xz * 0.35 + IN.worldPos.y * 0.2);
            float lowWall = 1.0 - saturate(IN.worldPos.y * 0.25 - gs_noise2(IN.worldPos.xz) * 0.3);
            stone *= lerp(1.0, 0.72, saturate(grime * lowWall));

            float3 albedo = lerp(stone, _Mortar.rgb, mortar);
            float smooth = _Glossiness * (1.0 - mortar * 0.6);
            gs_apply_wetness(albedo, smooth, 1.0, IN.worldNormal.y);

            o.Albedo = albedo;
            o.Smoothness = smooth;
            o.Metallic = 0;
            // Οι αρμοί είναι εσοχές: λίγη «απόφραξη» (occlusion) τους σκουραίνει ρεαλιστικά.
            o.Occlusion = 1.0 - mortar * 0.45;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
