using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 갤러리 화면. 시안(갤러리.svg)은 2열 마소너리 — 사진 비율대로 높이가 제각각이다.
    // 열 폭 178, 간격 16, 첫 행 y=176. 타일은 코드로 만든다(프리팹 불필요).
    public class GalleryController : MonoBehaviour
    {
        public enum Category { All, Capture, Album, Concert }

        [System.Serializable]
        public class Photo
        {
            public Sprite image;
            public Category category = Category.Capture;
            public string title = "251220MMA";
            public string date = "2025.12.20";
        }

        [Header("상단")]
        [SerializeField] TMP_Text navTitle;
        [SerializeField] TMP_Text labelSelected;
        [SerializeField] TMP_Text btnEditLabel;     // 편집 / 취소
        [SerializeField] Button btnEdit;
        [SerializeField] Button btnBack;

        [Header("탭")]
        [SerializeField] RectTransform tabPill;
        [SerializeField] TMP_Text[] tabLabels;      // 전체/캡처/앨범/콘서트 스틸컷 순

        [Header("그리드")]
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] float columnWidth = 178f;
        [SerializeField] float gap = 16f;
        [SerializeField] Sprite tileRound;      // bg_round8 — 시안 타일 r8
        [SerializeField] Sprite checkOff;
        [SerializeField] Sprite checkOn;

        [Header("삭제")]
        [SerializeField] GameObject modalDelete;
        [SerializeField] TMP_Text modalDeleteText;
        [SerializeField] Button btnDeleteCancel;
        [SerializeField] Button btnDeleteConfirm;

        // 타일을 누르면 캡처 상세로. 상세 화면의 라벨/사진을 여기서 채운다
        // (ARMediaController.OnCardClicked 와 같은 자리를 쓴다).
        [Header("사진 상세")]
        [SerializeField] string detailScreen = "CaptureDetail";
        [SerializeField] RawImage detailPhoto;      // Screen_CaptureDetail/PhotoImage
        [SerializeField] TMP_Text detailTitle;      // CapTitle
        [SerializeField] TMP_Text detailMeta;       // CapMeta
        [SerializeField] Button btnDetailDelete;    // Screen_CaptureDetail/BtnCapTrash

        [Header("데이터 (임시 — 캡처는 CaptureService, 나머지는 더미)")]
        [SerializeField] List<Photo> photos = new List<Photo>();

        int detailIndex = -1;      // 상세로 연 사진
        bool deletingDetail;       // 확인 모달이 '상세 1장 삭제' 인지
        Category tab = Category.All;
        bool editing;
        readonly HashSet<int> selected = new HashSet<int>();
        readonly List<GameObject> tiles = new List<GameObject>();
        readonly List<Image> checks = new List<Image>();

        void OnEnable()
        {
            if (btnEdit != null) btnEdit.onClick.AddListener(ToggleEdit);
            HookTabs(true);
            if (modalDelete != null) modalDelete.SetActive(false);
            editing = false;
            selected.Clear();
            Refresh();
        }

        // ★ 삭제 모달과 '사진 상세 삭제' 는 Screen_Gallery 가 꺼진 상태(캡처 상세)에서 눌린다.
        //   OnEnable/OnDisable 에 걸면 화면을 떠나는 순간 해제돼 버튼이 죽는다 → Start 에서 한 번만.
        void Start()
        {
            if (btnDeleteCancel != null) btnDeleteCancel.onClick.AddListener(CloseDelete);
            if (btnDeleteConfirm != null) btnDeleteConfirm.onClick.AddListener(DoDelete);
            if (btnDetailDelete != null) btnDetailDelete.onClick.AddListener(AskDeleteDetail);
        }

        void OnDestroy()
        {
            if (btnDeleteCancel != null) btnDeleteCancel.onClick.RemoveListener(CloseDelete);
            if (btnDeleteConfirm != null) btnDeleteConfirm.onClick.RemoveListener(DoDelete);
            if (btnDetailDelete != null) btnDetailDelete.onClick.RemoveListener(AskDeleteDetail);
        }

        void OnDisable()
        {
            if (btnEdit != null) btnEdit.onClick.RemoveListener(ToggleEdit);
            HookTabs(false);
        }

        void HookTabs(bool on)
        {
            if (tabLabels == null) return;
            for (int i = 0; i < tabLabels.Length; i++)
            {
                if (tabLabels[i] == null) continue;
                var btn = tabLabels[i].GetComponent<Button>();
                if (btn == null) btn = tabLabels[i].gameObject.AddComponent<Button>();
                // 씬 정리 때 버튼 없는 TMP 는 전부 raycastTarget 을 껐다.
                // 여기서 버튼을 붙이므로 다시 켜야 탭 클릭이 들어온다 (분류가 안 되던 원인).
                tabLabels[i].raycastTarget = on;
                if (btn.targetGraphic == null) btn.targetGraphic = tabLabels[i];
                btn.transition = Selectable.Transition.None;
                int idx = i;
                if (on) btn.onClick.AddListener(() => SelectTab((Category)idx));
                else btn.onClick.RemoveAllListeners();
            }
        }

        void SelectTab(Category c)
        {
            tab = c;
            selected.Clear();
            Refresh();
        }

        // 시안: 편집 → (선택 없음) 취소 → (선택 있음) 삭제. 버튼 하나가 3역을 한다.
        void ToggleEdit()
        {
            if (editing && selected.Count > 0) { AskDelete(); return; }
            editing = !editing;
            if (!editing) selected.Clear();
            Refresh();
        }

        List<int> Visible()
        {
            var list = new List<int>();
            for (int i = 0; i < photos.Count; i++)
                if (tab == Category.All || photos[i].category == tab) list.Add(i);
            return list;
        }

        void Refresh()
        {
            SyncHeader();
            SyncTabs();
            Build();
        }

        void SyncHeader()
        {
            bool picking = editing && selected.Count > 0;

            if (navTitle != null) navTitle.gameObject.SetActive(!picking);
            if (labelSelected != null)
            {
                labelSelected.gameObject.SetActive(picking);
                if (picking) labelSelected.text = $"{selected.Count}개 선택됨";
            }
            if (btnEditLabel != null)
            {
                // 시안: 편집(#A855F7) → 취소(#9CA0AD) → 삭제(#FF5B6F)
                btnEditLabel.text = !editing ? "편집" : (selected.Count > 0 ? "삭제" : "취소");
                btnEditLabel.color = !editing ? DesignTokens.Text.Point
                    : (selected.Count > 0 ? DesignTokens.Primitive.Status.Red
                                          : DesignTokens.Primitive.Neutral._200);
            }
        }

        void SyncTabs()
        {
            if (tabLabels == null) return;
            for (int i = 0; i < tabLabels.Length; i++)
            {
                if (tabLabels[i] == null) continue;
                bool on = (Category)i == tab;
                tabLabels[i].color = on ? DesignTokens.Text.Primary : DesignTokens.Text.Tertiary;
                if (on && tabPill != null)
                {
                    var src = tabLabels[i].rectTransform;
                    tabPill.anchorMin = src.anchorMin;
                    tabPill.anchorMax = src.anchorMax;
                    tabPill.pivot = src.pivot;
                    tabPill.anchoredPosition = src.anchoredPosition;
                    tabPill.sizeDelta = src.sizeDelta;
                }
            }
        }

        // 2열 마소너리 — 항상 더 짧은 열에 넣는다
        void Build()
        {
            if (content == null) return;

            foreach (var t in tiles) if (t != null) Destroy(t);
            tiles.Clear();
            checks.Clear();

            var vis = Visible();
            float[] colY = { 0f, 0f };

            foreach (int pi in vis)
            {
                var p = photos[pi];
                float ratio = (p.image != null && p.image.rect.width > 0.01f)
                    ? p.image.rect.height / p.image.rect.width : 1f;
                float h = columnWidth * ratio;

                int col = colY[0] <= colY[1] ? 0 : 1;
                float x = col * (columnWidth + gap);
                float y = -colY[col];

                var tile = new GameObject("Tile" + pi, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                tile.transform.SetParent(content, false);
                var rt = tile.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(x, y);
                rt.sizeDelta = new Vector2(columnWidth, h);

                // 시안 타일은 r8 라운드. 유니티 UI 는 이미지를 못 둥글게 잘라서
                // 타일에 Mask(라운드 스프라이트) 를 걸고 사진은 자식으로 넣는다.
                var img = tile.GetComponent<Image>();
                img.sprite = tileRound;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                img.preserveAspect = false;
                var mask = tile.AddComponent<Mask>();
                mask.showMaskGraphic = false;

                var photo = new GameObject("Photo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                photo.transform.SetParent(tile.transform, false);
                var prt = photo.GetComponent<RectTransform>();
                prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                var pimg = photo.GetComponent<Image>();
                pimg.sprite = p.image;
                pimg.preserveAspect = false;
                pimg.raycastTarget = false;

                // 체크박스 — 시안(갤러리 편집-1): 원 지름 20, 타일 좌상단에서 중심 (20,20).
                // 스프라이트 잉크가 캔버스의 60/72 라 박스를 24 로 줘야 원이 20 으로 그려진다.
                var check = new GameObject("Check", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                check.transform.SetParent(tile.transform, false);
                var crt = check.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(0f, 1f);
                crt.anchorMax = new Vector2(0f, 1f);
                crt.pivot = new Vector2(0f, 1f);
                crt.anchoredPosition = new Vector2(8f, -8f);
                crt.sizeDelta = new Vector2(24f, 24f);
                var cimg = check.GetComponent<Image>();
                cimg.raycastTarget = false;
                bool isSel = selected.Contains(pi);
                cimg.sprite = isSel ? checkOn : checkOff;
                check.SetActive(editing);
                checks.Add(cimg);

                int captured = pi;
                tile.GetComponent<Button>().onClick.AddListener(() => OnTile(captured));

                colY[col] += h + gap;
                tiles.Add(tile);
            }

            float total = Mathf.Max(colY[0], colY[1]) - gap;
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(0f, total));
        }

        void OnTile(int index)
        {
            if (editing)
            {
                if (!selected.Remove(index)) selected.Add(index);
                Refresh();
                return;
            }
            OpenDetail(index);
        }

        void OpenDetail(int index)
        {
            if (index < 0 || index >= photos.Count) return;
            detailIndex = index;
            var p = photos[index];

            UiUtil.FitCover(detailPhoto, p.image != null ? p.image.texture : null);
            if (detailTitle != null) detailTitle.text = p.title;
            if (detailMeta != null) detailMeta.text = p.date;

            if (ScreenManager.Instance != null && !string.IsNullOrEmpty(detailScreen))
                ScreenManager.Instance.Show(detailScreen);
            else
                Debug.LogError("[Gallery] ScreenManager 또는 detailScreen 이 비어 있음");
        }

        // '삭제' 를 누르면 확인 모달
        public void AskDelete()
        {
            if (selected.Count == 0) return;
            if (modalDeleteText != null)
                modalDeleteText.text = $"선택한 사진 {selected.Count}장을 삭제하시겠습니까?";
            if (modalDelete != null) modalDelete.SetActive(true);
        }

        // 시안 '사진 상세 삭제' — 카드/버튼 좌표가 갤러리 삭제와 같아 모달을 공유하고 문구만 바꾼다
        public void AskDeleteDetail()
        {
            if (detailIndex < 0 || detailIndex >= photos.Count)
            {
                Debug.LogWarning("[Gallery] 상세로 연 사진이 없어 삭제할 수 없음");
                return;
            }
            deletingDetail = true;
            if (modalDeleteText != null) modalDeleteText.text = "사진을 영구 삭제하시겠습니까?";
            if (modalDelete != null) modalDelete.SetActive(true);
        }

        void CloseDelete()
        {
            deletingDetail = false;
            if (modalDelete != null) modalDelete.SetActive(false);
        }

        void DoDelete()
        {
            // 상세에서 온 삭제는 그 한 장만 지우고 갤러리로 돌아간다
            if (deletingDetail)
            {
                if (detailIndex >= 0 && detailIndex < photos.Count) photos.RemoveAt(detailIndex);
                detailIndex = -1;
                CloseDelete();
                Refresh();
                if (ScreenManager.Instance != null) ScreenManager.Instance.Back();
                Toast.Show("사진이 삭제되었어요");
                return;
            }

            var idx = new List<int>(selected);
            idx.Sort((a, c) => c.CompareTo(a));      // 뒤에서부터 지워야 인덱스가 안 밀린다
            foreach (int i in idx) if (i >= 0 && i < photos.Count) photos.RemoveAt(i);

            selected.Clear();
            CloseDelete();
            editing = false;
            Refresh();
            Toast.Show("사진이 삭제되었어요");
        }
    }
}
