using UnityEngine;

namespace ARZone
{
    // 소환되는 캐릭터의 공통 조작면.
    // 스틱맨(임시) ↔ VRM 아바타(실물) 을 구현체 교체만으로 바꾼다.
    public interface IAvatarController
    {
        Transform Transform { get; }
        void WalkTo(Vector3 worldTarget);
    }
}