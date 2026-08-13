using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // '대화 주제 추천' 카드 3개. 음성 대화 홈과 통화 화면이 같은 카드를 쓴다(시안 동일).
    // 시안: 118×97 r12 #0F0D17, 제목 18 Regular white 잉크 +16,+18 / 설명 13 Medium #D3D5DD 잉크 +16.46,+48.74
    public static class VoiceTopicRow
    {
        [Serializable]
        public class Topic
        {
            public string title;
            public string desc;
            public Topic(string t, string d) { title = t; desc = d; }
        }

        public static readonly List<Topic> Default = new List<Topic>
        {
            new Topic("☕️ 오늘 하루",   "오늘 뭐 했는지\n알려줄게!"),
            new Topic("🎵 음악 이야기", "요즘 듣는 노래는\n뭐야? 추천해줘"),
            new Topic("⭐️ 랜덤 질문",   "궁금한 게 있으면\n물어봐"),
        };

        // 이모지(☕️🎵⭐️)는 Pretendard 에 글리프가 없다. 아이콘 스프라이트를 주면 그걸 쓰고,
        // 없으면 제목 텍스트 그대로 둔다(그 경우 이모지는 □ 로 보인다).
        static string StripEmoji(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder();
            foreach (var ch in s)
                if (ch < 0x2000) sb.Append(ch);           // 한글·영문·숫자·기본 문장부호만 남긴다
            return sb.ToString().TrimStart();
        }

        // row 폭에 맞춰 카드 3개를 채운다. onPick 은 카드를 누를 때.
        public static void Build(RectTransform row, Sprite cardSprite, IList<Topic> topics,
                                 Action<int> onPick, TMP_Text fontSource = null, IList<Sprite> icons = null)
        {
            if (row == null) return;
            UiUtil.ClearChildren(row);
            if (topics == null || topics.Count == 0) topics = Default;

            float w = row.sizeDelta.x > 1f ? row.sizeDelta.x : 372f;   // OnEnable 시점엔 rect 가 아직 0 이다
            float cardW = 118.67f, gap = (w - cardW * 3f) / 2f;

            for (int i = 0; i < topics.Count; i++)
            {
                var card = UiUtil.MakeNode(row, "Topic" + i);
                UiUtil.Place(card, i * (cardW + gap), 0f, cardW, 97f);

                var bg = UiUtil.MakeImage(card, "Bg", cardSprite, DesignTokens.Bg.Base, Image.Type.Sliced, true);
                var brt = (RectTransform)bg.transform;
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

                bool hasIcon = icons != null && i < icons.Count && icons[i] != null;
                if (hasIcon)
                {
                    var ic = UiUtil.MakeImage(card, "Icon", icons[i], Color.white);
                    UiUtil.Place(ic, 16f, 11f, 20f, 20f);
                }

                var title = UiUtil.MakeText(card.transform, "Title", DesignTokens.Type.Body4,
                                            DesignTokens.Text.Primary, TextAlignmentOptions.TopLeft);
                title.fontSize = 18f;                       // 시안 실측 (토큰 조합에 18 Regular 이 없다)
                title.text = hasIcon ? StripEmoji(topics[i].title) : topics[i].title;
                if (hasIcon) UiUtil.Place(title, 40f, 12f, cardW - 48f, 26f);
                else         UiUtil.Place(title, 16f, 12f, cardW - 24f, 26f);
                Copy(fontSource, title);

                var desc = UiUtil.MakeText(card.transform, "Desc", DesignTokens.Type.Body3,
                                           DesignTokens.Text.Secondary, TextAlignmentOptions.TopLeft);
                desc.fontSize = 13f;
                desc.textWrappingMode = TextWrappingModes.NoWrap;   // 줄바꿈은 문구의 \n 으로만
                desc.text = topics[i].desc;
                UiUtil.Place(desc, 16f, 44f, cardW - 30f, 44f);
                Copy(fontSource, desc);

                int k = i;
                var btn = bg.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.None;
                if (onPick != null) btn.onClick.AddListener(() => onPick(k));
            }
        }

        // 코드로 만든 TMP 는 폰트가 비어 한글이 깨진다 — 씬 라벨에서 물려받는다
        static void Copy(TMP_Text src, TMP_Text dst)
        {
            if (src == null || src.font == null || dst == null) return;
            dst.font = src.font;
            dst.fontSharedMaterial = src.fontSharedMaterial;
        }
    }
}
