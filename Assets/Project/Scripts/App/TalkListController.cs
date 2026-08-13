using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 텍스트 대화 목록. 행 (20,200) 372×101, 간격 113 (시안 실측).
    // 행을 왼쪽으로 밀면 삭제 버튼이 나온다(시안 '목록-1').
    public class TalkListController : MonoBehaviour
    {
        [System.Serializable]
        public class Session
        {
            public string title;
            public string preview;
            public string time;
            public bool unread;
            public bool heart;      // 미리보기 끝에 🖤 (TMP 폰트에 이모지가 없어 Image 로 붙인다)
            public Session(string t, string p, string tm, bool u = false, bool h = false)
            { title = t; preview = p; time = tm; unread = u; heart = h; }
        }

        [Header("상단")]
        [SerializeField] Button btnNewTalk;
        [SerializeField] Button btnBack;

        [Header("목록 (시안 실측)")]
        [SerializeField] RectTransform content;
        [SerializeField] float rowH = 101f;
        [SerializeField] float rowGap = 12f;
        [SerializeField] float swipeWidth = 52f;      // 시안: 행이 20→-32 로 52 밀린다
        [SerializeField] Sprite trashIcon;

        [Header("삭제 모달")]
        [SerializeField] GameObject modalDelete;
        [SerializeField] Button btnDeleteCancel;
        [SerializeField] Button btnDeleteConfirm;
        // 삭제 모달은 보관함과 같은 오브젝트를 공유한다(시안 좌표가 완전히 동일) → 문구만 바꾼다
        [SerializeField] TMP_Text modalTitle;
        [SerializeField] TMP_Text modalSub;

        [Header("스프라이트")]
        [SerializeField] Sprite rowSprite;      // bg_round16 — 시안 행 r16
        [SerializeField] Sprite circleSprite;   // circle — 안읽음 점(민트 원)
        [SerializeField] Sprite tipSprite;      // bg_round12 — 하단 팁 카드 r12
        [SerializeField] Sprite tipIcon;        // ic_tip_bulb
        [SerializeField] Sprite heartSprite;    // ic_heart_black — 미리보기 끝 🖤

        // 시안 '텍스트 대화 목록' 하단 팁 — 카드 (31,539) 350×40 r12,
        //   전구 (75.97,548.66) 22×24, 글자 (104.74,553.9) 12px #D3D5DD
        [SerializeField] string tipText = "대화 세션을 스와이프하면 내역을 삭제할 수 있어요";

        [Header("이동")]
        [SerializeField] string detailScreen = "TalkDetail";

        [Header("데이터 (임시 — API 오면 소스만 교체)")]
        [SerializeField]
        List<Session> sessions = new List<Session>
        {
            new Session("오늘의 무대 이야기", "오늘 무대 진짜 최고였어! 의상이랑 헤어 다 완벽...", "오후 12:25", true),
            new Session("토론토 여행 이야기", "헐!!! 대박인데?? 나도 거기 가보고 싶었는데! 거기 @@...", "어제"),
            new Session("첫 만남 & 앨범 콘셉트 대화", "오늘도 나 보러 와줬네? 뭐 하고 있었어?", "3일 전", false, true),
        };

        // 지금 어느 대화를 여는지 — 상세 화면이 읽는다
        public static int PendingIndex;

        int pendingDelete = -1;
        readonly List<GameObject> rows = new List<GameObject>();

        void OnEnable()
        {
            if (btnNewTalk != null) btnNewTalk.onClick.AddListener(OnNew);
            if (btnDeleteCancel != null) btnDeleteCancel.onClick.AddListener(CloseModal);
            if (btnDeleteConfirm != null) btnDeleteConfirm.onClick.AddListener(DoDelete);
            if (modalDelete != null) modalDelete.SetActive(false);
            Build();
        }

        void OnDisable()
        {
            if (btnNewTalk != null) btnNewTalk.onClick.RemoveListener(OnNew);
            if (btnDeleteCancel != null) btnDeleteCancel.onClick.RemoveListener(CloseModal);
            if (btnDeleteConfirm != null) btnDeleteConfirm.onClick.RemoveListener(DoDelete);
        }

        void Build()
        {
            if (content == null) return;
            foreach (var r in rows) if (r != null) Destroy(r);
            rows.Clear();

            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                var row = new GameObject("TalkRow" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                row.transform.SetParent(content, false);
                var rt = row.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(0f, 0f);
                rt.offsetMax = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(0f, -i * (rowH + rowGap));
                rt.sizeDelta = new Vector2(0f, rowH);
                var rowImg = row.GetComponent<Image>();
                rowImg.sprite = rowSprite; rowImg.type = Image.Type.Sliced;   // 시안 행 r16
                rowImg.color = DesignTokens.Bg.Base;

                // 밀었을 때 드러나는 삭제 버튼 (행 뒤에 깔린다)
                var del = MakeChild(row, "BtnRowDelete", 0f, rowH);
                var drt = del.GetComponent<RectTransform>();
                drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
                // 앞면과 정확히 같은 크기면 라운드 모서리의 안티에일리어싱이 어긋나
                // 네 귀퉁이로 빨간색이 삐져나온다 → 1 유닛 안쪽으로 넣는다.
                drt.offsetMin = new Vector2(1f, 1f); drt.offsetMax = new Vector2(-1f, -1f);
                var delImg = del.GetComponent<Image>();
                delImg.sprite = rowSprite; delImg.type = Image.Type.Sliced;
                delImg.color = DesignTokens.Primitive.Status.Red;
                var dbtn = del.AddComponent<Button>();
                int captured = i;
                dbtn.onClick.AddListener(() => AskDelete(captured));
                del.transform.SetAsFirstSibling();

                // 시안 실측(행 기준): 휴지통 중심 (343,36) 24×24 · '삭제' 중심 (342.7,70) 13px
                //   → 둘 다 행 오른쪽 끝에서 29 안쪽. 오른쪽 앵커로 잡아 폭이 변해도 안 흔들린다.
                if (trashIcon != null)
                {
                    var ic = UiUtil.MakeImage(del.transform, "Icon", trashIcon, DesignTokens.Text.Primary);
                    var irt = (RectTransform)ic.transform;
                    irt.anchorMin = irt.anchorMax = new Vector2(1f, 1f);
                    irt.pivot = new Vector2(1f, 1f);
                    irt.anchoredPosition = new Vector2(-17f, -24f);
                    irt.sizeDelta = new Vector2(24f, 24f);
                }
                Label(del, "DelLabel", "삭제", -3.31f, -62.04f, 52f, 16f, 13,
                      DesignTokens.Text.Primary, right: true);

                // 내용을 담는 앞면 — 이게 좌우로 움직인다
                var face = MakeChild(row, "Face", 0f, rowH);
                var frt = face.GetComponent<RectTransform>();
                frt.anchorMin = new Vector2(0f, 0f);
                frt.anchorMax = new Vector2(1f, 1f);
                frt.offsetMin = Vector2.zero;
                frt.offsetMax = Vector2.zero;
                // 앞면에도 라운드를 줘야 한다 — 여기에 sprite 가 없으면 뒤 행의 r16 이 가려져 각지게 보인다
                var faceImg = face.GetComponent<Image>();
                faceImg.sprite = rowSprite;
                faceImg.type = Image.Type.Sliced;
                faceImg.color = DesignTokens.Bg.Base;

                Label(face, "Title", s.title, 36.53f, -28.67f, 260f, 20f, 16, DesignTokens.Text.Primary);
                Label(face, "Preview", s.preview, 36.49f, -60.95f, 280f, 18f, 13, DesignTokens.Text.Tertiary);
                if (s.heart && heartSprite != null)
                {
                    var pv = face.transform.Find("Preview").GetComponent<TMP_Text>();
                    pv.ForceMeshUpdate();
                    var hi = UiUtil.MakeImage(face.transform, "PreviewHeart", heartSprite, Color.white);
                    var hrt = (RectTransform)hi.transform;
                    hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 1f);
                    hrt.pivot = new Vector2(0f, 1f);
                    hrt.anchoredPosition = new Vector2(36.49f + pv.preferredWidth + 4f, -60.95f);
                    hrt.sizeDelta = new Vector2(14f, 14f);
                }
                Label(face, "Time", s.time, -20f, -27.68f, 100f, 16f, 13, DesignTokens.Text.Secondary, right: true);

                if (s.unread)
                {
                    var dot = MakeChild(face, "Unread", 8f, 8f);
                    var urt = dot.GetComponent<RectTransform>();
                    urt.anchorMin = urt.anchorMax = new Vector2(1f, 1f);
                    urt.pivot = new Vector2(1f, 1f);
                    urt.anchoredPosition = new Vector2(-16f, -69f);
                    var di = dot.GetComponent<Image>();
                    di.sprite = circleSprite;                      // 네모가 아니라 원이다
                    di.color = DesignTokens.Action.Point;          // #6FE8F5
                }

                var swipe = face.AddComponent<SwipeRow>();
                swipe.Setup(frt, swipeWidth, () => Open(captured));
                rows.Add(row);
            }

            // 하단 팁 — 마지막 행에서 rowGap(12) 아래 (시안: 3행 bottom 527 → 팁 top 539)
            float tipY = sessions.Count * (rowH + rowGap);
            var tip = UiUtil.MakeImage(content, "Tip", tipSprite, DesignTokens.Bg.Surface, Image.Type.Sliced);
            UiUtil.Place(tip, 11f, tipY, 350f, 40f);
            var bulb = UiUtil.MakeImage(tip.transform, "Bulb", tipIcon, Color.white);
            UiUtil.Place(bulb, 43f, 8f, 22f, 24f);
            var tipLabel = UiUtil.MakeText(tip.transform, "Label", DesignTokens.Type.Caption1,
                                           DesignTokens.Text.Secondary, TextAlignmentOptions.MidlineLeft);
            tipLabel.fontSize = 12f;                 // 시안 실측 (토큰에 12 가 없다)
            tipLabel.textWrappingMode = TextWrappingModes.NoWrap;
            tipLabel.text = tipText;
            UiUtil.Place(tipLabel, 73.74f, 0f, 270f, 40f);
            rows.Add(tip.gameObject);

            content.sizeDelta = new Vector2(content.sizeDelta.x, tipY + 40f);
        }

        static GameObject MakeChild(GameObject parent, string name, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }

        // 코드로 만든 TMP 는 폰트가 비어 한글이 □□□ 로 나온다 → 씬 라벨에서 물려받는다
        TMP_Text FontSource => btnEditLabelSource;
        [SerializeField] TMP_Text btnEditLabelSource;

        void Label(GameObject parent, string name, string text, float x, float y,
                   float w, float h, float size, Color color, bool right = false, bool center = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            float ax = center ? 0.5f : (right ? 1f : 0f);
            rt.anchorMin = rt.anchorMax = new Vector2(ax, 1f);
            rt.pivot = new Vector2(ax, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (FontSource != null && FontSource.font != null)
            { t.font = FontSource.font; t.fontSharedMaterial = FontSource.fontSharedMaterial; }
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.raycastTarget = false;
            t.alignment = center ? TextAlignmentOptions.Center
                        : right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            if (name == "DelLabel") t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
        }

        void Open(int i)
        {
            PendingIndex = i;
            // 읽었으니 안읽음 점을 지운다. 목록으로 돌아오면 OnEnable 의 Build 가 반영한다.
            if (i >= 0 && i < sessions.Count) sessions[i].unread = false;
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(detailScreen);
        }

        void OnNew()
        {
            PendingIndex = -1;
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(detailScreen);
        }

        // 시안 '텍스트 대화 목록-1'
        void AskDelete(int i)
        {
            if (modalTitle != null) modalTitle.text = "대화를 삭제하시겠습니까?";
            if (modalSub != null) modalSub.text = "삭제한 대화는 복구할 수 없습니다.";
            pendingDelete = i;
            if (modalDelete != null) modalDelete.SetActive(true);
        }

        void CloseModal()
        {
            pendingDelete = -1;
            if (modalDelete != null) modalDelete.SetActive(false);
        }

        void DoDelete()
        {
            if (pendingDelete >= 0 && pendingDelete < sessions.Count)
                sessions.RemoveAt(pendingDelete);
            CloseModal();
            Build();
        }
    }
}
