using UnityEngine;
using TMPro;

namespace ARZone
{
    // 가로 모드 무대 재생 화면(Screen_StagePlayer)의 상단 정보.
    // 세로 상세는 StageDetailController 가 채우지만 가로 화면엔 컨트롤러가 없어
    // 제목이 씬에 박힌 값("SOLO")으로 남아 있었다.
    public class StagePlayerInfo : MonoBehaviour
    {
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text sub;

        void OnEnable()
        {
            var cat = StageCatalog.Instance;
            var d = cat != null ? cat.Selected : null;
            if (d == null)
            {
                Debug.LogWarning("[StagePlayer] 선택된 스테이지가 없어 제목을 못 채움");
                return;
            }
            if (title != null) title.text = d.title;
            if (sub != null) sub.text = d.sub;
        }
    }
}
