using System;
using System.Globalization;
using UnityEngine;
using TMPro;

namespace ARZone
{
    // Screen_Home 을 서버 홈 응답으로 채운다.
    //
    // 붙는 곳: Canvas/Screen_Home
    // 채우는 것: 말풍선 이름 · D-day · 친밀도 카드
    //
    // 홈은 화면 하나가 전부라 Store 를 두지 않는다(공유할 상대가 없다).
    // 서버가 안 되면 씬에 잡아둔 값이 그대로 남는다 — 빈 화면보다 낫다.
    public class HomeController : MonoBehaviour
    {
        public enum SourceKind { Api, Mock }

        [Header("데이터 출처")]
        [SerializeField] SourceKind source = SourceKind.Api;

        [Header("말풍선")]
        [SerializeField] TMP_Text speakerName;    // SpeechBubble/SpeckerName — 아바타 닉네임

        [Header("상단")]
        [SerializeField] TMP_Text ddayText;       // TopBar/DdayText — "<color=#936AFC>131 Days</color>"

        [Header("친밀도")]
        [SerializeField] IntimacyBar intimacy;    // IntimacyCard 의 IntimacyBar

        IHomeSource src;

        void Awake()
        {
            src = source == SourceKind.Mock ? (IHomeSource)new MockHomeSource() : new ApiHomeSource();
        }

        void OnEnable()
        {
            if (src == null) return;

            src.LoadHome(Bind, err =>
            {
                // ★ 실패해도 씬 값을 지우지 않는다. 홈이 빈 채로 뜨는 것보다 낫다.
                Debug.LogWarning($"[Home] 조회 실패: {err} (씬 값 유지)");
            });
        }

        void Bind(HomeResponseDto dto)
        {
            if (dto == null) return;

            // 아바타 닉네임. 서버가 null 을 줄 수 있다(아직 안 지은 경우) → 그때는 씬 값을 둔다.
            if (speakerName != null && dto.cubeOwnership != null
                && !string.IsNullOrEmpty(dto.cubeOwnership.avatarNickname))
                speakerName.text = dto.cubeOwnership.avatarNickname;

            if (ddayText != null && dto.cubeOwnership != null)
            {
                int days = DaysSince(dto.cubeOwnership.acquiredAt);
                // ★ 씬 원문은 두 줄이다: "<color=#936AFC>131 Days</color>\n\n    with Jennie"
                //   처음엔 통째로 덮어써서 둘째 줄(with …)이 사라졌었다 →
                //   씬 문구는 그대로 두고 '숫자'와 'with 뒤 이름'만 서버 값으로 갈아끼운다.
                // ⚠ 서버가 주는 날짜는 acquiredAt(큐브 획득 시각) 하나뿐이라
                //   'D-day = 큐브와 함께한 일수' 로 읽었다. 다른 의미면 알려줄 것.
                if (days >= 0)
                {
                    string t = ddayText.text;
                    t = System.Text.RegularExpressions.Regex.Replace(t, @"\d+ Days", $"{days} Days");
                    if (dto.cube != null && !string.IsNullOrEmpty(dto.cube.artistName))
                        t = System.Text.RegularExpressions.Regex.Replace(t, @"with .+", "with " + dto.cube.artistName);
                    ddayText.text = t;
                }
            }

            // 빛나는 부분 테두리 파일럿 (2026-08-25 유저 레퍼런스) — 친밀도 카드의 Border 에.
            // 런타임 부착이라 씬은 그대로고, 이 두 줄을 지우면 원래 균일 테두리로 돌아간다.
            if (intimacy != null)
            {
                var border = intimacy.transform.Find("Border");
                if (border != null && border.GetComponent<GlassEdgeGlow>() == null)
                    border.gameObject.AddComponent<GlassEdgeGlow>();
            }

            if (intimacy != null && dto.affinity != null && dto.affinity.level != null)
            {
                intimacy.SetData(dto.affinity.level.level,
                                 dto.affinity.level.levelName,
                                 dto.affinity.currentXp,
                                 dto.affinity.level.levelXp);
            }

            // 프로필 캐릭터 — 2026-08-31 유저 지시로 서버 이미지 덮어쓰기를 끈다.
            // 출처가 둘(서버 URL vs 로컬 선택 ProfileStore)이라 "프로필은 토끼인데 홈은 여자"로
            // 어긋났다. 아이콘은 ProfileIconBinder(로컬 선택) 하나로 통일하고,
            // 가입 때 고른 캐릭터는 NicknameFormBinder 가 ProfileStore 에 동기화한다.
            Debug.Log($"[Home] 채움 — 닉네임 '{dto.cubeOwnership?.avatarNickname}', " +
                      $"Lv.{dto.affinity?.level?.level} {dto.affinity?.level?.levelName}, " +
                      $"XP {dto.affinity?.currentXp}/{dto.affinity?.level?.levelXp}");
        }

        // 정사각 중앙 크롭 + 내접원 밖 투명 처리한 스프라이트를 만든다.
        static Sprite CircleSprite(Texture2D src)
        {
            int s = Mathf.Min(src.width, src.height);
            int ox = (src.width - s) / 2, oy = (src.height - s) / 2;
            var px = src.GetPixels(ox, oy, s, s);
            float r = s * 0.5f, r2 = r * r;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x - r + 0.5f, dy = y - r + 0.5f;
                    if (dx * dx + dy * dy > r2) px[y * s + x].a = 0f;
                }
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }

        // "2026-08-07T09:20:00" (offset 없는 KST) 부터 오늘까지 며칠.
        // 서버가 offset 을 안 붙이므로 DateTime.Parse 가 로컬시간으로 읽는다 — 기기도 KST 라 맞는다.
        // 못 읽으면 -1 을 돌려 호출부가 씬 값을 유지하게 한다.
        static int DaysSince(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return -1;
            if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                                   DateTimeStyles.None, out var t))
            {
                Debug.LogWarning($"[Home] acquiredAt 을 못 읽었다: '{iso}'");
                return -1;
            }
            int days = (int)(DateTime.Now.Date - t.Date).TotalDays;
            return Mathf.Max(0, days);
        }
    }
}
