using System;
using UnityEngine;
using UnityEngine.Video;

namespace ARZone
{
    // ARRig 안의 VideoPlayer 를 감싼다. 리그는 런타임에 생기므로 Bind 로 나중에 물린다.
    public class ARStagePlayer : MonoBehaviour
    {
        public event Action Finished;

        VideoPlayer player;

        public bool HasVideo => player != null;
        public bool IsPlaying => player != null && player.isPlaying;
        public double Time => player != null ? player.time : 0d;

        public void Bind(VideoPlayer vp)
        {
            Unbind();
            player = vp;
            if (player != null) player.loopPointReached += OnLoopPoint;
        }

        public void Unbind()
        {
            if (player != null) player.loopPointReached -= OnLoopPoint;
            player = null;
        }

        public void Play()
        {
            if (player == null) return;
            player.Play();
        }

        public void Pause()
        {
            if (player == null) return;
            player.Pause();
        }

        public void TogglePlay()
        {
            if (player == null) return;
            if (player.isPlaying) player.Pause();
            else player.Play();
        }

        public void Restart()
        {
            if (player == null) return;
            player.time = 0d;
            player.Play();
        }

        public void Stop()
        {
            if (player == null) return;
            player.Stop();
        }

        void OnLoopPoint(VideoPlayer _) => Finished?.Invoke();

        void OnDestroy() => Unbind();
    }
}