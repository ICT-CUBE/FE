using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // Popup_PostMenu — 글 카드(목록)·상세의 ⋮ 를 누르면 뜨는 팝업
    // (시안 '커뮤니티.svg' · '커뮤니티 글 상세.svg' — 둘 다 ⋮ 팝업이 열린 상태로 캡처돼 있다).
    // 목록/상세가 같은 팝업 하나를 공유하는 싱글턴 오버레이(Modal_PostDelete 와 같은 관례) — Canvas
    // 직계에 하나만 있고, Open() 이 불릴 때마다 Card 위치만 옮긴다.
    //
    // 실측 (두 시안 대조 — 아이콘·글자 상대 위치가 소수점까지 정확히 일치해 같은 컴포넌트로 확정):
    //   Card 132×120 r16 #181A20(BgHighSurface) · 패딩 4 · 두 줄(각 124×56, 사이 간격 0).
    //   신고 줄: 아이콘 ic_report 30×30(원본 24×24 대비 1.25배 — 시안 실측 그대로. 삭제 아이콘과
    //     배율이 다른 게 이상해 보이지만 두 시안에서 동일하게 재현돼 확정값) · 라벨 '신고하기'
    //     18 SemiBold(Headline3) 흰색, MidlineLeft.
    //     줄 배경: '커뮤니티.svg' 는 #0F1115 로 보이고 '글 상세' 는 카드와 같은 색이라 안 보인다 —
    //     두 시안이 갈려서, 카드 위에서 안 보이는(=배경 없음) 쪽을 기본값으로 잡았다(report 참고).
    //   삭제 줄: 아이콘 ic_trash 24×24(원본 그대로 — TalkArchiveController.MakeDeleteMenu 와 동일
    //     배율·오프셋) · 라벨 '삭제하기' 18 SemiBold #FF5B6F, MidlineLeft — TalkArchiveController 의
    //     '삭제하기'(잉크폭 59.52, 18 Headline3)와 완전히 같은 결론. 줄 배경은 두 시안 모두 동일하게
    //     #FF5B6F 10% (알파 그대로 안 쓰고 카드색 #181A20 위에 합성해 구운 불투명값 — global.md 규칙).
    //
    // mine=false(남의 글): 남의 글 ⋮ 시안은 없다. CommunityModels.cs 에 이미 있던 결정
    // ("mine 이면 ⋮ 에 삭제하기가 뜬다")을 따라 삭제 줄을 통째로 끄고 카드 높이를 64
    // (=패딩4+줄56+패딩4, 이미 있는 값의 재조합이라 새 수치를 지어낸 게 아니다)로 줄인다.
    //
    // 위치: 목록 카드 ⋮ 와 상세 BtnMore 는 팝업 대비 오프셋이 서로 다르다(다른 화면 다른 배치라
    // 실측값도 다름 — task-10-report.md). Open() 은 그 계산이 끝난 최종 좌표를 anchor 로 그대로
    //받아 Card 의 top-left 로 쓴다 — 호출부(CommunityController/CommunityPostController)가
    // CanvasTopLeft() 로 각자의 ⋮ 위치를 구하고 자기 화면의 오프셋을 더해 넘긴다.
    public class PostMenuPopup : MonoBehaviour
    {
        public static PostMenuPopup Instance { get; private set; }

        [Header("구성")]
        [SerializeField] GameObject popupRoot;      // 비우면 자기 자신
        [SerializeField] RectTransform card;
        [SerializeField] Button dimButton;

        [SerializeField] Button btnReport;          // 신고 줄 히트(투명)
        [SerializeField] Button btnDelete;          // 삭제 줄 배경(빨강 틴트) 자체가 버튼
        [SerializeField] GameObject deleteRow;      // 삭제 줄 통째(배경+아이콘+라벨) — mine=false 면 끈다

        [Header("삭제 확인 모달 (Modal_PostDelete)")]
        [SerializeField] GameObject modalDelete;
        [SerializeField] Button btnDeleteCancel;
        [SerializeField] Button btnDeleteConfirm;

        const float CardW = 132f;
        const float TwoRowH = 120f;    // 신고 + 삭제
        const float OneRowH = 64f;     // 신고만 (패딩4 + 줄56 + 패딩4)

        string targetId;

        GameObject Root => popupRoot != null ? popupRoot : gameObject;

        void Awake()
        {
            Instance = this;
            if (dimButton != null) dimButton.onClick.AddListener(Close);
            if (btnReport != null) btnReport.onClick.AddListener(OnReport);
            if (btnDelete != null) btnDelete.onClick.AddListener(OnAskDelete);
            if (btnDeleteCancel != null) btnDeleteCancel.onClick.AddListener(CloseDeleteModal);
            if (btnDeleteConfirm != null) btnDeleteConfirm.onClick.AddListener(OnConfirmDelete);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // anchor = Card 의 top-left 최종 좌표(이미 화면별 오프셋이 더해진 값). CanvasTopLeft() 로
        // 구한 ⋮ 위치에 호출부가 자기 화면의 오프셋을 더해 넘긴다.
        public void Open(string postId, bool mine, Vector2 anchor)
        {
            targetId = postId;
            if (card != null) UiUtil.Place(card, anchor.x, anchor.y, CardW, mine ? TwoRowH : OneRowH);
            UiUtil.SetActive(deleteRow, mine);
            Root.SetActive(true);
            Root.transform.SetAsLastSibling();
        }

        public void Close() => Root.SetActive(false);

        void OnReport()
        {
            Close();
            if (string.IsNullOrEmpty(targetId)) return;
            var store = CommunityStore.Instance;
            if (store == null) { Debug.LogError("[PostMenu] OnReport: CommunityStore.Instance 없음"); return; }
            store.Report(targetId);   // Toast("신고가 접수되었습니다.") 는 Report() 안에서 이미 띄운다 — 중복 구현 금지
        }

        // 곧바로 지우지 않고 확인 모달을 띄운다 (TalkArchiveController.AskDelete 와 동일 관례)
        void OnAskDelete()
        {
            Close();
            if (modalDelete != null)
            {
                modalDelete.SetActive(true);
                modalDelete.transform.SetAsLastSibling();
            }
        }

        void CloseDeleteModal() => UiUtil.SetActive(modalDelete, false);

        void OnConfirmDelete()
        {
            if (!string.IsNullOrEmpty(targetId))
            {
                var store = CommunityStore.Instance;
                if (store == null) Debug.LogError("[PostMenu] OnConfirmDelete: CommunityStore.Instance 없음");
                else store.Delete(targetId);   // 목록에서 지우고 다시 읽는다
            }
            CloseDeleteModal();
        }

        // target 의 top-left 코너를 이 팝업이 속한 Canvas 의 로컬 좌표(원점 좌상단, y 는 아래로
        // 증가 — UiUtil.Place 와 같은 관례)로 변환한다. 카드 ⋮ 는 스크롤 목록 안에, 상세 BtnMore 는
        // 스크롤 Content 안에 있어 위치가 계속 바뀌므로 클릭 시점의 실제 위치를 써야 한다
        // (스크롤 컨테이너 기준 고정 오프셋을 미리 구워둘 수 없다).
        public static Vector2 CanvasTopLeft(RectTransform target)
        {
            if (target == null) return Vector2.zero;
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null) return Vector2.zero;
            var canvasRect = (RectTransform)canvas.transform;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);   // 0=bottom-left, 1=top-left, 2=top-right, 3=bottom-right
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, corners[1]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, cam, out var local);

            Rect r = canvasRect.rect;
            return new Vector2(local.x - r.xMin, r.yMax - local.y);
        }
    }
}
