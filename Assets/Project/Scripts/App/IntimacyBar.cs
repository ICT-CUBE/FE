using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 친밀도 진행바 + 텍스트를 값에 맞춰 갱신.
    // 붙는 곳: Screen_Home/IntimacyCard.
    //
    // 2026-08-25: 바를 디자이너 제공 '반짝이는 로딩창'(216프레임, Resources/ProgressBar)으로 교체.
    //   프레임마다 채움 길이가 달라(0→100% 구운 애니메이션) fillAmount 대신
    //   **실측 채움표(FrameFill)** 에서 서버 비율(currentXp/levelXp)에 맞는 프레임까지 재생한다.
    //   → 바 끝이 항상 실제 숫자와 일치한다 (유저 지시 "실제 숫자랑 맞게").
    //   프레임이 없으면(리소스 미포함 빌드) 예전 fillAmount 방식으로 돈다.
    public class IntimacyBar : MonoBehaviour
    {
        [SerializeField] Image fill;             // ProgressFill 드래그 (프레임 모드에선 부모 트랙에 그린다)
        [SerializeField] TMP_Text levelText;     // LevelText 드래그
        [SerializeField] TMP_Text progressText;  // ProgressText 드래그
        [SerializeField] TMP_Text progressValue; // ProgressValue 드래그 ("730 / 800")
        [SerializeField] float fillSpeed = 2f;   // (폴백 모드) 채워지는 속도
        // 프레임 n(1~216)의 실측 채움 비율 — 에셋에서 알파>200 기준으로 잰 값
        static readonly float[] FrameFill = {
            0.0362f, 0.0436f, 0.0515f, 0.0589f, 0.0669f, 0.0742f, 0.0816f, 0.0890f, 0.0963f, 0.1043f, 0.1117f, 0.1190f,
            0.1264f, 0.1331f, 0.1405f, 0.1479f, 0.1552f, 0.1620f, 0.1693f, 0.1761f, 0.1834f, 0.1902f, 0.1969f, 0.2043f,
            0.2110f, 0.2178f, 0.2245f, 0.2313f, 0.2380f, 0.2448f, 0.2509f, 0.2577f, 0.2644f, 0.2712f, 0.2785f, 0.2853f,
            0.2920f, 0.2982f, 0.3037f, 0.3098f, 0.3153f, 0.3209f, 0.3264f, 0.3319f, 0.3374f, 0.3429f, 0.3491f, 0.3546f,
            0.3601f, 0.3656f, 0.3706f, 0.3761f, 0.3816f, 0.3865f, 0.3914f, 0.3963f, 0.4018f, 0.4061f, 0.4110f, 0.4160f,
            0.4202f, 0.4252f, 0.4294f, 0.4337f, 0.4380f, 0.4417f, 0.4460f, 0.4497f, 0.4540f, 0.4577f, 0.4613f, 0.4644f,
            0.4681f, 0.4712f, 0.4748f, 0.4779f, 0.4804f, 0.4834f, 0.4865f, 0.4890f, 0.4914f, 0.4939f, 0.4963f, 0.4988f,
            0.5006f, 0.5031f, 0.5049f, 0.5067f, 0.5086f, 0.5098f, 0.5117f, 0.5129f, 0.5135f, 0.5147f, 0.5160f, 0.5172f,
            0.5178f, 0.5184f, 0.5196f, 0.5202f, 0.5215f, 0.5221f, 0.5227f, 0.5233f, 0.5239f, 0.5245f, 0.5252f, 0.5258f,
            0.5264f, 0.5258f, 0.5252f, 0.5252f, 0.5245f, 0.5239f, 0.5233f, 0.5227f, 0.5227f, 0.5227f, 0.5227f, 0.5227f,
            0.5227f, 0.5233f, 0.5239f, 0.5245f, 0.5258f, 0.5264f, 0.5276f, 0.5288f, 0.5307f, 0.5319f, 0.5337f, 0.5350f,
            0.5368f, 0.5387f, 0.5405f, 0.5423f, 0.5442f, 0.5466f, 0.5491f, 0.5521f, 0.5546f, 0.5577f, 0.5607f, 0.5638f,
            0.5675f, 0.5706f, 0.5742f, 0.5779f, 0.5816f, 0.5859f, 0.5896f, 0.5939f, 0.5982f, 0.6037f, 0.6086f, 0.6135f,
            0.6184f, 0.6239f, 0.6288f, 0.6337f, 0.6387f, 0.6436f, 0.6491f, 0.6540f, 0.6595f, 0.6650f, 0.6706f, 0.6761f,
            0.6822f, 0.6883f, 0.6945f, 0.7006f, 0.7067f, 0.7135f, 0.7196f, 0.7264f, 0.7331f, 0.7399f, 0.7466f, 0.7534f,
            0.7607f, 0.7663f, 0.7724f, 0.7785f, 0.7847f, 0.7902f, 0.7963f, 0.8025f, 0.8092f, 0.8153f, 0.8215f, 0.8282f,
            0.8350f, 0.8417f, 0.8485f, 0.8552f, 0.8626f, 0.8699f, 0.8767f, 0.8840f, 0.8914f, 0.8988f, 0.9061f, 0.9135f,
            0.9202f, 0.9276f, 0.9350f, 0.9417f, 0.9393f, 0.9466f, 0.9540f, 0.9613f, 0.9687f, 0.9767f, 0.9840f, 0.9920f,
        };

        float target;
        bool frameMode;
        bool prepared;
        Sprite currentFrame;         // Resources 에서 온 원본 프레임
        Sprite currentCrop;          // 잘라 만든 표시용 스프라이트

        void Start()
        {
            if (fill != null) target = fill.fillAmount;
        }

        // 서버 홈 응답에서 온다: SetData(3, "Close", 730, 800)
        public void SetData(int level, string title, int current, int max)
        {
            if (max > 0 && current > max)
                Debug.LogWarning($"[Home] 친밀도 XP 가 레벨 상한을 넘는다 ({current}/{max}) — 서버 확인 필요");

            target = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

            if (levelText != null) levelText.text = title;
            if (progressText != null)
                progressText.text = $"Lv.{level + 1} 까지 {Mathf.Max(0, max - current):N0} 남았어요";
            if (progressValue != null) progressValue.text = $"{current:N0} / {max:N0}";

            TryFrameMode();
        }

        // ── 프레임 모드 ──
        //
        // ★ 2026-08-25 수정: 처음엔 프레임을 트랙에 통째로 그렸는데, 에셋의 반짝이 halo 가
        //   채움 끝 뒤로 150px 이상 구워져 있어(실측) 정지컷에선 끝에 혹처럼 튀어 보였다.
        //   → 프레임에서 '바 띠(y35~74)'만, '비율 지점까지만' 잘라 채움으로 쓴다.
        //     halo 는 잘려나가고, 트랙은 씬 원래 배경 그대로다. 길이도 픽셀 단위로 정확해진다.

        const float TexW = 1630f;          // 에셋 캔버스
        // 바 띠는 PIL(위 원점) 실측으로 y 35~74 인데, 유니티 텍스처 좌표는 '아래' 원점이라
        // 뒤집어 넣는다: 100 - 74 = 26 부터 높이 40.
        const float BandY = 26f;
        const float BandH = 40f;

        static Sprite LoadFrame(int n) => Resources.Load<Sprite>($"ProgressBar/progress_frame-{n}");

        void TryFrameMode()
        {
            if (fill == null) return;

            // 비율보다 채움이 '길거나 같은' 첫 프레임 — 자를 지점까지 반드시 꽉 차 있다
            int frame = FrameFill.Length;
            for (int i = 0; i < FrameFill.Length; i++)
                if (FrameFill[i] >= target) { frame = i + 1; break; }

            var frameSprite = LoadFrame(frame);
            if (frameSprite == null) { frameMode = false; return; }   // 리소스 없음 → 폴백
            frameMode = true;

            var rt = fill.rectTransform;
            if (!prepared)
            {
                prepared = true;
                // stretch 자식이던 채움을 '왼쪽부터 절대 폭' 으로 바꾼다 (트랙은 안 건드린다)
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.offsetMin = new Vector2(0f, 0f);
                rt.offsetMax = new Vector2(rt.offsetMax.x, 0f);
                fill.type = Image.Type.Simple;
                fill.preserveAspect = false;
                fill.color = Color.white;
                var ts = fill.GetComponent<TokenStyle>();
                if (ts != null) ts.enabled = false;    // 프레임 원색을 덮어쓰지 않게
            }

            float trackW = ((RectTransform)rt.parent).rect.width;
            if (trackW < 1f) trackW = 212f;            // 켜진 첫 프레임엔 rect 가 0 일 수 있다

            // 비율 지점까지만 자른 스프라이트 (halo 는 이 뒤에 있어 잘려나간다)
            float cutPx = Mathf.Clamp(target * TexW, 0f, TexW);
            fill.enabled = cutPx >= 1f;
            if (fill.enabled)
            {
                var cropped = Sprite.Create(frameSprite.texture,
                                            new Rect(0f, BandY, cutPx, BandH),
                                            new Vector2(0f, 0.5f), 100f);
                if (currentCrop != null) Destroy(currentCrop);
                currentCrop = cropped;
                fill.sprite = cropped;
                rt.sizeDelta = new Vector2(trackW * target, rt.sizeDelta.y);
            }

            if (currentFrame != null && currentFrame != frameSprite)
                Resources.UnloadAsset(currentFrame.texture);   // 이전 프레임 텍스처는 내린다
            currentFrame = frameSprite;
        }

        void Update()
        {
            if (frameMode || fill == null) return;
            // 폴백: 예전 fillAmount 방식
            fill.fillAmount = Mathf.MoveTowards(fill.fillAmount, target, fillSpeed * Time.deltaTime);
        }
    }
}
