using O2un.Core.Events;
using O2un.MVVM;
using UnityEngine;
using VContainer;

namespace O2un.UI
{
    [RequireComponent(typeof(VideoPlayerView))]
    public sealed partial class VideoPlayerContext : ContextBase<VideoPlayerView, VideoPlayerVM>
    {
        [Inject] private IUIEventSubscriber _subscriber;

        protected override VideoPlayerVM CreateModel()
        {
            return new(_subscriber);
        }
    }
}
