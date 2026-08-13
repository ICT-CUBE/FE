using UnityEngine;

namespace ARZone
{
    // Figma 디자인 토큰 v1을 코드로 옮긴 것.
    // 색은 Figma가 준 0~1 값 그대로 → Unity Color 와 동일하므로 변환 없음.
    // 옆의 hex 는 사람이 읽기 위한 주석.
    //
    // 모드가 2개다: Default(화면 UI) / AR(패스스루·카메라 위).
    //   DesignTokens.Mode 를 바꾸면 semantic 토큰이 그 모드 값으로 나온다.
    //   Primitive / Radius / Spacing 은 모드와 무관(양쪽 동일).
    //
    // 사용 예:
    //   DesignTokens.Mode = DesignTokens.TokenMode.AR;
    //   img.color = DesignTokens.Action.Primary;
    //   panel.color = DesignTokens.Bg.Glass;   // 모드에 따라 자동으로 달라짐
    public static partial class DesignTokens
    {
        public enum TokenMode { Default, AR }

        // 현재 모드. 폰 화면 UI = Default, AR 오버레이 = AR 로 설정해서 쓴다.
        public static TokenMode Mode = TokenMode.Default;

        static Color Pick(Color def, Color ar) => Mode == TokenMode.AR ? ar : def;

        // ── Primitive (모드 무관) ─────────────────────────────
        public static class Primitive
        {
            public static class Violet
            {
                public static readonly Color _100 = new Color(0.929412f, 0.913725f, 0.996078f, 1f); // #EDE9FE
                public static readonly Color _200 = new Color(0.866667f, 0.839216f, 0.996078f, 1f); // #DDD6FE
                public static readonly Color _300 = new Color(0.768627f, 0.709804f, 0.992157f, 1f); // #C4B5FD
                public static readonly Color _400 = new Color(0.654902f, 0.545098f, 0.980392f, 1f); // #A78BFA
                public static readonly Color _500 = new Color(0.658824f, 0.333333f, 0.968627f, 1f); // #A855F7
                public static readonly Color _600 = new Color(0.486275f, 0.227451f, 0.929412f, 1f); // #7C3AED
                public static readonly Color _700 = new Color(0.427451f, 0.156863f, 0.850980f, 1f); // #6D28D9
                public static readonly Color _800 = new Color(0.227451f, 0.168627f, 0.403922f, 1f); // #3A2B67
                public static readonly Color _900 = new Color(0.101961f, 0.078431f, 0.192157f, 1f); // #1A1431
                public static readonly Color _950 = new Color(0.070588f, 0.050980f, 0.133333f, 1f); // #120D22
            }

            public static class Neutral
            {
                public static readonly Color _0   = new Color(1f, 1f, 1f, 1f);                        // #FFFFFF
                public static readonly Color _50  = new Color(0.956863f, 0.956863f, 0.956863f, 1f);   // #F4F4F4
                public static readonly Color _100 = new Color(0.827451f, 0.835294f, 0.866667f, 1f);   // #D3D5DD
                public static readonly Color _200 = new Color(0.611765f, 0.627451f, 0.678431f, 1f);   // #9CA0AD
                public static readonly Color _400 = new Color(0.447059f, 0.470588f, 0.525490f, 1f);   // #727886
                public static readonly Color _600 = new Color(0.141176f, 0.156863f, 0.192157f, 1f);   // #242831
                public static readonly Color _800 = new Color(0.094118f, 0.101961f, 0.125490f, 1f);   // #181A20
                public static readonly Color _900 = new Color(0.059294f, 0.065882f, 0.080706f, 1f);   // #0F1115
                public static readonly Color _950 = new Color(0.019487f, 0.019487f, 0.020513f, 1f);   // #050505
            }

            public static class Cyan
            {
                public static readonly Color _200 = new Color(0.647059f, 0.952941f, 0.988235f, 1f); // #A5F3FC
                public static readonly Color _300 = new Color(0.537255f, 0.941176f, 0.972549f, 1f); // #89F0F8
                public static readonly Color _400 = new Color(0.435294f, 0.909804f, 0.960784f, 1f); // #6FE8F5
                public static readonly Color _500 = new Color(0.133333f, 0.827451f, 0.933333f, 1f); // #22D3EE
                public static readonly Color _600 = new Color(0.054902f, 0.607843f, 0.690196f, 1f); // #0E9BB0
            }

