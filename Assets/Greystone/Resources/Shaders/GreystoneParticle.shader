// =====================================================================================
//  Greystone/Particle
//  ---------------------------------------------------------------------------------
//  Απλός unlit shader σωματιδίων (καπνός, φωτιά, σπίθες, βροχή).
//  Το «μαλακό» στρογγυλό σχήμα υπολογίζεται από τις συντεταγμένες UV (απόσταση από το
//  κέντρο), άρα δεν απαιτείται υφή. Ο τρόπος ανάμειξης (alpha ή additive) ορίζεται από
//  τις ιδιότητες _SrcBlend/_DstBlend, ώστε ένας shader να καλύπτει όλες τις περιπτώσεις.
//  _Streak = 1 -> μακρόστενο σχήμα (σταγόνες βροχής).
// =====================================================================================
Shader "Greystone/Particle"
{
    Properties
    {
        _Color ("Χρώμα", Color) = (1,1,1,1)
        _Softness ("Απαλότητα άκρων", Range(0.01,1)) = 0.6
        _Streak ("Μακρόστενο (0/1)", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Softness;
            float _Streak;

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 c = i.uv - 0.5;
                c.x *= lerp(1.0, 6.0, _Streak);       // στενότερο στον x για σταγόνες
                float d = length(c) * 2.0;
                float a = 1.0 - smoothstep(1.0 - _Softness, 1.0, d);
                fixed4 col = i.color;
                col.a *= a;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
