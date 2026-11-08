using System;

namespace FluentLyrics.Models
{
    public class LyricLine
    {
        public TimeSpan Timestamp { get; set; }
        public string Text { get; set; } = string.Empty;

        public LyricLine() { }

        public LyricLine(TimeSpan timestamp, string text)
        {
            Timestamp = timestamp;
            Text = text;
        }

        public override string ToString() => $"[{Timestamp:mm\\:ss\\.ff}] {Text}";
    }
}
