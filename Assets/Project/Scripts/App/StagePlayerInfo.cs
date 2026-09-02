using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

namespace ARZone
{
    // 가로 전체화면 무대 재생 화면(Screen_StagePlayer).
    // 재생 자체는 StageDetailController 가 상시 호스트에 만들어둔 공유 VideoPlayer 로 돌고,
    // 이 컴포넌트는 그 상태를 보여주고(시간·진행바·영상면) 조작한다 (2026-08-30 유저 지시).
    // 상세와 같은 규칙: 재생 중 3초 뒤 버튼 숨김, 영상 영역 탭으로 토글.
    public class StagePlayerInfo : MonoBehaviour
    {
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text sub;

        [Header("재생 컨트롤 (2026-08-30)")]
        [SerializeField] Button btnPlay;
        [SerializeField] TMP_Text timeText;
        [SerializeField] Image progressFill;
        [SerializeField] RawImage videoSurface;
        [SerializeField] RenderTexture renderTexture;
        [SerializeField] Sprite icoPlay;
        [SerializeField] Sprite icoPause;

        float hideAt = -1f;

        static VideoPlayer Player => StageDetailController.Shared;

        void OnEnable()
        {
            var cat = StageCatalog.Instance;
            var d = cat != null ? cat.Selected : null;
            if (d == null)
                Debug.LogWarning("[StagePlayer] 선택된 스테이지가 없어 제목을 못 채움");
            else
            {
                if (title != null) title.text = d.title;
                if (sub != null) sub.text = d.sub;
            }

            if (videoSurface != null && renderTexture != null)
                videoSurface.texture = renderTexture;

            if (btnPlay != null) btnPlay.onClick.AddListener(TogglePlay);
            EnsureTapCatcher();
            SetControls(true);
        }

        void OnDisable()
        {
            if (btnPlay != null) btnPlay.onClick.RemoveListener(TogglePlay);
        }

        void Update()
        {
            var p = Player;

            // 첫 프레임이 그려진 뒤에만 영상 면을 보여준다 (상세와 같은 규칙)
            if (videoSurface != null)
            {
                float a = p != null && p.frame > 0 ? 1f : 0f;
                var c = videoSurface.color;
                if (!Mathf.Approximately(c.a, a)) videoSurface.color = new Color(c.r, c.g, c.b, a);
            }

            double len = p != null ? p.length : 0d;
            double now = p != null ? p.time : 0d;
            if (timeText != null) timeText.text = $"{Fmt(now)} / {Fmt(len)}";
            if (progressFill != null)
            {
                var rt = progressFill.rectTransform;
                float pr = len > 0.001d ? Mathf.Clamp01((float)(now / len)) : 0f;
                rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
                rt.anchorMax = new Vector2(pr, rt.anchorMax.y);
                rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
                rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
            }

            SyncIcon();
            if (hideAt > 0f && Time.time >= hideAt && p != null && p.isPlaying)
                SetControls(false);
        }

        void TogglePlay()
        {
            var p = Player;
            if (p == null) return;
            if (p.isPlaying) { p.Pause(); SetControls(true); }
            else
            {
                p.Play();
                SetControls(true);
                // 상세를 안 거치고 전체화면에서 처음 재생한 경우에만 세션을 연다
                var d = StageCatalog.Instance != null ? StageCatalog.Instance.Selected : null;
                if (ApiMediaSource.StageSessionId == 0 && d != null)
                    ApiMediaSource.BeginStage(d.title, ar: false);
            }
            SyncIcon();
        }

        void SetControls(bool on)
        {
            if (btnPlay != null && btnPlay.gameObject.activeSelf != on)
                btnPlay.gameObject.SetActive(on);
            var p = Player;
            hideAt = on && p != null && p.isPlaying ? Time.time + 3f : -1f;
        }

        void ToggleControls()
            => SetControls(btnPlay != null && !btnPlay.gameObject.activeSelf);

        void SyncIcon()
        {
            if (btnPlay == null || btnPlay.image == null || icoPlay == null || icoPause == null) return;
            var p = Player;
            var want = p != null && p.isPlaying ? icoPause : icoPlay;
            if (btnPlay.image.sprite != want) btnPlay.image.sprite = want;
        }

        // 영상 영역(배경 이미지가 있는 부모) 탭 → 컨트롤 토글. 버튼은 코드로 붙인다
        void EnsureTapCatcher()
        {
            if (videoSurface == null || videoSurface.transform.parent == null) return;
            var go = videoSurface.transform.parent.gameObject;
            var b = go.GetComponent<Button>();
            if (b == null)
            {
                b = go.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
            }
            b.onClick.RemoveListener(ToggleControls);
            b.onClick.AddListener(ToggleControls);
        }

        static string Fmt(double sec)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt((float)sec));
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
