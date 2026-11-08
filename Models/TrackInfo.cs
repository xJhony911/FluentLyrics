using System;

namespace FluentLyrics.Models
{
    public class TrackInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string AlbumTitle { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;
        public bool IsPlaying { get; set; }
        public TimeSpan Position { get; set; } = TimeSpan.Zero;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public bool IsEmpty => string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(Artist);

        public override bool Equals(object? obj)
        {
            if (obj is TrackInfo other)
            {
                return string.Equals(Title, other.Title, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(Artist, other.Artist, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                Title?.ToLowerInvariant(),
                Artist?.ToLowerInvariant()
            );
        }
    }
}
