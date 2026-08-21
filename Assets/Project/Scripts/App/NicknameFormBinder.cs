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

        [Header("링 상태 (선택=보라+체크 배지 포함 / 미선택=회색 — 자식 \"Ring\" 의 스프라이트만 바꾼다)")]
        [SerializeField] Sprite ringOnSprite;
        [SerializeField] Sprite ringOffSprite;

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

            // 선택 표시: 링(항상 보임, 선택=보라/미선택=회색) + 딤(미선택일 때만)
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] == null) continue;
                var img = profiles[i].GetComponent<Image>();
                if (img != null) img.color = Color.white;   // 사진은 원본 색 그대로

                bool isSelected = i == selected;

                var ring = profiles[i].transform.Find("Ring");
                if (ring != null)
                {
                    ring.gameObject.SetActive(true);   // 링은 항상 보인다
                    var ringImg = ring.GetComponent<Image>();
                    if (ringImg != null) ringImg.sprite = isSelected ? ringOnSprite : ringOffSprite;
                }

                var dim = profiles[i].transform.Find("Dim");
                if (dim != null) dim.gameObject.SetActive(!isSelected);   // 딤은 미선택일 때만
            }

            bool ok = r.IsValid && selected >= 0;
            if (next != null) next.interactable = ok;
            if (nextBackground != null)
                nextBackground.color = ok ? DesignTokens.Action.Primary : DesignTokens.Bg.LowSurface;
        }
    }
}