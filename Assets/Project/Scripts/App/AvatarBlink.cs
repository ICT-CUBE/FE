using UnityEngine;
using UniVRM10;

namespace ARZone
{
    // VRM 아바타 자동 눈깜빡임 (2026-08-31, 페이스 애니메이션 요청).
    // 2~5초 간격으로 blink 표정을 빠르게 닫았다 연다.
    // 모델에 blink 바인딩이 없으면(현재 avatar_main — 셰이프키 0개) SetWeight 가
    // 아무 것도 안 움직여 조용히 무해하다 — 얼굴 있는 모델로 바꾸는 즉시 살아난다.
    public class AvatarBlink : MonoBehaviour
    {
        [SerializeField, Tooltip("깜빡임 간격 최소(초)")] float minInterval = 2f;
        [SerializeField, Tooltip("깜빡임 간격 최대(초)")] float maxInterval = 5f;
        [SerializeField, Tooltip("감는 시간(초)")] float closeTime = 0.07f;
        [SerializeField, Tooltip("뜨는 시간(초)")] float openTime = 0.12f;

        Vrm10Instance vrm;
        float nextAt;      // 다음 깜빡임 시각
        float phase = -1f; // <0 대기, 0~closeTime 감는 중, 이후 뜨는 중

        static readonly ExpressionKey Blink = ExpressionKey.CreateFromPreset(ExpressionPreset.blink);

        // 아바타 인스턴스마다 한 번만 붙인다 — 스폰 지점 세 곳에서 공용으로 부른다.
        public static void Ensure(GameObject avatar)
        {
            if (avatar != null && avatar.activeInHierarchy && avatar.GetComponent<AvatarBlink>() == null)
                avatar.AddComponent<AvatarBlink>();
        }

        void Start()
        {
            vrm = GetComponent<Vrm10Instance>();
            if (vrm == null) vrm = GetComponentInParent<Vrm10Instance>();
            if (vrm == null) vrm = GetComponentInChildren<Vrm10Instance>();
            if (vrm == null) { Debug.LogWarning("[Blink] Vrm10Instance 를 못 찾음"); enabled = false; return; }
            Schedule();
        }

        void Schedule()
        {
            nextAt = Time.time + Random.Range(minInterval, maxInterval);
            phase = -1f;
        }

        void LateUpdate()
        {
            if (vrm == null || vrm.Runtime == null) return;

            if (phase < 0f)
            {
                if (Time.time < nextAt) return;
                phase = 0f;
            }

            phase += Time.deltaTime;
            float w;
            if (phase <= closeTime) w = phase / closeTime;                       // 감는다
            else if (phase <= closeTime + openTime)                              // 뜬다
                w = 1f - (phase - closeTime) / openTime;
            else { vrm.Runtime.Expression.SetWeight(Blink, 0f); Schedule(); return; }

            vrm.Runtime.Expression.SetWeight(Blink, Mathf.Clamp01(w));
        }

        void OnDisable()
        {
            if (vrm != null && vrm.Runtime != null) vrm.Runtime.Expression.SetWeight(Blink, 0f);
        }
    }
}
