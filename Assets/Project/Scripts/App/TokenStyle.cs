using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // Image 또는 TMP_Text 에 붙여서, 인스펙터에서 토큰을 골라 자동 적용.
    // 색을 손으로 안 넣고 디자인 토큰과 항상 일치시킨다.
    [ExecuteAlways]
    public class TokenStyle : MonoBehaviour
    {
        public enum ColorToken
        {
            None,
            BgBase, BgSurface, BgGlass, BgHighSurface, BgLowSurface,
            ActionPrimary, ActionPressed, ActionPoint,
            TextPrimary, TextSecondary, TextTertiary, TextInverse, TextPoint,
            StrokeStrong, StrokeMedium, StrokeRegular, StrokeLight, StrokeUltralight,
            StatusRed, StatusAmber, StatusGreen
        }

        public enum TypeToken
        {
            None,
            Highlight1, Highlight2, Highlight3,
            Headline1, Headline2, Headline3,
            Title1, Title2,
            Body1, Body2, Body3, Body4,
            Caption1, Caption2,
            SpecialLarge, SpecialSmall, Alert1
        }

        [SerializeField] ColorToken color = ColorToken.None;
        [SerializeField] TypeToken type = TypeToken.None;

        void OnEnable() { Apply(); }
        void OnValidate() { Apply(); }

        public void Apply()
        {
            if (color != ColorToken.None)
            {
                Color c = Resolve(color);
                var img = GetComponent<Image>();
                if (img != null) img.color = c;
                var txt = GetComponent<TMP_Text>();
                if (txt != null) txt.color = c;
            }

            if (type != TypeToken.None)
            {
                var txt = GetComponent<TMP_Text>();
                if (txt != null) ResolveType(type).Apply(txt);
            }
        }

        static Color Resolve(ColorToken t)
        {
            switch (t)
            {
                case ColorToken.BgBase:          return DesignTokens.Bg.Base;
                case ColorToken.BgSurface:       return DesignTokens.Bg.Surface;
                case ColorToken.BgGlass:         return DesignTokens.Bg.Glass;
                case ColorToken.BgHighSurface:   return DesignTokens.Bg.HighSurface;
                case ColorToken.BgLowSurface:    return DesignTokens.Bg.LowSurface;
                case ColorToken.ActionPrimary:   return DesignTokens.Action.Primary;
                case ColorToken.ActionPressed:   return DesignTokens.Action.Pressed;
                case ColorToken.ActionPoint:     return DesignTokens.Action.Point;
                case ColorToken.TextPrimary:     return DesignTokens.Text.Primary;
                case ColorToken.TextSecondary:   return DesignTokens.Text.Secondary;
                case ColorToken.TextTertiary:    return DesignTokens.Text.Tertiary;
                case ColorToken.TextInverse:     return DesignTokens.Text.Inverse;
                case ColorToken.TextPoint:       return DesignTokens.Text.Point;
                case ColorToken.StrokeStrong:    return DesignTokens.Stroke.Strong;
                case ColorToken.StrokeMedium:    return DesignTokens.Stroke.Medium;
                case ColorToken.StrokeRegular:   return DesignTokens.Stroke.Regular;
                case ColorToken.StrokeLight:     return DesignTokens.Stroke.Light;
                case ColorToken.StrokeUltralight:return DesignTokens.Stroke.Ultralight;
                case ColorToken.StatusRed:       return DesignTokens.Primitive.Status.Red;
                case ColorToken.StatusAmber:     return DesignTokens.Primitive.Status.Amber;
                case ColorToken.StatusGreen:     return DesignTokens.Primitive.Status.Green;
                default:                         return Color.white;
            }
        }

        static TypeStyle ResolveType(TypeToken t)
        {
            switch (t)
            {
                case TypeToken.Highlight1:   return DesignTokens.Type.Highlight1;
                case TypeToken.Highlight2:   return DesignTokens.Type.Highlight2;
                case TypeToken.Highlight3:   return DesignTokens.Type.Highlight3;
                case TypeToken.Headline1:    return DesignTokens.Type.Headline1;
                case TypeToken.Headline2:    return DesignTokens.Type.Headline2;
                case TypeToken.Headline3:    return DesignTokens.Type.Headline3;
                case TypeToken.Title1:       return DesignTokens.Type.Title1;
                case TypeToken.Title2:       return DesignTokens.Type.Title2;
                case TypeToken.Body1:        return DesignTokens.Type.Body1;
                case TypeToken.Body2:        return DesignTokens.Type.Body2;
                case TypeToken.Body3:        return DesignTokens.Type.Body3;
                case TypeToken.Body4:        return DesignTokens.Type.Body4;
                case TypeToken.Caption1:     return DesignTokens.Type.Caption1;
                case TypeToken.Caption2:     return DesignTokens.Type.Caption2;
                case TypeToken.SpecialLarge: return DesignTokens.Type.SpecialLarge;
                case TypeToken.SpecialSmall: return DesignTokens.Type.SpecialSmall;
                case TypeToken.Alert1:       return DesignTokens.Type.Alert1;
                default:                     return DesignTokens.Type.Body2;
            }
        }
    }
}