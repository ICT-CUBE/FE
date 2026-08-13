using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 레벨업 결과 화면. 레벨에 따라 칩·제목·아트워크·문구가 바뀐다.
    // 와프의 Lv.2 Friend / Lv.3 Close / Lv.4 Special 세 장을 이 한 화면으로 처리.
    public class LevelUpResultController : MonoBehaviour
    {
        [Serializable]
        public class LevelArt
        {
            public int level = 2;
            public string title = "Friend";
            public Sprite artwork;
            public Sprite deco;      // 칩 위 장식 — Lv.2 별1 · Lv.3 별2 · Lv.4 별·왕관·별
            [TextArea] public string desc = "친구가 되었어요.\n더 다양한 일상 이야기를 나눌 수 있어요.";
        }

        [SerializeField] TMP_Text levelChipLabel;   // "Lv.3"
        [SerializeField] TMP_Text levelTitle;       // "Close"
        [SerializeField] Image artwork;
        [SerializeField] Image deco;                // 칩 위 장식 (레벨마다 폭이 다르다)
        [SerializeField] TMP_Text resultDesc;
        [SerializeField] Button btnOk;
        [SerializeField] string returnScreen = "Mission";

        [SerializeField] LevelArt[] levels;

        void OnEnable()
        {
            if (btnOk != null) btnOk.onClick.AddListener(OnOk);
            Refresh();
        }

        void OnDisable()
        {
            if (btnOk != null) btnOk.onClick.RemoveListener(OnOk);
        }

        void Refresh()
        {
            var c = MissionCatalog.Instance;
            if (c == null) { Debug.LogError("[LevelUpResult] MissionCatalog 이 씬에 없음"); return; }

            // 레벨업 직후이므로 '도달한 레벨' 을 보여준다
            int reached = c.level;
            LevelArt art = null;
            if (levels != null)
                foreach (var l in levels)
                    if (l != null && l.level == reached) { art = l; break; }

            if (levelChipLabel != null) levelChipLabel.text = $"Lv.{reached}";
            if (levelTitle != null) levelTitle.text = art != null ? art.title : c.levelTitle;
            if (resultDesc != null && art != null) resultDesc.text = art.desc;
            if (artwork != null && art != null && art.artwork != null) artwork.sprite = art.artwork;

            // 장식은 레벨마다 폭이 달라서(별1 / 별2 / 별·왕관·별) 스프라이트 크기를 그대로 쓴다.
            // 에셋이 3x 라 PPU 300 으로 임포트해뒀고, SetNativeSize 가 곧 시안 크기가 된다.
            if (deco != null)
            {
                var d = art != null ? art.deco : null;
                deco.gameObject.SetActive(d != null);
                if (d != null) { deco.sprite = d; deco.SetNativeSize(); }
            }
        }

        void OnOk()
        {
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(returnScreen);
        }
    }
}
