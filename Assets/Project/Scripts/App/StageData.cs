using UnityEngine;
using UnityEngine.Video;

namespace ARZone
{
    // 한 무대가 여러 탭에 걸릴 수 있어 비트 플래그로 둔다 (예: 만트라 = MV + 콘서트).
    // ⚠ 직렬화 값이 바뀌었다: 예전 0(MV)=1, 1(콘서트)=2. 씬 데이터도 같이 옮겼다.
    [System.Flags]
    public enum StageCategory { None = 0, MusicVideo = 1, Concert = 2 }

    // 스테이지(영상) 한 개의 데이터. 나중에 서버에서 받아올 형태.
    [System.Serializable]
    public class StageData
    {
        public string title = "SOLO";                    // "SOLO"
        public string sub = "JENNIE Special MV Stage";   // 부제
        public string date = "2026. 01. 10";             // 표시용 날짜
        public StageCategory category = StageCategory.MusicVideo;
        public Sprite thumb;                             // 썸네일 (없으면 회색 유지)
        public VideoClip clip;                           // 재생할 영상
        public bool featured;                            // 목록 상단 큰 카드로 쓸 것인가
    }
}