// Vẽ ô của bàn bằng SDF thay vì lấy mẫu texture.
//
// Vì sao cần: sprite hình tròn có dải chống răng cưa dày cố định TRONG texture, nên
// mép mượt hay không phụ thuộc vào tỉ lệ giữa cỡ texture và cỡ vẽ ra. Thu nhỏ thì
// mất mẫu (răng cưa), phóng to thì nhoè. Mipmap và trilinear chỉ giảm bớt chứ không
// khử được, vì gốc vấn đề là hình được LƯU thành pixel.
//
// Ở đây hình được TÍNH LẠI cho từng pixel màn hình: khoảng cách có dấu tới biên, rồi
// lấy độ phủ theo đúng bề rộng một pixel (fwidth). Nên mép sắc và mượt ở mọi cỡ, kể
// cả khi ô phóng to lúc chọn hay co lại lúc nổ.
//
// Đặt trong Resources/ để chắc chắn có mặt trong build — Shader.Find không bảo đảm
// shader được đóng gói nếu không có material nào tham chiếu tới nó từ scene.
Shader "ConnectPuzzle/UiRoundedSdf"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Bán kính bo, theo tỉ lệ của nửa cạnh ngắn. 0.5 = hình tròn.
        _Corner ("Corner", Range(0,0.5)) = 0.5

        // >0 thì vẽ VÒNG rỗng ruột, dày bằng ngần này (tỉ lệ nửa cạnh).
        _Ring ("Ring width", Range(0,0.5)) = 0

        // 1 = tự vẽ chóa sáng + tối đáy như bubble của bản HTML.
        _Sheen ("Sheen", Range(0,1)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _Corner;
            float _Ring;
            float _Sheen;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPos = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            // Khoảng cách có dấu tới biên hình chữ nhật bo góc, trong hệ toạ độ tâm.
            float sdRoundedBox(float2 p, float2 halfSize, float r)
            {
                float2 q = abs(p) - (halfSize - r);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // UV 0..1 -> toạ độ tâm -0.5..0.5
                float2 p = IN.texcoord - 0.5;
                float2 halfSize = float2(0.5, 0.5);
                float r = min(_Corner, 0.5);

                float d = sdRoundedBox(p, halfSize, r);

                // fwidth = hình chiếu của MỘT pixel màn hình lên không gian UV. Chia cho
                // nó tức là đo độ phủ theo đúng bề rộng một pixel, nên mép luôn dày đúng
                // một pixel dù ô đang to hay nhỏ — đây chính là chỗ khử răng cưa.
                float aa = fwidth(d) * 0.5;
                float alpha = 1.0 - smoothstep(-aa, aa, d);

                if (_Ring > 0.0)
                {
                    float inner = sdRoundedBox(p, halfSize - _Ring, max(r - _Ring, 0.0));
                    alpha *= smoothstep(-aa, aa, inner);
                }

                fixed4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                fixed4 col = tex * IN.color;

                if (_Sheen > 0.0)
                {
                    // chóa sáng ở 32%/72% và dải tối sát đáy trong — hai lớp của .bub
                    float2 hi = IN.texcoord - float2(0.32, 0.72);
                    float highlight = saturate(1.0 - length(hi) / 0.42);
                    col.rgb = lerp(col.rgb, float3(1, 1, 1), highlight * highlight * 0.45);

                    float depth = saturate((0.30 - IN.texcoord.y) / 0.30);
                    col.rgb *= lerp(1.0, 0.72, depth * depth);
                }

                col.a *= alpha;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPos.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
