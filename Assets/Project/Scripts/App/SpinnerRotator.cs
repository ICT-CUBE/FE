using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 원형 로딩 스피너. 열린 링 이미지 한 장을 계속 돌린다.
    // 시안 '로딩창.png' 실측: 링 바깥지름 56.7, 두께 4.67, 중심 (206,436).
    //
    // 프레임 시퀀스(ScanIndicatorAnimator)와 달리 이미지 1장이면 되고 텍스처도 한 장만 쓴다.
    [RequireComponent(typeof(Image))]
    public class SpinnerRotator : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = -360f;   // 음수 = 시계 방향
        RectTransform rt;

        void Awake() => rt = (RectTransform)transform;

        void OnEnable()
        {
            if (rt != null) rt.localRotation = Quaternion.identity;
        }

        void Update()
        {
            if (rt == null) return;
            rt.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
        }
    }
}
