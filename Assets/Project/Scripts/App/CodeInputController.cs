using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

namespace ARZone
{
    // 코드 입력 화면. 키패드 입력을 4칸에 채우고 제한시간·검증을 관리한다.
    // 서버 검증이 붙기 전까지는 expectedCode 로 임시 판정한다.
    public class CodeInputController : MonoBehaviour
    {
        [SerializeField] KeypadController keypad;      // 커스텀 키패드(안 쓰면 비워둔다)
        [SerializeField] TMP_InputField hiddenInput;   // OS 키보드용 — 화면엔 안 보이고 값만 받는다
        [SerializeField] List<TMP_Text> boxes = new List<TMP_Text>();  // Box1~4 안의 Text
        [SerializeField] TMP_Text timerText;
        [SerializeField] float limitSeconds = 38f;

        [Header("이동할 화면 id")]
        [SerializeField] string successScreen = "Hub";
        [SerializeField] string failScreen = "CodeFail";

        [Header("임시 — 서버 붙기 전까지")]
        [SerializeField] string expectedCode = "9999";   // (서버 배선 후 미사용 — 씬 값 보존용으로만 남긴다)
        [SerializeField] int cubeId = 1;                  // 데모 시드. 큐브 스캔이 식별하면 그 값으로

        readonly StringBuilder buf = new StringBuilder();
        float remain;
        bool finished;

        void OnEnable()
        {
            buf.Clear();
            remain = limitSeconds;
            finished = false;
            Redraw();
            if (keypad != null) keypad.OnKey += HandleKey;

            if (hiddenInput != null)
            {
                hiddenInput.characterLimit = boxes.Count;
                hiddenInput.contentType = TMP_InputField.ContentType.IntegerNumber;
                hiddenInput.SetTextWithoutNotify("");
                hiddenInput.onValueChanged.AddListener(OnTyped);
                hiddenInput.ActivateInputField();      // OS 키보드가 올라온다
            }
        }

        void OnDisable()
        {
            if (keypad != null) keypad.OnKey -= HandleKey;
            if (hiddenInput != null) hiddenInput.onValueChanged.RemoveListener(OnTyped);
        }

        void Update()
        {
            if (finished) return;

            remain -= Time.deltaTime;
            if (timerText != null)
                timerText.text = $"코드 입력까지 {Mathf.CeilToInt(Mathf.Max(0f, remain))}초 남았습니다";

            if (remain <= 0f)
            {
                finished = true;
                Fail();
            }
        }

        // OS 키보드로 들어온 값을 4칸에 그대로 반영한다
        void OnTyped(string value)
        {
            if (finished) return;

            buf.Clear();
            foreach (char c in value)
                if (char.IsDigit(c) && buf.Length < boxes.Count) buf.Append(c);

            Redraw();
            if (buf.Length == boxes.Count) Submit();
        }

        // 화면 아무 데나 누르면 다시 키보드를 올린다
        public void FocusKeyboard()
        {
            if (hiddenInput != null && !finished) hiddenInput.ActivateInputField();
        }

        void HandleKey(string key)
        {
            if (finished) return;

            if (key == "<")
            {
                if (buf.Length > 0) buf.Length--;
            }
            else if (buf.Length < boxes.Count)
            {
                buf.Append(key);
            }

            Redraw();

            if (buf.Length == boxes.Count) Submit();
        }

        void Redraw()
        {
            for (int i = 0; i < boxes.Count; i++)
                if (boxes[i] != null)
                    boxes[i].text = i < buf.Length ? buf[i].ToString() : "";
        }

        void Submit()
        {
            finished = true;
            // 서버 인증 (2026-08-25 배선) — POST /cubes/verification {cubeId, ownershipCode}
            // 예전엔 expectedCode 로컬 비교였다. cubeId 는 큐브 스캔이 식별해줄 값인데
            // 지금은 그 연결이 없어 데모 시드(1)를 쓴다. ⚠ 시드 큐브 1은 이미 소유 상태라
            // 서버가 409(CUBE_NOT_REGISTRABLE)를 준다 — 미소유 시드가 생겨야 성공 경로를 밟는다.
            var req = new CubeVerifyReq { cubeId = cubeId, ownershipCode = buf.ToString() };
            ApiClient.Post("api/v1/cubes/verification", JsonUtility.ToJson(req),
                json =>
                {
                    try
                    {
                        var res = JsonUtility.FromJson<CubeVerifyRes>(json);
                        Debug.Log($"[코드] 서버 인증 성공 — cubeOwnershipId={res.cubeOwnershipId}");
                    }
                    catch (System.Exception) { }
                    Done(successScreen, "성공");
                },
                err => Done(failScreen, $"실패({err})"));
        }

        void Fail() => Done(failScreen, "시간 초과");

        void Done(string screen, string reason)
        {
            Debug.Log($"[코드] {reason}");
            if (!string.IsNullOrEmpty(screen)) ScreenManager.Instance?.Show(screen);
        }
    }
}