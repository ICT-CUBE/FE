using System.Collections;
using UnityEngine;

namespace ARZone
{
    // 스플래시를 일정 시간 보여준 뒤 다음 화면으로 넘어간다.
    // Screen_Splash 루트에 붙인다.
    public class SplashTimer : MonoBehaviour
    {
        [SerializeField] float seconds = 2f;
        [SerializeField] string nextScreen = "Onboarding";

        void OnEnable() => StartCoroutine(Wait());

        IEnumerator Wait()
        {
            yield return new WaitForSeconds(seconds);

            if (!string.IsNullOrEmpty(nextScreen))
                ScreenManager.Instance?.Show(nextScreen);
        }
    }
}