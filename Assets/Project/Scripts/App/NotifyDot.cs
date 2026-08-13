using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 홈 상단 버튼(알림 종 / 미션)의 '새 것 있음' 점.
    // 디자이너 에셋에 점이 그려져 있어서 별도 오브젝트가 아니라
    // on/off 스프라이트 교체로 표현한다.
    //
    // 홈 화면이 꺼져 있어도 스프라이트만 바꿔두면 되므로,
    // 알림/미션 컨트롤러가 화면을 나갈 때 Set() 을 호출해준다.
    [RequireComponent(typeof(Image))]
    public class NotifyDot : MonoBehaviour
    {
        [SerializeField] Sprite on;    // 점 있는 버튼 (btn_alarm_on / btn_mission_on)
        [SerializeField] Sprite off;   // 점 없는 버튼 (btn_alarm_off / btn_mission_off)

        Image img;

        void Awake() => img = GetComponent<Image>();

        public void Set(bool hasNew)
        {
            if (img == null) img = GetComponent<Image>();
            var s = hasNew ? on : off;
            if (img != null && s != null) img.sprite = s;
        }
    }
}
