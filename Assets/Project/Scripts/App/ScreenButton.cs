using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 버튼에 붙여서, 누르면 지정 화면으로 이동. Back=true면 뒤로가기.
    [RequireComponent(typeof(Image))]
    public class ScreenButton : MonoBehaviour
    {
        [SerializeField] string targetScreen; // 이동할 화면 id
        [SerializeField] bool back;            // 체크하면 뒤로가기

        void Start()
        {
            var btn = GetComponent<Button>();
            if (btn == null) btn = gameObject.AddComponent<Button>();
            btn.onClick.AddListener(OnClick);
        }

        void OnClick()
        {
            if (ScreenManager.Instance == null) return;
            if (back) ScreenManager.Instance.Back();
            else ScreenManager.Instance.Show(targetScreen);
        }
    }
}