            public static class Status
            {
                public static readonly Color Red   = new Color(1f, 0.356863f, 0.435294f, 1f);        // #FF5B6F
                public static readonly Color Amber = new Color(1f, 0.721569f, 0.301961f, 1f);        // #FFB84D
                public static readonly Color Green = new Color(0.239216f, 0.850980f, 0.639216f, 1f); // #3DD9A3
            }
        }

        // ── Semantic: bg (모드별 다름) ───────────────────────
        public static class Bg
        {
            // Default: neutral/950  |  AR: 완전 투명 (배경은 패스스루)
            public static Color Base => Pick(
                Primitive.Neutral._950,
                new Color(1f, 1f, 1f, 0f));

            // Default: neutral/900  |  AR: neutral/200 @20%
            public static Color Surface => Pick(
                Primitive.Neutral._900,
                new Color(0.611765f, 0.627451f, 0.678431f, 0.2f));

            // Default: #0F0D17 @50%  |  AR: neutral/200 @20%
            public static Color Glass => Pick(
                new Color(0.058824f, 0.050980f, 0.090196f, 0.5f),
                new Color(0.611765f, 0.627451f, 0.678431f, 0.2f));

            // Default: neutral/800  |  AR: neutral/400 @40%
            public static Color HighSurface => Pick(
                Primitive.Neutral._800,
                new Color(0.447059f, 0.470588f, 0.525490f, 0.4f));

            // Default: neutral/400  |  AR: neutral/600 @60%
            public static Color LowSurface => Pick(
                Primitive.Neutral._400,
                new Color(0.141176f, 0.156863f, 0.192157f, 0.6f));
        }

        // ── Semantic: action ────────────────────────────────
        public static class Action
        {
            // 양쪽 동일: violet/500
            public static Color Primary => Primitive.Violet._500;

            // 양쪽 동일: violet/600
            public static Color Pressed => Primitive.Violet._600;

            // Default: cyan/400  |  AR: cyan/600
            public static Color Point => Pick(Primitive.Cyan._400, Primitive.Cyan._600);
        }

        // ── Semantic: text ──────────────────────────────────
        public static class Text
        {
            // 양쪽 동일: neutral/0
            public static Color Primary => Primitive.Neutral._0;

            // Default: neutral/100  |  AR: neutral/50
            public static Color Secondary => Pick(Primitive.Neutral._100, Primitive.Neutral._50);

            // Default: neutral/400  |  AR: neutral/100
            public static Color Tertiary => Pick(Primitive.Neutral._400, Primitive.Neutral._100);

            // 양쪽 동일: neutral/950
            public static Color Inverse => Primitive.Neutral._950;

            // Default: violet/500(action/primary)  |  AR: violet/700
            public static Color Point => Pick(Primitive.Violet._500, Primitive.Violet._700);

            // action/point 를 그대로 따름 (Default cyan/400, AR cyan/600)
            public static Color Point2 => Action.Point;
        }

        // ── Semantic: stroke ────────────────────────────────
        public static class Stroke
        {
            public static Color Strong     => Primitive.Neutral._200; // 양쪽 동일
            public static Color Medium     => Primitive.Neutral._400; // 양쪽 동일
            public static Color Regular    => Primitive.Neutral._600; // 양쪽 동일
            public static Color Light      => Primitive.Neutral._600; // 양쪽 동일
            public static Color Ultralight => Primitive.Neutral._800; // 양쪽 동일
        }

        // ── Radius (px, 모드 무관) ───────────────────────────
        public static class Radius
        {
            public const float Sm   = 8f;
            public const float Md   = 12f;
            public const float Lg   = 16f;
            public const float Xl   = 24f;
            public const float Pill = 99f;
        }

        // ── Spacing (px, 모드 무관) ──────────────────────────
        public static class Spacing
        {
            public const float _1  = 4f;
            public const float _2  = 8f;
            public const float _3  = 12f;
            public const float _4  = 16f;
            public const float _5  = 20f;
            public const float _6  = 24f;
            public const float _7  = 28f;
            public const float _8  = 32f;
            public const float _9  = 40f;
            public const float _10 = 48f;
        }
    }
}
