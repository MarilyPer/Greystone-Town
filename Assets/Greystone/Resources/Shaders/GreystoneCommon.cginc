// =====================================================================================
//  GreystoneCommon.cginc
//  ---------------------------------------------------------------------------------
//  Κοινές βοηθητικές συναρτήσεις για όλους τους shaders του Greystone.
//
//  * Θόρυβος (hash / value noise / fbm) υπολογισμένος εξ ολοκλήρου στη GPU, ώστε
//    να μη χρειάζονται εξωτερικές υφές (textures): όλη η «υφή» του κόσμου είναι
//    διαδικαστική (procedural).
//  * Καθολικές (global) μεταβλητές shader που ορίζονται ΜΙΑ φορά ανά frame από τον
//    C# κώδικα με Shader.SetGlobalFloat/Vector (π.χ. άνεμος, υγρασία, νυχτερινή
//    λάμψη). Αυτή είναι τεχνική βελτιστοποίησης: αντί να αλλάζουμε ιδιότητες σε
//    εκατοντάδες υλικά, αλλάζουμε μία τιμή που τη βλέπουν όλα.
// =====================================================================================
#ifndef GREYSTONE_COMMON_INCLUDED
#define GREYSTONE_COMMON_INCLUDED

// ---- Καθολικές παράμετροι κόσμου (ορίζονται από WeatherSystem / SkySystem) ----------
float  _GS_Wetness;        // 0 = στεγνό, 1 = μούσκεμα (βροχή)
float  _GS_NightGlow;      // 0 = μέρα, 1 = νύχτα (ανάβουν τα παράθυρα/φανάρια)
float  _GS_WindStrength;   // ένταση ανέμου 0..1.5
float4 _GS_WindDir;        // κατεύθυνση ανέμου στο επίπεδο xz (xy του float4)

// ---- Hash: ψευδοτυχαίος αριθμός 0..1 από διάνυσμα --------------------------------
float gs_hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float gs_hash31(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// ---- Value noise 2D με ομαλή (smoothstep) παρεμβολή ---------------------------------
float gs_noise2(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = gs_hash21(i);
    float b = gs_hash21(i + float2(1, 0));
    float c = gs_hash21(i + float2(0, 1));
    float d = gs_hash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// ---- Value noise 3D --------------------------------------------------------------------
float gs_noise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = gs_hash31(i);
    float n100 = gs_hash31(i + float3(1, 0, 0));
    float n010 = gs_hash31(i + float3(0, 1, 0));
    float n110 = gs_hash31(i + float3(1, 1, 0));
    float n001 = gs_hash31(i + float3(0, 0, 1));
    float n101 = gs_hash31(i + float3(1, 0, 1));
    float n011 = gs_hash31(i + float3(0, 1, 1));
    float n111 = gs_hash31(i + float3(1, 1, 1));
    float x00 = lerp(n000, n100, u.x);
    float x10 = lerp(n010, n110, u.x);
    float x01 = lerp(n001, n101, u.x);
    float x11 = lerp(n011, n111, u.x);
    return lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z);
}

// ---- Fractal Brownian Motion: άθροισμα οκτάβων θορύβου (3 οκτάβες) ---------------------
float gs_fbm2(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    [unroll] for (int k = 0; k < 3; k++)
    {
        v += a * gs_noise2(p);
        p = p * 2.03 + 17.1;
        a *= 0.5;
    }
    return v / 0.875;
}

// ---- Εφαρμογή «βρεγμένης» όψης: σκουραίνει το χρώμα και αυξάνει τη γυαλάδα ------------
void gs_apply_wetness(inout float3 albedo, inout float smoothness, float response, float upFacing)
{
    // Οι οριζόντιες επιφάνειες βρέχονται περισσότερο από τις κατακόρυφες.
    float w = saturate(_GS_Wetness * response * lerp(0.55, 1.0, saturate(upFacing)));
    albedo *= lerp(1.0, 0.62, w);
    smoothness = lerp(smoothness, 0.82, w);
}

#endif
