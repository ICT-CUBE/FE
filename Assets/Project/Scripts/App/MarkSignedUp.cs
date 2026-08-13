using UnityEngine;

namespace ARZone
{
    // 이 화면이 뜨면 "가입 완료"로 기록한다. Screen_SignupDone 루트에 붙인다.
    public class MarkSignedUp : MonoBehaviour
    {
        void OnEnable() => LoginModeSwitcher.HasSignedUp = true;
    }
}