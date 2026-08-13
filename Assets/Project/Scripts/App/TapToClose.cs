using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ARZone
{
    // 확인 버튼 대신, 여백(배경)을 탭하면 닫는다.
    // 카드·버튼 같은 자식 Graphic 이 레이캐스트를 먹으므로 정말 빈 곳만 반응한다.
    //
    //   returnScreen 이 있으면  → ScreenManager 로 그 화면으로 이동
    //   비어 있으면            → closeTarget(없으면 부모)을 SetActive(false)
    //
    // 붙이는 곳 = 화면 전체를 덮는 Graphic (Screen_*/Bg, Sheet_*/Dim)
    [RequireComponent(typeof(Graphic))]
    public class TapToClose : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] string returnScreen = "";
        [SerializeField] GameObject closeTarget;

        void Awake()
        {
            // 여백이 탭을 받아야 한다. 알파 0 이어도 raycastTarget 이 켜져 있으면 잡힌다.
            var g = GetComponent<Graphic>();
            if (g != null) g.raycastTarget = true;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!string.IsNullOrEmpty(returnScreen))
            {
                if (ScreenManager.Instance != null) ScreenManager.Instance.Show(returnScreen);
                else Debug.LogError("[TapToClose] ScreenManager 가 씬에 없음");
                return;
            }

            var t = closeTarget != null ? closeTarget
                  : transform.parent != null ? transform.parent.gameObject : null;
            if (t != null) t.SetActive(false);
        }
    }
}
