// =====================================================================================
//  Greystone/Text3D
//  ---------------------------------------------------------------------------------
//  Shader για τα κείμενα TextMesh στις πινακίδες. Ο προεπιλεγμένος shader των
//  γραμματοσειρών του Unity σχεδιάζεται «πάνω» από όλα (αγνοεί το βάθος), οπότε τα
//  γράμματα θα φαίνονταν μέσα από τοίχους. Εδώ κάνουμε κανονικό έλεγχο βάθους (ZTest)
//  και απόρριψη πίσω όψης, ώστε το κείμενο να συμπεριφέρεται σαν φυσικό αντικείμενο.
// =====================================================================================
Shader "Greystone/Text3D"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Χρώμα κειμένου", Color) = (0.12,0.08,0.05,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Back
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = i.color;
                col.a *= tex2D(_MainTex, i.uv).a;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
