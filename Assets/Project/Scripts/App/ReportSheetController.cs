using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 신고 바텀시트. '신고 바텀시트.svg' 실측.
    //
    //   사유 행  (20.5, 394.5 / 450.5 / 506.5) 371×47  — 간격 56
    //     선택   테두리 #A855F7 · 라디오 = 채운 원 #A855F7@40% + 체크 (ic_check_circle_on)
    //     미선택 테두리 #242831 · 라디오 = 링 #727886        (ic_checkbox_off)
    //     라디오는 24×24, 중심 (44, 행 y+23.5)
    //   상세 설명 박스 (20.5,599.5) 371×177, 힌트 (36.71,618.9)
    //   글자수   (…,748) 우측정렬 '0 / 200'
    //   제출     (20,793) 372×56 #A855F7
    //
    // 신고 내용을 실제로 보내는 곳(백엔드)이 아직 없다 → 지금은 화면 동작만 하고 로그를 남긴다.
    public class ReportSheetController : MonoBehaviour
    {
        [Header("사유 3개 (같은 순서로)")]
        [SerializeField] Image[] optBg = new Image[3];
        [SerializeField] Image[] optRadio = new Image[3];
        [SerializeField] Button[] optButton = new Button[3];
        [SerializeField] Sprite radioOn;    // ic_check_circle_on
        [SerializeField] Sprite radioOff;   // ic_checkbox_off

        [Header("상세 설명")]
        [SerializeField] RectTransform detailBox;
        [SerializeField] TMP_Text detailHint;    // placeholder 로 재활용
        [SerializeField] TMP_Text detailCount;
        [SerializeField] int charLimit = 200;

        [Header("제출")]
        [SerializeField] Button btnSubmit;

        int picked = -1;
        TMP_InputField detail;

        void OnEnable()
        {
            EnsureInput();
            for (int i = 0; i < optButton.Length; i++)
            {
                int k = i;
                if (optButton[i] != null) optButton[i].onClick.AddListener(() => Pick(k));
            }
            if (btnSubmit != null) btnSubmit.onClick.AddListener(Submit);
            Reset();
        }

        void OnDisable()
        {
            foreach (var b in optButton) if (b != null) b.onClick.RemoveAllListeners();
            if (btnSubmit != null) btnSubmit.onClick.RemoveAllListeners();
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        void Reset()
        {
            picked = -1;
            if (detail != null) detail.text = "";
            SyncCount("");
            Sync();
        }

        void Pick(int i)
        {
            picked = i;
            Sync();
        }

        // 시안의 선택/미선택 두 상태를 그대로 반영한다
        void Sync()
        {
            for (int i = 0; i < optBg.Length; i++)
            {
                bool on = i == picked;
                if (optBg[i] != null)
                    optBg[i].color = on ? DesignTokens.Action.Primary : DesignTokens.Stroke.Regular;
                if (optRadio[i] != null)
                {
                    optRadio[i].sprite = on ? radioOn : radioOff;
                    optRadio[i].color = Color.white;   // 스프라이트에 색이 구워져 있다
                }
            }
        }

        // 시안 상세 박스는 이미지 + 힌트뿐이라 글자를 칠 수 없다 → 코드로 InputField 를 붙인다.
        void EnsureInput()
        {
            if (detail != null || detailBox == null) return;

            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(detailBox, false);
            var art = (RectTransform)area.transform;
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(16.21f, 36f);    // 힌트 x=36.71 − 박스 x=20.5, 아래는 글자수 자리
            art.offsetMax = new Vector2(-16.21f, -19.4f);

            var tt = UiUtil.MakeText(area.transform, "Text", DesignTokens.Type.Body4,
                                     DesignTokens.Text.Primary, TextAlignmentOptions.TopLeft);
            var trt = (RectTransform)tt.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            tt.richText = false;

            var f = detailBox.gameObject.AddComponent<TMP_InputField>();
            f.textViewport = art;
            f.textComponent = tt;
            InputFieldTuner.Tune(f);   // ⚠ textComponent 배선 뒤에 불러야 캐럿(|)이 생긴다
            f.placeholder = detailHint;
            f.lineType = TMP_InputField.LineType.MultiLineNewline;
            f.characterLimit = charLimit;
            f.onValueChanged.AddListener(SyncCount);
            detail = f;
        }

        void SyncCount(string v)
        {
            if (detailCount != null) detailCount.text = (v != null ? v.Length : 0) + " / " + charLimit;
        }

        // 신고 대상 메시지 id — 대화 화면이 시트를 열 때 채운다
        public static int PendingMessageId;

        // 화면 라벨 → 서버 enum. 라벨 문구가 바뀌어도 키워드로 잡히게 한다.
        static string ToServerReason(string label)
        {
            if (!string.IsNullOrEmpty(label))
            {
                if (label.Contains("페르소나") || label.Contains("말투")) return "INCORRECT_PERSONA";
                if (label.Contains("사실") || label.Contains("환각") || label.Contains("잘못")) return "HALLUCINATION";
            }
            return "INAPPROPRIATE_EXPRESSION";
        }

        // 시안엔 미선택 상태의 안내가 없다 → 사유를 안 고르면 아무 일도 일어나지 않는다.
        void Submit()
        {
            if (picked < 0) return;
            string label = picked < optBg.Length && optBg[picked] != null
                ? ReasonText(picked) : picked.ToString();

            // 서버 전송 (2026-08-25 배선) — POST …/conversation-messages/{id}/reports
            if (ConversationStore.Instance != null && PendingMessageId > 0)
                ConversationStore.Instance.ReportMessage(PendingMessageId, ToServerReason(label),
                                                         detail != null ? detail.text : "");
            else
                Debug.LogWarning($"[Report] 신고 대상 메시지 id 가 없다 (사유={label})");
            PendingMessageId = 0;
            Close();
        }

        string ReasonText(int i)
        {
            var t = optBg[i].transform.parent != null
                ? optBg[i].transform.parent.Find("OptLabel" + i) : null;
            var x = t != null ? t.GetComponent<TMP_Text>() : null;
            return x != null ? x.text : i.ToString();
        }
    }
}
