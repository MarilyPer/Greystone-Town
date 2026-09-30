// =====================================================================================
//  Greystone/Bird
//  ---------------------------------------------------------------------------------
//  Πουλιά σμήνους, ζωγραφισμένα με GPU instancing. Το χτύπημα των φτερών γίνεται
//  εξ ολοκλήρου στη GPU: οι κορυφές των φτερών (|x| μεγάλο) ανεβοκατεβαίνουν με
//  ημίτονο, με φάση που εξαρτάται από τη θέση του instance — έτσι κάθε πουλί
//  φτερουγίζει διαφορετικά χωρίς επιπλέον δεδομένα από τη CPU.
// =====================================================================================
Shader "Greystone/Bird"
{
    Properties
    {
        _Color ("Χρώμα", Color) = (0.08,0.08,0.09,1)
        _FlapSpeed ("Ταχύτητα φτερουγίσματος", Float) = 11
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing
        #include "GreystoneCommon.cginc"

        fixed4 _Color;
        float _FlapSpeed;

        struct Input { float dummy; };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 origin = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
            float phase = origin.y * 1.7 + (origin.x - origin.z) * 0.03;  // ομαλά μεταβαλλόμενη φάση ανά πουλί
            float flap = sin(_Time.y * _FlapSpeed + phase);
            v.vertex.y += flap * abs(v.vertex.x) * 0.7;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _Color.rgb;
            o.Smoothness = 0.2;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
