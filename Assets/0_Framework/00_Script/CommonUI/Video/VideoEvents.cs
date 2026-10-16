using System;
using O2un.Core.Events;
using UnityEngine.Video;

namespace O2un.UI
{
    public readonly struct PlayVideo : IUIEvent
    {
        public delegate void VideoFinishEvent(float startTime);

        public PlayVideo(string url, VideoFinishEvent videoStopEvent = null, Action videoErrorEvent = null)
        {
            Source = VideoSource.FromUrl(url);
            VideoStopEvent = videoStopEvent;
            VideoErrorEvent = videoErrorEvent;
        }

        public PlayVideo(VideoClip clip, VideoFinishEvent videoStopEvent = null, Action videoErrorEvent = null)
        {
            Source = VideoSource.FromClip(clip);
            VideoStopEvent = videoStopEvent;
            VideoErrorEvent = videoErrorEvent;
        }

        public VideoSource Source { get; }
        public VideoFinishEvent VideoStopEvent { get; }
        public Action VideoErrorEvent { get; }
    }

    public readonly struct StopVideo : IUIEvent
    {
    }

    public readonly struct MuteVideo : IUIEvent
    {
        public MuteVideo(bool isMuted)
        {
            IsMuted = isMuted;
        }

        public bool IsMuted { get; }
    }
}
