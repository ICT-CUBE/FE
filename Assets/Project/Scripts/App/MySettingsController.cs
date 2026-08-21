using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 설정 화면(시안 '설정.svg'). 토글 7개 — 상태는 SettingsStore(PlayerPrefs)에 저장/복원한다.
    //
    // 시안의 토글은 트랙+손잡이가 한 장으로 그려진 커스텀 필 스위치라 UnityEngine.UI.Toggle 을 쓰지 않고
    // (그러면 별도 배경/체크마크 그래픽이 더 필요해진다) MyProfileEditController 의 아바타 선택 패턴과
    // 같은 방식 — Button + 스프라이트 통째 교체(toggle_on.png / toggle_off.png)를 쓴다.
    //
    // 7개를 커스텀 [Serializable] 행 클래스 배열 하나로 묶는 대신(Button+Image+string+bool 조합) 병렬
    // 배열로 뺐다 — MyController.avatarSprites, MyProfileEditController.avatarButtons/avatarRings 와 같은
    // 이 코드베이스의 기존 관례(병렬 배열)를 따른 것. 인덱스 i 가 곧 그 토글의 정체성이다.
    public class MySettingsController : MonoBehaviour
    {
        [Header("토글 7개 (인덱스 순서 = 시안 위에서 아래)")]
        [SerializeField] Button[] toggleButtons;
        [SerializeField] Image[] toggleImages;
        [SerializeField] string[] toggleKeys;
        [SerializeField] bool[] toggleDefaults;

        [Header("토글 스프라이트")]
        [SerializeField] Sprite toggleOnSprite;
        [SerializeField] Sprite toggleOffSprite;

        void Awake()
        {
            if (toggleButtons == null) return;
            for (int i = 0; i < toggleButtons.Length; i++)
            {
                int idx = i; // 클로저 캡처
                if (toggleButtons[idx] != null)
                    toggleButtons[idx].onClick.AddListener(() => Flip(idx));
            }
        }

        void OnEnable()
        {
            if (toggleImages == null) return;
            for (int i = 0; i < toggleImages.Length; i++)
                Refresh(i);
        }

        void Flip(int i)
        {
            if (toggleKeys == null || i < 0 || i >= toggleKeys.Length) return;
            bool def = toggleDefaults != null && i < toggleDefaults.Length && toggleDefaults[i];
            bool now = SettingsStore.Get(toggleKeys[i], def);
            SettingsStore.Set(toggleKeys[i], !now);
            Refresh(i);
        }

        void Refresh(int i)
        {
            if (toggleImages == null || i >= toggleImages.Length || toggleImages[i] == null) return;
            if (toggleKeys == null || i >= toggleKeys.Length) return;
            bool def = toggleDefaults != null && i < toggleDefaults.Length && toggleDefaults[i];
            bool on = SettingsStore.Get(toggleKeys[i], def);
            toggleImages[i].sprite = on ? toggleOnSprite : toggleOffSprite;
        }
    }
}
