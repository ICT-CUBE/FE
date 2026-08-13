using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 친밀도 현황 화면. 레벨·경험치·혜택·활동 목록을 채운다.
    public class IntimacyDetailController : MonoBehaviour
    {
        [Header("레벨")]
        [SerializeField] TMP_Text levelText;      // "Lv.3 Close"
        [SerializeField] TMP_Text percentText;    // PercentChip 안의 라벨
        [SerializeField] Image expFill;           // Image Type = Filled
        [SerializeField] TMP_Text expText;        // "1,245 / 2,000"
        [SerializeField] TMP_Text tipText;

        [Header("활동 버튼 (시안 3상태)")]
        [SerializeField] Sprite btnDone;   // bg_round8        — '완료됨' 채움
        [SerializeField] Sprite btnGo;     // bg_round8_stroke — '바로가기' 테두리

        [Header("레벨 타임라인")]
        [SerializeField] Image trackFill;              // 현재 레벨까지 채워진 선
        [SerializeField] Image[] nodes;                // Node1~4
        [Header("타임라인 노드 이미지")]
        [SerializeField] Sprite nodeSpriteDone;      // 체크(브이)
        [SerializeField] Sprite nodeSpriteCurrent;   // 현재 (흰 링)
        [SerializeField] Sprite nodeSpriteLocked;    // 잠김 (회색)
        [SerializeField] TMP_Text[] nodeLabels;        // Label1~4
        [SerializeField] TMP_Text unlockChipLabel;     // "Lv.4 Special 단계 해금 보상"
        [SerializeField] Transform unlockRow;          // 해금 보상 3칸

        [Header("타임라인 색")]
        [SerializeField] Color nodeDone = new Color(0.486f, 0.227f, 0.929f, 1f);   // ActionPrimary
        [SerializeField] Color nodeLocked = new Color(0.141f, 0.157f, 0.192f, 1f); // Neutral 600
        [SerializeField] Color labelOn = Color.white;
        [SerializeField] Color labelOff = new Color(0.447f, 0.471f, 0.525f, 1f);

        [Header("목록")]
        [SerializeField] GameObject benefitPrefab;   // BenefitCard
        [SerializeField] Transform benefitRow;
        [SerializeField] GameObject activityPrefab;  // ActivityRow
        [SerializeField] Transform todayList;
        [SerializeField] Transform levelList;

        void OnEnable() => Refresh();

        void Refresh()
        {
            var c = MissionCatalog.Instance;
            if (c == null) { Debug.LogError("[IntimacyDetail] MissionCatalog 이 씬에 없음"); return; }

            if (levelText != null) levelText.text = $"Lv.{c.level} {c.levelTitle}";
            if (percentText != null) percentText.text = $"{Mathf.RoundToInt(c.Ratio * 100f)}%";
            if (expFill != null) expFill.fillAmount = c.Ratio;
            if (expText != null) expText.text = $"{c.exp:N0} / {c.expMax:N0}";
            if (tipText != null) tipText.text = $"조금만 더 힘내세요! 다음 단계까지 {c.Remain} XP";

            BuildBenefits(c);
            BuildTimeline(c);
            BuildActivities(todayList, c.todayActivities);
            BuildActivities(levelList, c.levelChanges);
        }

        // 와프의 Lv.1~4 가로 타임라인. 현재 레벨까지 채우고 나머지는 잠금 표시.
        void BuildTimeline(MissionCatalog c)
        {
            int cur = Mathf.Clamp(c.level, 1, nodes != null ? nodes.Length : 4);

            if (nodes != null)
            {
                for (int i = 0; i < nodes.Length; i++)
                {
                    if (nodes[i] == null) continue;
                    bool done = (i + 1) <= cur;
                    bool current = (i + 1) == cur;

                    // 에셋에 체크·링이 그려져 있으므로 색으로 칠하지 않고 스프라이트를 바꾼다.
                    // 잠긴 단계는 배경 없이 자물쇠만 보인다.
                    if (!done)
                    {
                        nodes[i].color = new Color(1f, 1f, 1f, 0f);
                    }
                    else
                    {
                        var spr = current ? nodeSpriteCurrent : nodeSpriteDone;
                        if (spr != null)
                        {
                            nodes[i].sprite = spr;
                            nodes[i].color = Color.white;
                        }
                        else nodes[i].color = nodeDone;
                    }

                    // 체크는 node_done 스프라이트에 이미 그려져 있으므로 자식 Check 는 쓰지 않는다.
                    // 자물쇠만 '아직 못 간 단계'에 보인다.
                    for (int k = 0; k < nodes[i].transform.childCount; k++)
                    {
                        var child = nodes[i].transform.GetChild(k);
                        child.gameObject.SetActive(child.name == "Lock" && !done);
                    }
                }
            }

            if (nodeLabels != null)
            {
                for (int i = 0; i < nodeLabels.Length; i++)
                {
                    if (nodeLabels[i] == null) continue;
                    string name = (c.levelNames != null && i < c.levelNames.Count) ? c.levelNames[i] : "";
                    nodeLabels[i].text = $"Lv.{i + 1}\n{name}";
                    nodeLabels[i].color = (i + 1) == cur ? labelOn : labelOff;
                }
            }

            // 채워진 선 = 첫 노드에서 현재 노드까지
            if (trackFill != null && nodes != null && nodes.Length > 1)
            {
                float ratio = (cur - 1) / (float)(nodes.Length - 1);
                var rt = trackFill.rectTransform;
                var full = nodes[nodes.Length - 1].rectTransform.anchoredPosition.x
                         - nodes[0].rectTransform.anchoredPosition.x;
                rt.sizeDelta = new Vector2(Mathf.Max(3f, full * ratio), rt.sizeDelta.y);
            }

            int next = Mathf.Min(cur + 1, c.levelNames != null ? c.levelNames.Count : 4);
            string nextName = (c.levelNames != null && next - 1 < c.levelNames.Count) ? c.levelNames[next - 1] : "";
            if (unlockChipLabel != null)
                unlockChipLabel.text = $"Lv.{next} {nextName} 단계 해금 보상";

            BuildUnlocks(c);
        }

        void BuildUnlocks(MissionCatalog c)
        {
            if (benefitPrefab == null || unlockRow == null) return;
            UiUtil.ClearChildren(unlockRow);

            foreach (var b in c.nextUnlocks)
            {
                var go = Instantiate(benefitPrefab, unlockRow);
                go.SetActive(true);
                UiUtil.SetText(go, "Title", b.title);
                UiUtil.SetText(go, "Desc", b.desc);
                UiUtil.SetSprite(go, "Icon", b.icon);
                UiUtil.SetActive(go, "Border", false);   // 해금 3칸은 테두리 없음(혜택 칸은 유지)

                // 혜택 칸(125)보다 낮은 자리라 카드가 박스 밖으로 삐져나온다 → 줄 높이에 맞춘다
                var rt = go.transform as RectTransform;
                var rowRt = unlockRow as RectTransform;
                if (rt != null && rowRt != null)
                    rt.sizeDelta = new Vector2(rt.sizeDelta.x, rowRt.rect.height);
            }
        }

        void BuildBenefits(MissionCatalog c)
        {
            if (benefitPrefab == null || benefitRow == null) return;
            UiUtil.ClearChildren(benefitRow);

            foreach (var b in c.benefits)
            {
                var go = Instantiate(benefitPrefab, benefitRow);
                go.SetActive(true);
                UiUtil.SetText(go, "Title", b.title);
                UiUtil.SetText(go, "Desc", b.desc);
                UiUtil.SetSprite(go, "Icon", b.icon);
            }
        }

        void BuildActivities(Transform container, System.Collections.Generic.List<MissionData> list)
        {
            if (activityPrefab == null || container == null) return;
            UiUtil.EnsureVertical(container, 8f);
            UiUtil.ClearChildren(container);

            foreach (var m in list)
            {
                var go = Instantiate(activityPrefab, container);
                go.SetActive(true);
                UiUtil.SetText(go, "Title", m.title);
                UiUtil.SetText(go, "Reward", $"{m.reward}");
                UiUtil.SetSprite(go, "Icon/IconGlyph", m.icon);

                // 시안: 완료한 활동은 링·아이콘이 보라, 아직이면 회색
                var accent = m.claimable ? new Color(0.658824f, 0.333333f, 0.968627f, 1f)
                                         : new Color(0.447059f, 0.470588f, 0.525490f, 1f);
                var ringT = go.transform.Find("Icon");
                if (ringT != null)
                {
                    var ring = ringT.GetComponent<Image>();
                    if (ring != null) ring.color = accent;
                    var glyphT = ringT.Find("IconGlyph");
                    var glyph = glyphT != null ? glyphT.GetComponent<Image>() : null;
                    if (glyph != null && glyph.sprite != null) glyph.color = accent;
                }

                var btn = go.transform.Find("BtnAction");
                if (btn == null) continue;

                // 시안: 완료됨 = #181A20 채움 + 흰 글씨 / 바로가기 = #A855F7 테두리 + 보라 글씨
                bool done = m.claimable;
                var violet = new Color(0.658824f, 0.333333f, 0.968627f, 1f);   // #A855F7
                var btnImg = btn.GetComponent<Image>();
                if (btnImg != null)
                {
                    var spr = done ? btnDone : btnGo;
                    if (spr != null) btnImg.sprite = spr;
                    btnImg.type = Image.Type.Sliced;
                    btnImg.color = done ? new Color(0.094118f, 0.101961f, 0.125490f, 1f) : violet;
                }

                var label = btn.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = done ? "완료됨" : "바로가기";
                    label.color = done ? Color.white : violet;
                }

                var b = btn.GetComponent<Button>();
                if (b == null) continue;
                var captured = m;
                b.interactable = !m.claimable;
                b.onClick.AddListener(() =>
                {
                    if (!string.IsNullOrEmpty(captured.goScreen) && ScreenManager.Instance != null)
                        ScreenManager.Instance.Show(captured.goScreen);
                });
            }
        }
    }
}