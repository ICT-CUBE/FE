using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 친밀도 진행바 + 텍스트를 값에 맞춰 갱신.
    public class IntimacyBar : MonoBehaviour
    {
        [SerializeField] Image fill;             // ProgressFill 드래그
        [SerializeField] TMP_Text levelText;     // LevelText 드래그
        [SerializeField] TMP_Text progressText;  // ProgressText 드래그
        [SerializeField] float fillSpeed = 2f;   // 채워지는 속도

        float target;

        // 데이터가 들어오기 전엔 씬에 잡아둔 값을 유지한다(0 으로 빨려들어가지 않게)
        void Start()
        {
            if (fill != null) target = fill.fillAmount;
        }

        // 데이터 들어오면 이거 호출: SetData(3, "단짝", 1245, 2000)
        public void SetData(int level, string title, int current, int max)
        {
            target = max > 0 ? (float)current / max : 0f;
            if (levelText != null) levelText.text = $"Lv.{level} {title}";
            if (progressText != null) progressText.text = $"Lv.{level + 1} 까지 {max - current} 남았어요";
        }

        void Update()
        {
            if (fill == null) return;
            // 목표치까지 부드럽게 차오름
            fill.fillAmount = Mathf.MoveTowards(fill.fillAmount, target, fillSpeed * Time.deltaTime);
        }
    }
}