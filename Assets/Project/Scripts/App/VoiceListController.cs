using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 전체 음성 대화 (시안 '음성 대화 목록' / '음성 대화 목록 삭제').
    // 행 실측(행 top 기준): 재생 (23.33,-31.5) 10.67×12 · 제목 14px 잉크 -19.86 · 파형 (48,-45) 114×10
    //                      길이 12px 잉크 -45.68 · 날짜 12px 우측 · ⋮ (365.6,-23.11) 28.8²
    // ⋮ 를 누르면 그 행 아래에 '삭제하기' 팝오버가 뜨고, 누르면 삭제 확인 모달이 뜬다 (유저 지시).
    public class VoiceListController : MonoBehaviour
    {
        [Header("목록")]
        [SerializeField] RectTransform body;         // Screen_VoiceList/VoiceListBody
        [SerializeField] TMP_Text fontSource;
        [SerializeField] float rowPitch = 83f;
        [SerializeField] List<VoiceHomeController.Session> sessions = new List<VoiceHomeController.Session>();

        [Header("⋮ 팝오버")]
        [SerializeField] RectTransform rowMenu;      // Popover_VoiceRowMenu
        [SerializeField] Button btnMenuDelete;
        [SerializeField] float menuX = 268f;         // 시안 (268,227) — 두 번째 행 기준
        [SerializeField] float menuDrop = 38f;       // 행 top 에서 팝오버 top 까지

        [Header("삭제 확인")]
        [SerializeField] GameObject modalDelete;
        [SerializeField] Button btnDeleteCancel;
        [SerializeField] Button btnDeleteConfirm;

        [Header("스프라이트")]
        [SerializeField] Sprite playIcon;
        [SerializeField] Sprite waveSprite;          // ic_wave_row
        [SerializeField] Sprite dotsSprite;          // ic_more_vert

        int menuTarget = -1;

        void OnEnable()
        {
            if (btnMenuDelete != null) btnMenuDelete.onClick.AddListener(OpenDelete);
            if (btnDeleteCancel != null) btnDeleteCancel.onClick.AddListener(CloseDelete);
            if (btnDeleteConfirm != null) btnDeleteConfirm.onClick.AddListener(ConfirmDelete);
            CloseMenu();
            CloseDelete();
            Build();
        }

        void OnDisable()
        {
            if (btnMenuDelete != null) btnMenuDelete.onClick.RemoveListener(OpenDelete);
            if (btnDeleteCancel != null) btnDeleteCancel.onClick.RemoveListener(CloseDelete);
            if (btnDeleteConfirm != null) btnDeleteConfirm.onClick.RemoveListener(ConfirmDelete);
        }

        void Build()
        {
            if (body == null) { Debug.LogError("[VoiceList] body 가 비어 있다"); return; }
            UiUtil.ClearChildren(body);
            if (sessions == null || sessions.Count == 0) sessions = VoiceHomeController.MockAll;

            float w = body.sizeDelta.x > 1f ? body.sizeDelta.x : 412f;   // OnEnable 시점엔 rect 가 아직 0 이다
            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                var row = UiUtil.MakeNode(body, "Row" + i);
                UiUtil.Place(row, 0f, i * rowPitch, w, rowPitch);

                var play = UiUtil.MakeImage(row, "Play", playIcon, DesignTokens.Text.Primary);
                UiUtil.Place(play, 23.33f, 31.5f, 10.67f, 12f);

                var title = UiUtil.MakeText(row.transform, "Title", DesignTokens.Type.Body3,
                                            DesignTokens.Text.Primary, TextAlignmentOptions.MidlineLeft);
                title.fontSize = 14f;
                title.text = s.title;
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.overflowMode = TextOverflowModes.Ellipsis;
                UiUtil.Place(title, 48.31f, 14.58f, 275f, 23f);
                CopyFont(title);

                if (waveSprite != null)
                {
                    var wave = UiUtil.MakeImage(row, "Wave", waveSprite, Color.white);
                    UiUtil.Place(wave, 48f, 45f, 114f, 10f);
                }

                var len = UiUtil.MakeText(row.transform, "Length", DesignTokens.Type.Caption1,
                                          DesignTokens.Text.Secondary, TextAlignmentOptions.MidlineLeft);
                len.fontSize = 12f;
                len.text = s.length;
                UiUtil.Place(len, 174.66f, 41.41f, 80f, 19f);
                CopyFont(len);

                var when = UiUtil.MakeText(row.transform, "When", DesignTokens.Type.Caption1,
                                           DesignTokens.Text.Primary, TextAlignmentOptions.MidlineRight);
                when.fontSize = 12f;
                when.text = s.when;
                UiUtil.Place(when, 258.77f, 28.23f, 100f, 19f);
                CopyFont(when);

                // ⚠ 6번째 인자(raycast) 를 안 주면 기본값 false 라 Button 을 붙여도 탭이 안 닿는다
                var dots = UiUtil.MakeImage(row, "More", dotsSprite, DesignTokens.Text.Primary,
                                            Image.Type.Simple, true);
                UiUtil.Place(dots, 365.6f, 23.11f, 28.8f, 28.8f);
                int k = i;
                var mb = dots.gameObject.AddComponent<Button>();
                mb.targetGraphic = dots;
                mb.transition = Selectable.Transition.None;
                mb.onClick.AddListener(() => ToggleMenu(k));

                var line = UiUtil.MakeImage(row, "Line", null, DesignTokens.Bg.Surface);
                UiUtil.Place(line, 20f, rowPitch - 4f, w - 40f, 1f);
            }
        }

        // 팝오버는 Canvas 직계라 화면 절대 좌표로 옮긴다 (행 top = 106 + i×피치)
        void ToggleMenu(int i)
        {
            if (rowMenu == null) return;
            if (menuTarget == i && rowMenu.gameObject.activeSelf) { CloseMenu(); return; }

            menuTarget = i;
            float top = (body != null ? -body.anchoredPosition.y : 106f) + i * rowPitch + menuDrop;
            rowMenu.anchorMin = rowMenu.anchorMax = new Vector2(0f, 1f);
            rowMenu.pivot = new Vector2(0f, 1f);
            rowMenu.anchoredPosition = new Vector2(menuX, -top);
            rowMenu.gameObject.SetActive(true);
            rowMenu.SetAsLastSibling();
        }

        void CloseMenu()
        {
            if (rowMenu != null) rowMenu.gameObject.SetActive(false);
        }

        void OpenDelete()
        {
            CloseMenu();
            if (modalDelete != null) { modalDelete.SetActive(true); modalDelete.transform.SetAsLastSibling(); }
        }

        void CloseDelete()
        {
            if (modalDelete != null) modalDelete.SetActive(false);
        }

        void ConfirmDelete()
        {
            if (menuTarget >= 0 && menuTarget < sessions.Count) sessions.RemoveAt(menuTarget);
            menuTarget = -1;
            CloseDelete();
            Build();
            Toast.Show("음성 대화가 삭제되었어요.");
        }

        void CopyFont(TMP_Text t)
        {
            if (fontSource == null || fontSource.font == null || t == null) return;
            t.font = fontSource.font;
            t.fontSharedMaterial = fontSource.fontSharedMaterial;
        }
    }
}
