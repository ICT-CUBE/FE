using UnityEngine;
using TMPro;

namespace ARZone
{
    // Figma Typography (Pretendard) 를 코드로.
    // 한 스타일 = 크기 + 굵기 + 행간(%) + 자간(%).
    //
    // 폰트: Resources/Fonts/ 에 있는 SDF 4종을 이름으로 자동 로드 (드래그 불필요).
    //
    // 사용:
    //   DesignTokens.Type.Headline1.Apply(myLabel);
    public struct TypeStyle
    {
        public float Size;
        public FontWeight Weight;
        public float LineHeightPct;
        public float LetterPct;

        public TypeStyle(float size, FontWeight weight, float lineHeightPct, float letterPct)
        {
            Size = size; Weight = weight; LineHeightPct = lineHeightPct; LetterPct = letterPct;
        }

        public void Apply(TMP_Text t)
        {
            if (t == null) return;
            var font = Fonts.ForWeight(Weight);
            if (font != null) t.font = font;
            t.fontSize = Size;
            t.fontWeight = Weight;
            t.lineSpacing = LineSpacingFor(t.font);
            t.characterSpacing = LetterPct;         // TMP 근사 (시각 확인)
        }

        // TMP 의 줄 간격 = (ascentLine - descentLine + lineSpacing) × (fontSize / pointSize) 이고
        // lineSpacing 단위는 font unit 이다. 행간(%) 을 그대로 넣으면 폰트 기본 행간만큼 더 벌어진다
        // (Pretendard 90pt 기준 107.36 → Body2 140% 가 22.4 여야 하는데 26.2 가 됐다).
        float LineSpacingFor(TMP_FontAsset f)
        {
            if (f == null) return LineHeightPct - 100f;
            var fi = f.faceInfo;
            if (fi.pointSize <= 0) return LineHeightPct - 100f;
            return LineHeightPct / 100f * fi.pointSize - (fi.ascentLine - fi.descentLine);
        }
    }

    // Pretendard SDF 폰트를 Resources 에서 한 번만 로드해 캐싱.
    public static class Fonts
    {
        const string Dir = "Fonts/";
        static TMP_FontAsset regular, medium, semibold, bold;
        static bool loaded;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            regular  = Resources.Load<TMP_FontAsset>(Dir + "Pretendard-Regular SDF");
            medium   = Resources.Load<TMP_FontAsset>(Dir + "Pretendard-Medium SDF");
            semibold = Resources.Load<TMP_FontAsset>(Dir + "Pretendard-SemiBold SDF");
            bold     = Resources.Load<TMP_FontAsset>(Dir + "Pretendard-Bold SDF");
            if (regular == null)
                Debug.LogWarning("[Fonts] Resources/Fonts 에서 Pretendard SDF 를 못 찾음 — 이름 확인");
        }

        public static TMP_FontAsset ForWeight(FontWeight w)
        {
            Load();
            if (w >= FontWeight.Bold)     return bold     ?? regular;
            if (w >= FontWeight.SemiBold) return semibold ?? regular;
            if (w >= FontWeight.Medium)   return medium   ?? regular;
            return regular;
        }
    }

    public static partial class DesignTokens
    {
        public static class Type
        {
            public static readonly TypeStyle Highlight1 = new TypeStyle(40, FontWeight.Bold,     130, -2.5f);
            public static readonly TypeStyle Highlight2 = new TypeStyle(32, FontWeight.Bold,     130, -2.5f);
            public static readonly TypeStyle Highlight3 = new TypeStyle(28, FontWeight.Bold,     135, -2f);

            public static readonly TypeStyle Headline1 = new TypeStyle(24, FontWeight.SemiBold, 140, -2f);
            public static readonly TypeStyle Headline2 = new TypeStyle(20, FontWeight.SemiBold, 140, -1.5f);
            public static readonly TypeStyle Headline3 = new TypeStyle(18, FontWeight.SemiBold, 140, -1.5f);

            public static readonly TypeStyle Title1 = new TypeStyle(18, FontWeight.Bold,     130, -1f);
            public static readonly TypeStyle Title2 = new TypeStyle(18, FontWeight.SemiBold, 130, -1f);

            public static readonly TypeStyle Body1 = new TypeStyle(16, FontWeight.SemiBold, 140, -1f);
            public static readonly TypeStyle Body2 = new TypeStyle(16, FontWeight.Medium,   140, -1f);
            public static readonly TypeStyle Body3 = new TypeStyle(14, FontWeight.Medium,   150, -0.5f);
            public static readonly TypeStyle Body4 = new TypeStyle(14, FontWeight.Regular,  140, -0.5f);

            public static readonly TypeStyle Caption1 = new TypeStyle(13, FontWeight.Regular, 140, 0f);
            public static readonly TypeStyle Caption2 = new TypeStyle(13, FontWeight.Regular, 140, 0f);

            public static readonly TypeStyle SpecialLarge = new TypeStyle(24, FontWeight.Bold, 140, 0f);
            public static readonly TypeStyle SpecialSmall = new TypeStyle(16, FontWeight.Bold, 140, 0f);

            public static readonly TypeStyle Alert1 = new TypeStyle(12, FontWeight.SemiBold, 140, 0f);
        }
    }
}