using TMPro;
using UnityEngine;

namespace ARZone
{
    // 앱 전체 입력 필드 편집성 수정 (2026-08-31 유저 지시: 한 글자씩 지우기·중간 수정).
    //
    // 안드로이드에서 TMP_InputField 는 'Hide Mobile Input' 이 꺼져 있으면(기본값)
    // 인라인 편집이 비활성화되어 ①커서를 글자 사이로 옮길 수 없고 ②지우기가 통째로
    // 날아가는 동작이 된다. 켜면 유니티가 그린 텍스트 안에서 네이티브 IME 편집
    // (커서 탭 이동·한 글자 백스페이스)이 동작한다.
    //
    // 씬에 든 필드 + 각 화면이 코드로 만드는 필드(EnsureInput 계열)를 모두 잡기 위해
    // 화면이 바뀔 때마다 훑는다 — 필드 수가 적어 비용은 무시할 수준.
    static class InputFieldTuner
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            Apply();
            if (ScreenManager.Instance != null)
                ScreenManager.Instance.ScreenChanged += _ => Apply();
        }

        static void Apply()
        {
            var fields = Object.FindObjectsByType<TMP_InputField>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var f in fields) Tune(f);
        }

        // 코드로 늦게 만든 필드(EnsureInput 계열)는 화면 전환 스윕을 비껴가므로
        // 생성 직후 이걸 직접 부른다.
        public static void Tune(TMP_InputField f)
        {
            if (f != null)
            {
                f.shouldHideMobileInput = true;   // 인라인 편집 활성화 (안드로이드)
                f.onFocusSelectAll = false;       // 포커스 시 전체 선택 → 첫 백스페이스에 다 지워지는 것 방지
                f.resetOnDeActivation = false;    // 포커스 잃어도 입력값 유지
                // 커서(캐럿): 보라 세로줄, 1초 주기 깜빡임 (2026-08-31 유저 지시 —
                // "어디 입력하고 있는지 보이게"). caretBlinkRate 는 초당 깜빡임 횟수.
                f.customCaretColor = true;
                f.caretColor = new Color32(0xA8, 0x55, 0xF7, 0xFF);   // #A855F7
                f.caretWidth = 2;
                f.caretBlinkRate = 1f;

                // 런타임 AddComponent 필드의 캐럿 복구 (2026-08-31 "커뮤니티에서 | 안 나옴"):
                // TMP 는 OnEnable 에서 textComponent 가 이미 있을 때만 캐럿 오브젝트를 만든다
                // (TMP_InputField.cs OnEnable). AddComponent 직후엔 textComponent 가 아직
                // 없어 캐럿이 영영 안 생긴다 — 배선 끝난 활성 필드에 캐럿이 없으면 껐다 켜서
                // OnEnable 을 다시 태운다. (비활성 필드는 나중에 켜질 때 정상 생성되므로 제외)
                if (f.isActiveAndEnabled && f.textComponent != null
                    && f.GetComponentInChildren<TMP_SelectionCaret>(true) == null)
                {
                    f.enabled = false;
                    f.enabled = true;
                }
            }
        }
    }
}
