using System;
using UnityEngine;
using UnityEngine.Video;

namespace O2un.UI
{
    public readonly struct VideoSource : IEquatable<VideoSource>
    {
        private VideoSource(string url, VideoClip clip)
        {
            Url = url;
            Clip = clip;
        }

        public string Url { get; }
        public VideoClip Clip { get; }
        public bool IsEmpty => null == Clip && true == string.IsNullOrWhiteSpace(Url);

        public static VideoSource FromUrl(string url)
        {
            return new VideoSource(url, null);
        }

        public static VideoSource FromClip(VideoClip clip)
        {
            return new VideoSource(null, clip);
        }

        public Vector2Int SizeOr(Vector2Int fallback)
        {
            if (null == Clip)
            {
                return fallback;
            }

            return new Vector2Int((int)Clip.width, (int)Clip.height);
        }

        public bool Equals(VideoSource other)
        {
            return Url == other.Url && Clip == other.Clip;
        }

        public override bool Equals(object obj)
        {
            return obj is VideoSource other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Url, Clip);
        }
    }
}
