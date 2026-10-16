using System;
using Cysharp.Threading.Tasks;
using O2un.Core.Events;
using O2un.MVVM;
using R3;
using UnityEngine;

namespace O2un.UI
{
    /// <summary>
    /// 영상 한 편의 재생 상태. UI 이벤트(<see cref="PlayVideo"/>·<see cref="StopVideo"/>·<see cref="MuteVideo"/>)로 조작하고,
    /// 출력 RenderTexture 를 만들고 해제한다. 실제 재생은 View 의 VideoPlayer 가 한다.
    /// </summary>
    public sealed class VideoPlayerVM : ViewModelBase
    {
        private static readonly Vector2Int DEFAULT_SIZE = new Vector2Int(1920, 1080);

        private readonly IUIEventSubscriber _sub;

        public VideoPlayerVM(IUIEventSubscriber sub)
        {
            _sub = sub;
        }

        private readonly Subject<Unit> _playVideo = new();
        private readonly ReactiveProperty<VideoSource> _source = new();
        private readonly ReactiveProperty<RenderTexture> _videoTexture = new();
        private readonly ReactiveProperty<bool> _isMuted = new();
        public Observable<Unit> PlayVideo => _playVideo;
        public ReadOnlyReactiveProperty<VideoSource> Source => _source;
        public ReadOnlyReactiveProperty<RenderTexture> VideoTexture => _videoTexture;
        public ReadOnlyReactiveProperty<bool> IsMuted => _isMuted;
        public PlayVideo.VideoFinishEvent VideoStopEvent { get; private set; }
        public Action VideoErrorEvent { get; private set; }

        public override async UniTask InitAsync()
        {
            await base.InitAsync();

            _sub.Observe<PlayVideo>().Subscribe(Start).AddTo(DisposableR3);
            _sub.Observe<StopVideo>().Subscribe(Stop).AddTo(DisposableR3);
            _sub.Observe<MuteVideo>().Subscribe(video => _isMuted.Value = video.IsMuted).AddTo(DisposableR3);
        }

        protected override void SafeDispose()
        {
            CleanUpTexture();

            _videoTexture.Dispose();
            _source.Dispose();
            _isMuted.Dispose();
            _playVideo.Dispose();

            base.SafeDispose();
        }

        public void CleanUpTexture()
        {
            if (null == _videoTexture.Value)
            {
                return;
            }

            RenderTexture texture = _videoTexture.Value;
            _videoTexture.Value = null;
            texture.Release();
            UnityEngine.Object.Destroy(texture);
        }

        private void Start(PlayVideo video)
        {
            if (false == video.Source.IsEmpty)
            {
                _source.Value = video.Source;
            }

            if (true == _source.Value.IsEmpty)
            {
                return;
            }

            PrepareTexture(_source.Value.SizeOr(DEFAULT_SIZE));
            VideoStopEvent = video.VideoStopEvent;
            VideoErrorEvent = video.VideoErrorEvent;

            if (false == IsVisible.CurrentValue)
            {
                SetVisible(true);
            }
            else
            {
                _playVideo.OnNext(Unit.Default);
            }
        }

        private void Stop(StopVideo video)
        {
            SetVisible(false);
        }

        private void PrepareTexture(Vector2Int size)
        {
            RenderTexture current = _videoTexture.Value;
            if (null != current && current.width == size.x && current.height == size.y)
            {
                return;
            }

            CleanUpTexture();
            _videoTexture.Value = new RenderTexture(new RenderTextureDescriptor(size.x, size.y)
            {
                colorFormat = RenderTextureFormat.DefaultHDR,
                depthBufferBits = 0,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
            })
            {
                name = "VideoTexture",
            };
        }
    }
}
