using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 스테이지 목록 보관소 + 현재 선택된 항목.
    // 지금은 인스펙터에 넣은 임시 데이터. 백엔드 API 나오면 Load() 만 갈아끼우면 된다.
    public class StageCatalog : MonoBehaviour
    {
        static StageCatalog instance;

        // Awake 순서는 오브젝트 간에 보장되지 않는다.
        // 다른 컴포넌트가 OnEnable 에서 부르면 아직 null 일 수 있어, 없으면 직접 찾는다.
        public static StageCatalog Instance
        {
            get
            {
                if (instance == null)
                    instance = FindAnyObjectByType<StageCatalog>(FindObjectsInactive.Include);
                return instance;
            }
            private set => instance = value;
        }

        [SerializeField] List<StageData> stages = new List<StageData>();

        public IReadOnlyList<StageData> All => stages;
        public StageData Selected { get; private set; }

        void Awake() => Instance = this;

        public void Select(StageData s) => Selected = s;

        // 플래그라 한 무대가 여러 탭에 들어갈 수 있다
        public List<StageData> Filter(StageCategory c)
        {
            var r = new List<StageData>();
            foreach (var s in stages) if ((s.category & c) != 0) r.Add(s);
            return r;
        }

        // 같은 카테고리의 다른 항목들 (상세의 '추천 동영상')
        public List<StageData> Recommend(StageData except, int max = 4)
        {
            var r = new List<StageData>();
            foreach (var s in stages)
            {
                if (s == except) continue;
                r.Add(s);
                if (r.Count >= max) break;
            }
            return r;
        }
    }
}