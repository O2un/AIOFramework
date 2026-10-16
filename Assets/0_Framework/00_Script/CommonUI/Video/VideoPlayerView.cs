using O2un.Core;
using O2un.Core.Utils;
using O2un.MVVM;
using R3;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace O2un.UI
{
    public sealed class VideoPlayerView : ViewBase<VideoPlayerVM>
    {
        private const ushort AUDIO_TRACK = 0;

        [SerializeField] private VideoPlayer _videoPlayer;
        [SerializeField] private RawImage _raw;

        private float _videoStartTime;
        private bool _isPlaying;

        protected override void BindModel()
        {
            // Prepare 를 거는 구독보다 먼저 붙인다 — BindModel 은 Init 완료를 기다리지 않으므로
            // 순서를 뒤집으면 첫 Prepare 의 콜백을 받을 사람이 없다.
            this.Track<VideoPlayer, VideoPlayer.EventHandler>(_videoPlayer, PlayVideo,
                static (player, handler) => player.prepareCompleted += handler,
                static (player, handler) => player.prepareCompleted -= handler);
            this.Track<VideoPlayer, VideoPlayer.EventHandler>(_videoPlayer, _ => FinishVideo(),
                static (player, handler) => player.loopPointReached += handler,
                static (player, handler) => player.loopPointReached -= handler);
            this.Track<VideoPlayer, VideoPlayer.ErrorEventHandler>(_videoPlayer, OnVideoError,
                static (player, handler) => player.errorReceived += handler,
                static (player, handler) => player.errorReceived -= handler);

            Model.VideoTexture.Subscribe(texture =>
            {
                _videoPlayer.targetTexture = texture;
                _raw.texture = texture;
            }).AddTo(DisposableR3);
            Model.Source.Subscribe(ApplySource).AddTo(DisposableR3);
            Model.IsMuted.Subscribe(ApplyMute).AddTo(DisposableR3);
            Model.PlayVideo.Subscribe(_ => RequestPlay()).AddTo(DisposableR3);

            OnTurnOnAfter.Subscribe(_ => RequestPlay()).AddTo(DisposableR3);
            OnTurnOffAfter.Subscribe(_ =>
            {
                _videoPlayer.Stop();
                _videoPlayer.targetTexture = null;
                _raw.texture = null;
                Model.CleanUpTexture();
                FinishVideo();
            }).AddTo(DisposableR3);
        }

        // 재생이 끝난 플레이어는 Stop() 을 거치지 않으면 isPrepared 가 true 로 남고, 그 상태의 Prepare() 는
        // prepareCompleted 를 다시 쏘지 않는다. Play() 가 그 콜백 안에만 있으므로 여기서 갈라주지 않으면
        // 같은 소스를 다시 틀 때 첫/끝 프레임에 멈춘 채 영구히 재생되지 않는다.
        private void RequestPlay()
        {
            if (false == _videoPlayer.isPrepared)
            {
                _videoPlayer.Prepare();
                return;
            }

            _videoPlayer.time = 0;
            PlayVideo(_videoPlayer);
        }

        private void PlayVideo(VideoPlayer source)
        {
            _videoStartTime = Time.time;
            _isPlaying = true;
            ApplyMute(Model.IsMuted.CurrentValue);
            source.Play();
        }

        // 한 번도 재생되지 않은 영상(준비 중 실패·닫힘)에 이전 영상의 시작 시각으로 재생 기록이 나가지 않게 한다.
        private void FinishVideo()
        {
            if (false == _isPlaying)
            {
                return;
            }

            _isPlaying = false;
            Model.VideoStopEvent?.Invoke(_videoStartTime);
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            Log.Print(Log.LogLevel.Error, $"VideoPlayerView: {SourceName(source)} 재생 실패 — {message}");

            source.Stop();
            _isPlaying = false;
            Model.VideoErrorEvent?.Invoke();
        }

        private void ApplySource(VideoSource source)
        {
            if (null != source.Clip)
            {
                _videoPlayer.source = UnityEngine.Video.VideoSource.VideoClip;
                _videoPlayer.clip = source.Clip;
                return;
            }

            _videoPlayer.source = UnityEngine.Video.VideoSource.Url;
            _videoPlayer.url = source.Url;
        }

        private void ApplyMute(bool isMuted)
        {
            _videoPlayer.SetDirectAudioMute(AUDIO_TRACK, isMuted);
        }

        private static string SourceName(VideoPlayer source)
        {
            return null != source.clip ? source.clip.name : source.url;
        }
    }
}
