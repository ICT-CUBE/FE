using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ARZone
{
    // 홈 말풍선 인삿말을 여러 개 중에서 돌려가며 보여준다.
    // 서버 없이 클라이언트에서만 처리. 화면에 들어올 때마다 새로 고른다.
    public class GreetingRotator : MonoBehaviour
    {
        [SerializeField] TMP_Text target;

        [Tooltip("{name} 자리에 사용자 닉네임이 들어간다. 줄바꿈은 \\n 으로 입력")]
        [SerializeField] List<string> greetings = new List<string>
        {
            "오늘도 나 보러 왔어?\n뭐 하고 있었어?",
            "{name}, 기다리고 있었어!",
            "오늘 하루는 어땠어?",
            "보고 싶었어.\n잘 지냈어?",
            "{name} 오늘도 같이 놀자!",
        };

        [SerializeField] string userName = "친구";

        int lastIndex = -1;

        void Reset() => target = GetComponent<TMP_Text>();

        void OnEnable() => ShowNext();

        // 닉네임 설정/로그인 후 호출해서 이름을 넣어준다.
        public void SetUserName(string name)
        {
            if (!string.IsNullOrWhiteSpace(name)) userName = name;
        }

        // 직전에 나온 것과 겹치지 않게 다음 인삿말을 고른다.
        public void ShowNext()
        {
            if (target == null || greetings.Count == 0) return;

            int index = greetings.Count == 1 ? 0 : PickDifferent(lastIndex);
            lastIndex = index;
            target.text = greetings[index].Replace("{name}", userName);
        }

        int PickDifferent(int avoid)
        {
            int i;
            do { i = Random.Range(0, greetings.Count); } while (i == avoid);
            return i;
        }
    }
}