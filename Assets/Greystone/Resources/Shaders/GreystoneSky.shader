// =====================================================================================
//  Greystone/Sky  (Skybox)
//  ---------------------------------------------------------------------------------
//  Διαδικαστικός ουρανός με κύκλο ημέρας-νύχτας:
//   * Βαθμίδα χρώματος ζενίθ -> ορίζοντας (χρώματα από το SkySystem.cs ανάλογα με το
//     ύψος του ήλιου).
//   * Δίσκος ήλιου με «φωτοστέφανο» (Mie-like glow) και δίσκος σελήνης.
//   * Αστέρια: ψευδοτυχαία σημεία σε κελιά της σφαίρας κατεύθυνσης, με τρεμόπαιγμα.
//   * Σύννεφα: fbm θόρυβος προβεβλημένος σε νοητό επίπεδο ψηλά στον ουρανό, που
//     μετακινείται με τον άνεμο (_CloudOffset). Η κάλυψη (_CloudCover) αλλάζει με τον καιρό.
// =====================================================================================
Shader "Greystone/Sky"
{
    Properties
    {
        _ZenithColor  ("Ζενίθ", Color) = (0.22,0.45,0.85,1)
        _HorizonColor ("Ορίζοντας", Color) = (0.65,0.78,0.92,1)
        _GroundColor  ("Κάτω ημισφαίριο", Color) = (0.3,0.32,0.3,1)
        _SunDir       ("Κατεύθυνση ήλιου", Vector) = (0,1,0,0)
        _MoonDir      ("Κατεύθυνση σελήνης", Vector) = (0,-1,0,0)
        _SunColor     ("Χρώμα ήλιου", Color) = (1,0.95,0.85,1)
        _StarIntensity("Ένταση αστεριών", Range(0,1)) = 0
        _CloudCover   ("Κάλυψη νεφών", Range(0,1)) = 0.35
        _CloudColor   ("Χρώμα νεφών", Color) = (1,1,1,1)
        _CloudOffset  ("Μετατόπιση νεφών", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "GreystoneCommon.cginc"

            fixed4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor, _CloudColor;
            float4 _SunDir, _MoonDir, _CloudOffset;
            float  _StarIntensity, _CloudCover;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;   // το skybox mesh είναι κεντραρισμένο στην κάμερα
                return o;
            }

            float3 stars(float3 d)
            {
                float3 p = d * 220.0;
                float3 id = floor(p);
                float h = gs_hash31(id);
                float3 f = frac(p) - 0.5;
                float star = step(0.9965, h) * smoothstep(0.32, 0.0, length(f));
                float tw = 0.6 + 0.4 * sin(_Time.y * (2.0 + h * 5.0) + h * 40.0);
                float3 tint = lerp(float3(0.8, 0.85, 1.0), float3(1.0, 0.9, 0.75), frac(h * 17.0));
                return star * tw * tint * 1.6;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = d.y;
                float3 sunD = normalize(_SunDir.xyz);
                float3 moonD = normalize(_MoonDir.xyz);

                // ----- Βασική βαθμίδα -----
                float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(up), 0.45));
                if (up < 0.0)
                    col = lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-up * 5.0));

                // ----- Ήλιος -----
                float sd = dot(d, sunD);
                float sunUp = saturate(sunD.y * 4.0 + 0.3);
                float horizonGlow = pow(saturate(sd), 6.0) * (1.0 - saturate(up * 2.0)) * 0.55;
                col += _SunColor.rgb * (pow(saturate(sd), 180.0) * 0.9 + horizonGlow) * sunUp;
                float disk = smoothstep(0.99955, 0.9997, sd);
                col = lerp(col, _SunColor.rgb * 4.0, disk * sunUp);

                // ----- Αστέρια (μόνο πάνω από τον ορίζοντα) -----
                float starFade = saturate(up * 4.0) * _StarIntensity * (1.0 - _CloudCover * 0.9);
                col += stars(d) * starFade;

                // ----- Σελήνη -----
                float md = dot(d, moonD);
                float moonDisk = smoothstep(0.99925, 0.9994, md);
                float moonUp = saturate(moonD.y * 5.0);
                float crater = gs_noise2(d.xz * 900.0) * 0.25;
                col = lerp(col, float3(0.85, 0.87, 0.92) * (0.85 - crater), moonDisk * moonUp);
                col += float3(0.25, 0.3, 0.4) * pow(saturate(md), 90.0) * moonUp * _StarIntensity * 0.4;

                // ----- Σύννεφα (προβολή σε επίπεδο) -----
                if (up > 0.0)
                {
                    float2 uv = d.xz / (up + 0.18) * 1.6 + _CloudOffset.xy;
                    float n = gs_fbm2(uv * 0.9) * 0.65 + gs_fbm2(uv * 2.7 + 3.1) * 0.35;
                    float cover = smoothstep(1.0 - _CloudCover, 1.0 - _CloudCover + 0.28, n);
                    cover *= smoothstep(0.0, 0.12, up);         // σβήνουν προς τον ορίζοντα
                    // Φωτισμός νεφών: φωτεινότερα προς τον ήλιο, σκουρότερα στη βάση
                    float lit = 0.75 + 0.35 * pow(saturate(sd), 4.0);
                    float3 cc = _CloudColor.rgb * lit * lerp(1.0, 0.7, saturate(n - 0.55) * 2.0);
                    col = lerp(col, cc, cover * 0.92);
                }
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
