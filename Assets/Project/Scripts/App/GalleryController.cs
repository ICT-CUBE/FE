using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 갤러리 화면. 시안(갤러리.svg)은 2열 마소너리 — 사진 비율대로 높이가 제각각이다.
    // 열 폭 178, 간격 16, 첫 행 y=176. 타일은 코드로 만든다(프리팹 불필요).
    //
    // 2026-08-21: 데이터 출처를 씬의 더미 목록 → GalleryStore(서버) 로 바꿨다.
    // 좌표·색·시안 근거는 전부 그대로다. 바뀐 건 "무엇을 그리느냐" 뿐이다.
    public class GalleryController : MonoBehaviour
    {
        [Header("상단")]
        [SerializeField] TMP_Text navTitle;
        [SerializeField] TMP_Text labelSelected;
        [SerializeField] TMP_Text btnEditLabel;     // 편집 / 취소
        [SerializeField] Button btnEdit;
        [SerializeField] Button btnBack;

        [Header("탭")]
        [SerializeField] RectTransform tabPill;
        [SerializeField] TMP_Text[] tabLabels;      // 전체/캡처/앨범/콘서트 스틸컷 순 = GalleryCategory 순서

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
        // AR 재경험 버튼 — 무대·트랙 캡처에서만 보인다 (앨범·콘서트 스틸컷·소환 캡처엔
        // 서버 ar-experience 가 없다. 2026-08-30 유저 지시)
        [SerializeField] GameObject btnReplay;      // Screen_CaptureDetail/BtnCapReplay

        // 한 타일에 딸린 것들. 사진이 도착하면 높이가 바뀌므로 타일을 다시 만들지 않고
        // 이 기록을 보고 자리만 다시 잡는다(20장을 매번 Destroy/Create 하지 않는다).
        class Tile
        {
            public GalleryItem item;
            public RectTransform rect;
            public RawImage photo;
            public Image check;
            public float ratio = 1f;    // 높이/폭. 사진이 오기 전엔 1(정사각 자리표시자)
        }

        GalleryItem detailItem;    // 상세로 연 사진
        bool deletingDetail;       // 확인 모달이 '상세 1장 삭제' 인지

        // 캡처 상세가 '갤러리에서' 열렸는지 'AR 촬영 흐름에서' 열렸는지 구분한다.
        // ARMediaController.OnReplayStart 가 읽어 서버 AR재경험(ar-experience)으로 무대를 고르고,
        // AR 흐름이 상세를 열 때(OnCardClicked)는 null 로 되돌린다.
        public static GalleryItem CurrentDetail;
        GalleryCategory tab = GalleryCategory.All;
        bool editing;
        readonly HashSet<string> selected = new HashSet<string>();   // ★ GalleryItem.Key (sourceType:id)
        readonly List<Tile> tiles = new List<Tile>();

        GalleryStore Store => GalleryStore.Instance;

        void OnEnable()
        {
            if (btnEdit != null) btnEdit.onClick.AddListener(ToggleEdit);
            HookTabs(true);
            if (modalDelete != null) modalDelete.SetActive(false);
            editing = false;
            selected.Clear();

            if (Store == null)
            {
                // 목록이 통째로 비면 어디서 끊겼는지 바로 보이게 남긴다(CommunityStore 와 같은 관례).
                Debug.LogError("[Gallery] GalleryStore 가 없다 — Canvas/ScreenManager 에 GalleryStore 를 붙일 것");
                Refresh();
                return;
            }

            Store.OnChanged += Refresh;
            Store.Load(tab);     // 갤러리에 들어올 때마다 서버 기준으로 새로 받는다
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
            if (Store != null) Store.OnChanged -= Refresh;
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
                if (on) btn.onClick.AddListener(() => SelectTab((GalleryCategory)idx));
                else btn.onClick.RemoveAllListeners();
            }
        }

        void SelectTab(GalleryCategory c)
        {
            tab = c;
            selected.Clear();
            // 서버가 카테고리별로 걸러 준다 — 전량을 받아 앱에서 거르지 않는다.
            if (Store != null) Store.Load(c);
            else Refresh();
        }

        // 시안: 편집 → (선택 없음) 취소 → (선택 있음) 삭제. 버튼 하나가 3역을 한다.
        void ToggleEdit()
        {
            if (editing && selected.Count > 0) { AskDelete(); return; }
            editing = !editing;
            if (!editing) selected.Clear();
            Refresh();
        }

        List<GalleryItem> Visible() => Store != null ? Store.Items : new List<GalleryItem>();

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
                bool on = (GalleryCategory)i == tab;
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

            foreach (var t in tiles) if (t.rect != null) Destroy(t.rect.gameObject);
            tiles.Clear();

            foreach (var item in Visible())
            {
                var tile = new GameObject("Tile_" + item.Key,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                tile.transform.SetParent(content, false);
                var rt = tile.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);

                // 시안 타일은 r8 라운드. 유니티 UI 는 이미지를 못 둥글게 잘라서
                // 타일에 Mask(라운드 스프라이트) 를 걸고 사진은 자식으로 넣는다.
                var img = tile.GetComponent<Image>();
                img.sprite = tileRound;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                img.preserveAspect = false;
                var mask = tile.AddComponent<Mask>();
                mask.showMaskGraphic = false;

                // 서버 사진은 Texture2D 라 Image(Sprite) 가 아니라 RawImage 로 받는다.
                // 타일 높이를 사진 비율에 맞춰 잡으므로 늘어나 보이지 않는다(크롭 불필요).
                var photo = new GameObject("Photo", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                photo.transform.SetParent(tile.transform, false);
                var prt = photo.GetComponent<RectTransform>();
                prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                var praw = photo.GetComponent<RawImage>();
                praw.raycastTarget = false;

                // 체크(브이) — 시안(갤러리 편집-1): 원 지름 20, 타일 좌상단에서 중심 (20,20).
                // 스프라이트 잉크가 캔버스의 60/72 라 박스를 24 로 줘야 원이 20 으로 그려진다.
                //
                // ★ 앨범 사진(ALBUM_IMAGE)도 체크박스를 띄운다 — 시안이 8타일 전부에 그려놨다.
                //   서버는 앨범 삭제를 400 으로 막지만, 데모에서 밟지 않는 경로라
                //   유저 결정으로 시안을 따른다(docs/ARZONE_API연결_설계.md 7-3-1).
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
                cimg.sprite = selected.Contains(item.Key) ? checkOn : checkOff;
                check.SetActive(editing);

                var rec = new Tile { item = item, rect = rt, photo = praw, check = cimg };
                tiles.Add(rec);

                var captured = item;
                tile.GetComponent<Button>().onClick.AddListener(() => OnTile(captured));

                ApplyTexture(rec, item.texture != null ? item.texture : ImageCache.Peek(item.imageUrl));

                // 아직 없으면 받아온다. 도착하면 그 타일만 비율을 고치고 열을 다시 앉힌다.
                if (rec.photo.texture == null && !string.IsNullOrEmpty(item.imageUrl))
                {
                    var target = rec;
                    ImageCache.Load(item.imageUrl, tex =>
                    {
                        if (target.rect == null) return;    // 그 사이 화면이 다시 그려졌다
                        target.item.texture = tex;
                        ApplyTexture(target, tex);
                        Relayout();
                    });
                }
            }

            Relayout();
        }

        void ApplyTexture(Tile t, Texture2D tex)
        {
            if (t.photo == null) return;

            if (tex == null)
            {
                // 자리표시자 — 사진이 오기 전(또는 실패). 흰 판이 뜨지 않게 어두운 색으로 둔다.
                t.photo.texture = null;
                t.photo.color = DesignTokens.Primitive.Neutral._900;
                t.ratio = 1f;    // 비율을 모르니 정사각
                return;
            }

            t.photo.texture = tex;
            t.photo.color = Color.white;
            t.ratio = tex.width > 0 ? (float)tex.height / tex.width : 1f;
        }

        // 타일을 다시 만들지 않고 자리만 잡는다. 사진이 한 장 도착할 때마다 불린다.
        void Relayout()
        {
            if (content == null) return;

            float[] colY = { 0f, 0f };

            foreach (var t in tiles)
            {
                if (t.rect == null) continue;
                float h = columnWidth * t.ratio;

                int col = colY[0] <= colY[1] ? 0 : 1;
                t.rect.anchoredPosition = new Vector2(col * (columnWidth + gap), -colY[col]);
                t.rect.sizeDelta = new Vector2(columnWidth, h);

                colY[col] += h + gap;
            }

            float total = Mathf.Max(colY[0], colY[1]) - gap;
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(0f, total));
        }

        void OnTile(GalleryItem item)
        {
            if (editing)
            {
                if (!selected.Remove(item.Key)) selected.Add(item.Key);
                SyncHeader();
                // 체크 스프라이트만 갈아끼운다 — 전체를 다시 그리면 받아둔 사진이 깜빡인다.
                foreach (var t in tiles)
                    if (t.check != null) t.check.sprite = selected.Contains(t.item.Key) ? checkOn : checkOff;
                return;
            }
            OpenDetail(item);
        }

        void OpenDetail(GalleryItem item)
        {
            if (item == null) return;
            detailItem = item;
            CurrentDetail = item;
            if (btnReplay != null)
                btnReplay.SetActive(item.sourceType == GallerySourceType.StageCapture
                                    || item.sourceType == GallerySourceType.TrackCapture);

            // 유저 지시(2026-08-25): 상세에선 가두지 말고 원래 비율대로.
            // FitCover 는 rect 0(꺼진 화면)에서 일찍 반환해 사진이 아예 안 뜨기도 했다.
            UiUtil.FitContain(detailPhoto, item.texture);
            // 썸네일이 아직 안 내려온(또는 실패한) 항목은 texture 가 null — RawImage 가 흰 사각형만 뜬다.
            // 상세에서 직접 받아서 도착하면 같은 자리(비율 유지 FitContain)로 다시 그린다.
            if (item.texture == null && !string.IsNullOrEmpty(item.imageUrl))
                ImageCache.Load(item.imageUrl, tex =>
                {
                    if (tex == null || detailItem != item) return;   // 그새 다른 사진을 열었으면 버린다
                    item.texture = tex;
                    UiUtil.FitContain(detailPhoto, tex);
                });
            // ⚠ 서버 갤러리 응답에는 제목이 없다(id/sourceType/category/imageUrl/displayDate 뿐).
            //   시안 문구를 지어내지 않으려고 detailTitle 은 건드리지 않는다.
            if (detailMeta != null) detailMeta.text = (item.displayDate ?? "").Replace('-', '.');

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
            if (detailItem == null)
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
            if (Store == null) { CloseDelete(); return; }

            // 상세에서 온 삭제는 그 한 장만 지우고 갤러리로 돌아간다
            if (deletingDetail)
            {
                var one = new List<GalleryItem> { detailItem };
                detailItem = null;
                CloseDelete();
                Store.Delete(one, () =>
                {
                    if (ScreenManager.Instance != null) ScreenManager.Instance.Back();
                    Toast.Show("사진이 삭제되었어요");
                });
                return;
            }

            var targets = new List<GalleryItem>();
            foreach (var t in tiles)
                if (selected.Contains(t.item.Key)) targets.Add(t.item);

            selected.Clear();
            CloseDelete();
            editing = false;

            Store.Delete(targets, () => Toast.Show("사진이 삭제되었어요"));
        }
    }
}
