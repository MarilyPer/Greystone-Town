// =====================================================================================
//  Greystone/Sky  (Skybox, συμβατό με URP)
//  ---------------------------------------------------------------------------------
//  Διαδικαστικός ουρανός για τον κύκλο μέρας/νύχτας. Όλες οι τιμές του δίνονται από το
//  DayNightCycle.cs σε κάθε frame:
//   * βαθμίδα χρώματος ζενίθ -> ορίζοντας,
//   * δίσκος ήλιου με λάμψη γύρω του, δίσκος σελήνης,
//   * αστέρια που τρεμοπαίζουν (μόνο τη νύχτα),
//   * σύννεφα από θόρυβο fBm, προβεβλημένα σε νοητό επίπεδο ψηλά, που κινούνται αργά.
//  Είναι απλός unlit shader (vertex/fragment) άρα λειτουργεί και στο URP και στο Built-in.
// =====================================================================================
Shader "Greystone/Sky"
{
    Properties
    {
        _ZenithColor  ("Ζενίθ", Color) = (0.2,0.42,0.82,1)
        _HorizonColor ("Ορίζοντας", Color) = (0.66,0.78,0.9,1)
        _GroundColor  ("Κάτω ημισφαίριο", Color) = (0.3,0.32,0.3,1)
        _SunDir       ("Κατεύθυνση ήλιου", Vector) = (0,1,0,0)
        _MoonDir      ("Κατεύθυνση σελήνης", Vector) = (0,-1,0,0)
        _SunColor     ("Χρώμα ήλιου", Color) = (1,0.95,0.85,1)
        _StarIntensity("Ένταση αστεριών", Range(0,1)) = 0
        _CloudCover   ("Κάλυψη νεφών", Range(0,1)) = 0.4
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
            #include "UnityCG.cginc"

            fixed4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor, _CloudColor;
            float4 _SunDir, _MoonDir, _CloudOffset;
            float  _StarIntensity, _CloudCover;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            // ---- Θόρυβος (hash / value noise / fBm) ----
            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float hash31(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float noise2(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x), lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), u.x), u.y);
            }
            float fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                [unroll] for (int k = 0; k < 4; k++) { v += a * noise2(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v / 0.9375;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float3 stars(float3 d)
            {
                float3 p = d * 220.0;
                float3 id = floor(p);
                float h = hash31(id);
                float3 f = frac(p) - 0.5;
                float star = step(0.9965, h) * smoothstep(0.32, 0.0, length(f));
                float tw = 0.6 + 0.4 * sin(_Time.y * (2.0 + h * 5.0) + h * 40.0);
                return star * tw * lerp(float3(0.8, 0.85, 1.0), float3(1.0, 0.9, 0.75), frac(h * 17.0)) * 1.6;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = d.y;
                float3 sunD = normalize(_SunDir.xyz);
                float3 moonD = normalize(_MoonDir.xyz);

                float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(up), 0.45));
                if (up < 0.0) col = lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-up * 5.0));

                // Ήλιος
                float sd = dot(d, sunD);
                float sunUp = saturate(sunD.y * 4.0 + 0.3);
                col += _SunColor.rgb * (pow(saturate(sd), 180.0) * 0.9 + pow(saturate(sd), 6.0) * (1.0 - saturate(up * 2.0)) * 0.55) * sunUp;
                col = lerp(col, _SunColor.rgb * 4.0, smoothstep(0.99955, 0.9997, sd) * sunUp);

                // Αστέρια
                col += stars(d) * saturate(up * 4.0) * _StarIntensity * (1.0 - _CloudCover * 0.8);

                // Σελήνη
                float md = dot(d, moonD);
                float moonUp = saturate(moonD.y * 5.0);
                col = lerp(col, float3(0.85, 0.87, 0.92) * (0.85 - noise2(d.xz * 900.0) * 0.25), smoothstep(0.99925, 0.9994, md) * moonUp);
                col += float3(0.25, 0.3, 0.4) * pow(saturate(md), 90.0) * moonUp * _StarIntensity * 0.4;

                // Σύννεφα
                if (up > 0.0)
                {
                    float2 uv = d.xz / (up + 0.18) * 1.6 + _CloudOffset.xy;
                    float n = fbm(uv * 0.9) * 0.65 + fbm(uv * 2.7 + 3.1) * 0.35;
                    float cover = smoothstep(1.0 - _CloudCover, 1.0 - _CloudCover + 0.28, n) * smoothstep(0.0, 0.12, up);
                    float3 cc = _CloudColor.rgb * (0.75 + 0.35 * pow(saturate(sd), 4.0)) * lerp(1.0, 0.7, saturate(n - 0.55) * 2.0);
                    col = lerp(col, cc, cover * 0.9);
                }
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
