using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 버튼을 누르면 시트/모달 오브젝트를 켠다.
    // ScreenButton 은 ScreenManager 에 등록된 '화면' 만 전환할 수 있어서,
    // Sheet_* / Modal_* 처럼 등록하지 않는 오버레이는 이걸로 연다.
    // (닫는 건 TapToClose · SheetSwipeToClose 가 맡는다)
    [RequireComponent(typeof(Image))]
    public class OpenSheetButton : MonoBehaviour
    {
        [SerializeField] GameObject sheet;

        void Start()
        {
            var btn = GetComponent<Button>();
            if (btn == null) btn = gameObject.AddComponent<Button>();
            btn.onClick.AddListener(Open);
        }

        void Open()
        {
            if (sheet == null) { Debug.LogWarning($"[OpenSheetButton] {name}: 열 시트가 비어 있음"); return; }
            sheet.SetActive(true);
            sheet.transform.SetAsLastSibling();   // 맨 앞으로
        }
    }
}
