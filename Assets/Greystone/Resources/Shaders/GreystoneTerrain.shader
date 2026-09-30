// =====================================================================================
//  Greystone/Terrain
//  ---------------------------------------------------------------------------------
//  Χρωματισμός εδάφους εξ ολοκλήρου αναλυτικά (χωρίς υφές και χωρίς splat maps):
//
//   * Γρασίδι: δύο αποχρώσεις αναμεμειγμένες με fbm θόρυβο σε διάφορες κλίμακες.
//   * Βράχος: εμφανίζεται όπου η κλίση είναι μεγάλη (1 - normal.y).
//   * Λάσπη/άμμος: κοντά στη στάθμη του ποταμού (ανάλογα με το ύψος).
//   * Δρόμοι: υπολογίζεται η απόσταση κάθε pixel από έως 8 ευθύγραμμα τμήματα
//     (πίνακας _Roads που στέλνει ο C# κώδικας με SetVectorArray). Έτσι οι δρόμοι
//     έχουν καθαρά όρια ανεξάρτητα από την ανάλυση του πλέγματος (mesh).
//   * Πλατεία: κυκλική περιοχή με πλακόστρωτο (cobbles) μοτίβο Voronoi-τύπου.
//   * Χωράφια: ορθογώνια με αυλάκια (furrows) — λωρίδες του open-field system.
// =====================================================================================
Shader "Greystone/Terrain"
{
    Properties
    {
        _GrassA ("Γρασίδι Α", Color) = (0.26,0.38,0.14,1)
        _GrassB ("Γρασίδι Β", Color) = (0.42,0.47,0.2,1)
        _Rock   ("Βράχος", Color) = (0.47,0.45,0.41,1)
        _Mud    ("Λάσπη όχθης", Color) = (0.33,0.27,0.19,1)
        _Dirt   ("Χωμάτινος δρόμος", Color) = (0.49,0.41,0.3,1)
        _Cobble ("Πλακόστρωτο", Color) = (0.6,0.57,0.52,1)
        _Soil   ("Οργωμένο χώμα", Color) = (0.36,0.27,0.18,1)
        _WaterLevel ("Στάθμη νερού", Float) = -1.2
        _RoadHalfWidth ("Μισό πλάτος δρόμου", Float) = 2.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.5
        #include "GreystoneCommon.cginc"

        fixed4 _GrassA, _GrassB, _Rock, _Mud, _Dirt, _Cobble, _Soil;
        float  _WaterLevel;
        float  _RoadHalfWidth;

        // Δεδομένα διάταξης χωριού (από WorldLayout.cs)
        float4 _Roads[8];      // (x1, z1, x2, z2)
        float  _RoadCount;
        float4 _Plaza;         // (x, z, ακτίνα, -)
        float4 _Fields[4];     // (xmin, zmin, xmax, zmax)
        float  _FieldCount;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        // Απόσταση σημείου p από ευθύγραμμο τμήμα ab (κλασική προβολή σε τμήμα).
        float segDist(float2 p, float2 a, float2 b)
        {
            float2 pa = p - a;
            float2 ba = b - a;
            float h = saturate(dot(pa, ba) / dot(ba, ba));
            return length(pa - ba * h);
        }

        // Πλακόστρωτο: για κάθε pixel βρίσκουμε το πλησιέστερο «κέντρο» πέτρας (Voronoi 3x3).
        float3 cobbles(float2 p, out float gap)
        {
            float2 g = p * 2.2;
            float2 i = floor(g);
            float2 f = frac(g);
            float d1 = 8.0, d2 = 8.0;
            float2 bestCell = 0;
            [unroll] for (int y = -1; y <= 1; y++)
            [unroll] for (int x = -1; x <= 1; x++)
            {
                float2 c = float2(x, y);
                float2 o = float2(gs_hash21(i + c), gs_hash21(i + c + 13.7)) * 0.8 + 0.1;
                float d = length(c + o - f);
                if (d < d1) { d2 = d1; d1 = d; bestCell = i + c; }
                else if (d < d2) { d2 = d; }
            }
            gap = 1.0 - smoothstep(0.03, 0.12, d2 - d1);
            return _Cobble.rgb * lerp(0.75, 1.15, gs_hash21(bestCell));
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 p = IN.worldPos.xz;
            float slope = 1.0 - saturate(IN.worldNormal.y);

            // ---------------- Γρασίδι ----------------
            float n1 = gs_fbm2(p * 0.035);
            float n2 = gs_fbm2(p * 0.4);
            float n3 = gs_noise2(p * 2.7);
            float3 col = lerp(_GrassA.rgb, _GrassB.rgb, smoothstep(0.3, 0.75, n1));
            col *= lerp(0.82, 1.12, n2) * lerp(0.93, 1.05, n3);
            // Ξερά μπαλώματα στα ψηλά
            col = lerp(col, _GrassB.rgb * float3(1.15, 1.0, 0.75), smoothstep(18.0, 38.0, IN.worldPos.y) * 0.5);

            // ---------------- Βράχος σε απότομες κλίσεις ----------------
            float rockMask = smoothstep(0.28, 0.45, slope + (n2 - 0.5) * 0.15);
            float3 rock = _Rock.rgb * lerp(0.75, 1.1, gs_noise2(p * 1.3 + IN.worldPos.y));
            col = lerp(col, rock, rockMask);

            // ---------------- Λάσπη κοντά στο νερό ----------------
            float mudMask = 1.0 - smoothstep(_WaterLevel + 0.15, _WaterLevel + 0.9 + n2 * 0.4, IN.worldPos.y);
            col = lerp(col, _Mud.rgb * lerp(0.8, 1.1, n3), mudMask);

            // ---------------- Χωράφια (λωρίδες με αυλάκια) ----------------
            float fieldMask = 0.0;
            float3 fieldCol = _Soil.rgb;
            [unroll] for (int k = 0; k < 4; k++)
            {
                if (k < (int)_FieldCount)
                {
                    float4 r = _Fields[k];
                    float2 dmin = p - r.xy;
                    float2 dmax = r.zw - p;
                    float inside = min(min(dmin.x, dmin.y), min(dmax.x, dmax.y));
                    float m = smoothstep(-0.6, 0.8, inside + (n3 - 0.5) * 0.6);
                    // Λωρίδες 6 μέτρων (διαφορετικές οικογένειες) με ελαφρώς άλλο χρώμα
                    float strip = floor((p.x - r.x) / 6.0);
                    float furrow = 0.5 + 0.5 * sin(p.y * 7.0);
                    float3 c = _Soil.rgb * lerp(0.85, 1.12, gs_hash21(float2(strip, k))) * lerp(0.8, 1.05, furrow);
                    fieldCol = lerp(fieldCol, c, m);
                    fieldMask = max(fieldMask, m);
                }
            }
            col = lerp(col, fieldCol, fieldMask);

            // ---------------- Δρόμοι ----------------
            float rd = 1e5;
            [unroll] for (int j = 0; j < 8; j++)
            {
                if (j < (int)_RoadCount)
                    rd = min(rd, segDist(p, _Roads[j].xy, _Roads[j].zw));
            }
            float edgeNoise = (n3 - 0.5) * 0.9 + (n2 - 0.5) * 0.6;
            float roadMask = 1.0 - smoothstep(_RoadHalfWidth - 0.7 + edgeNoise, _RoadHalfWidth + 0.3 + edgeNoise, rd);
            // Αυλακιές από ρόδες κάρων στο κέντρο του δρόμου
            float rut = 1.0 - smoothstep(0.1, 0.35, abs(abs(rd) - 0.8));
            float3 dirt = _Dirt.rgb * lerp(0.8, 1.12, gs_noise2(p * 3.3)) * lerp(1.0, 0.85, rut);
            // Στις πολύ απότομες πλαγιές δεν ζωγραφίζουμε δρόμο
            roadMask *= 1.0 - rockMask;
            col = lerp(col, dirt, roadMask);

            // ---------------- Πλατεία (πλακόστρωτο) ----------------
            float pd = length(p - _Plaza.xy);
            float plazaMask = 1.0 - smoothstep(_Plaza.z - 0.8 + edgeNoise, _Plaza.z + 0.4 + edgeNoise, pd);
            float gap;
            float3 cob = cobbles(p, gap);
            cob = lerp(cob, _Dirt.rgb * 0.7, gap);
            col = lerp(col, cob, plazaMask);

            float smooth = lerp(0.08, 0.2, plazaMask);
            gs_apply_wetness(col, smooth, 1.0, IN.worldNormal.y);

            o.Albedo = col;
            o.Smoothness = smooth;
            o.Metallic = 0;
            o.Occlusion = 1.0 - gap * plazaMask * 0.5;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
