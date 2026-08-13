using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 닉네임 화면의 입력 검증·카운터·캐릭터 선택을 UI에 연결한다.
    public class NicknameFormBinder : MonoBehaviour
    {
        [Header("입력")]
        [SerializeField] TMP_InputField nickname;
        [SerializeField] List<Button> profiles = new List<Button>();

        [Header("표시")]
        [SerializeField] TMP_Text hintText;      // 평소엔 안내, 틀리면 빨간 에러
        [SerializeField] TMP_Text counterText;   // "3 / 10"
        [SerializeField] string hint = "한글, 영문, 숫자 조합만 사용 가능합니다.";

        [Header("다음 버튼")]
        [SerializeField] Button next;
        [SerializeField] Image nextBackground;

        int selected = -1;

        void OnEnable()
        {
            if (nickname != null) nickname.onValueChanged.AddListener(_ => Refresh());

            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] == null) continue;
                int idx = i;                       // 클로저 캡처 주의
                profiles[i].onClick.AddListener(() =>
                {
                    selected = idx;
                    AvatarNameBinder.SelectedProfile = idx;   // 다음 화면(큐브 인식 완료)이 읽는다
                    Refresh();
                });
            }
            Refresh();
        }

        void OnDisable()
        {
            if (nickname != null) nickname.onValueChanged.RemoveAllListeners();
            foreach (var b in profiles) if (b != null) b.onClick.RemoveAllListeners();
        }

        void Refresh()
        {
            string text = nickname != null ? nickname.text : "";
            var r = Validators.Nickname(text);

            if (counterText != null)
                counterText.text = $"{text.Length} / {Validators.NicknameMaxLength}";

            if (hintText != null)
            {
                hintText.text  = r.ShowError ? r.Message : hint;
                hintText.color = DesignTokens.Primitive.Status.Red;   // 시안: 항상 빨강
            }

            // 선택된 캐릭터만 밝게
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] == null) continue;
                var img = profiles[i].GetComponent<Image>();
                if (img != null) img.color = Color.white;   // 사진은 원본 색 그대로

                // 선택 표시는 원 외곽선으로 (자식 "Ring" 이 있으면 켜고 끈다)
                var ring = profiles[i].transform.Find("Ring");
                if (ring != null) ring.gameObject.SetActive(i == selected);
            }

            bool ok = r.IsValid && selected >= 0;
            if (next != null) next.interactable = ok;
            if (nextBackground != null)
                nextBackground.color = ok ? DesignTokens.Action.Primary : DesignTokens.Bg.LowSurface;
        }
    }
}