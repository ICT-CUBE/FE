using System;
using UnityEngine;
using UniVRM10;

namespace ARZone
{
    // VRM 아바타 립싱크 (2026-08-27, AR 감상용).
    // 재생 중인 AudioSource 의 음량(RMS)으로 입 모프('aa')를 연다 —
    // gate(현재 가사 줄이 있는 동안만 true)로 간주곡·전주에서는 입을 다문다.
    // 음소 단위 분석은 아니지만 곡을 따라 입이 열리고 닫혀 데모에선 충분히 노래하는 걸로 보인다.
    public class AvatarLipSync : MonoBehaviour
    {
        public AudioSource source;
        public Func<bool> gate;                 // null 이면 항상 허용

        [SerializeField, Tooltip("음량→입 벌림 감도")] float gain = 6f;
        [SerializeField, Tooltip("입 여닫는 속도")] float speed = 12f;

        Vrm10Instance vrm;
        readonly float[] buf = new float[256];
        float level;

        static readonly ExpressionKey Aa = ExpressionKey.CreateFromPreset(ExpressionPreset.aa);

        void Start()
        {
            vrm = GetComponent<Vrm10Instance>();
            if (vrm == null) vrm = GetComponentInParent<Vrm10Instance>();
            if (vrm == null) Debug.LogWarning("[LipSync] Vrm10Instance 를 못 찾음 — 입이 안 움직인다");
        }

        void LateUpdate()
        {
            if (vrm == null || vrm.Runtime == null) return;

            float target = 0f;
            if (source != null && source.isPlaying && (gate == null || gate()))
            {
                source.GetOutputData(buf, 0);
                double s = 0;
                foreach (var v in buf) s += v * v;
                target = Mathf.Clamp01(Mathf.Sqrt((float)(s / buf.Length)) * gain);
            }
            level = Mathf.Lerp(level, target, Time.deltaTime * speed);
            vrm.Runtime.Expression.SetWeight(Aa, level);
        }

        void OnDisable()
        {
            level = 0f;
            if (vrm != null && vrm.Runtime != null) vrm.Runtime.Expression.SetWeight(Aa, 0f);
        }
    }
}
