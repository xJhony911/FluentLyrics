using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentLyrics.Models;

namespace FluentLyrics.Services
{
    public class LyricsService
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FluentLyrics",
            "Cache"
        );

        private static readonly Regex LrcRegex = new(
            @"^\[(?<min>\d{1,2}):(?<sec>\d{1,2}(?:\.\d{1,3})?)\](?<text>.*)$",
            RegexOptions.Compiled
        );

        static LyricsService()
        {
            if (!HttpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                HttpClient.DefaultRequestHeaders.Add("User-Agent", "FluentLyrics/1.0 (Windows NT 10.0; Win64; x64)");
            }
        }

        public async Task<List<LyricLine>> GetLyricsAsync(TrackInfo track, CancellationToken ct = default)
        {
            if (track.IsEmpty) return new List<LyricLine>();

            // Clean strings
            string cleanTitle = CleanTrackTitle(track.Title);
            string cleanArtist = track.Artist.Trim();

            // 1. Check local cache first
            var cached = LoadFromCache(cleanArtist, cleanTitle);
            if (cached != null && cached.Count > 0)
            {
                return cached;
            }

            try
            {
                // 2. Fetch from LRCLIB (exact match)
                int durationSeconds = (int)Math.Round(track.Duration.TotalSeconds);
                string url = $"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(cleanArtist)}&track_name={Uri.EscapeDataString(cleanTitle)}";
                if (durationSeconds > 0)
                {
                    url += $"&duration={durationSeconds}";
                }
                if (!string.IsNullOrWhiteSpace(track.AlbumTitle))
                {
                    url += $"&album_name={Uri.EscapeDataString(track.AlbumTitle)}";
                }

                using var response = await HttpClient.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    var lines = ParseLrclibJson(json);
                    if (lines.Count > 0)
                    {
                        SaveToCache(cleanArtist, cleanTitle, lines);
                        return lines;
                    }
                }

                // 3. Fallback: Search endpoint
                string searchUrl = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(cleanArtist + " " + cleanTitle)}";
                using var searchResponse = await HttpClient.GetAsync(searchUrl, ct);
                if (searchResponse.IsSuccessStatusCode)
                {
                    var searchJson = await searchResponse.Content.ReadAsStringAsync(ct);
                    var lines = ParseLrclibSearchJson(searchJson, durationSeconds);
                    if (lines.Count > 0)
                    {
                        SaveToCache(cleanArtist, cleanTitle, lines);
                        return lines;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching lyrics: {ex.Message}");
            }

            return new List<LyricLine>();
        }

        private static List<LyricLine> ParseLrclibJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("syncedLyrics", out var syncedElement) &&
                    syncedElement.ValueKind == JsonValueKind.String)
                {
                    var syncedText = syncedElement.GetString();
                    if (!string.IsNullOrWhiteSpace(syncedText))
                    {
                        return ParseLrc(syncedText);
                    }
                }

                if (root.TryGetProperty("plainLyrics", out var plainElement) &&
                    plainElement.ValueKind == JsonValueKind.String)
                {
                    var plainText = plainElement.GetString();
                    if (!string.IsNullOrWhiteSpace(plainText))
                    {
                        // Return as a single placeholder line
                        return new List<LyricLine> { new(TimeSpan.Zero, "♫ " + plainText.Split('\n').FirstOrDefault()?.Trim() + " ♫") };
                    }
                }
            }
            catch { }

            return new List<LyricLine>();
        }

        private static List<LyricLine> ParseLrclibSearchJson(string json, int targetDuration)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    JsonElement? bestItem = null;
                    int minDiff = int.MaxValue;

                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("syncedLyrics", out var syn) &&
                            syn.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(syn.GetString()))
                        {
                            if (targetDuration > 0 && item.TryGetProperty("duration", out var dur))
                            {
                                int itemDur = dur.GetInt32();
                                int diff = Math.Abs(itemDur - targetDuration);
                                if (diff < minDiff)
                                {
                                    minDiff = diff;
                                    bestItem = item;
                                }
                            }
                            else
                            {
                                bestItem = item;
                                break;
                            }
                        }
                    }

                    if (bestItem.HasValue &&
                        bestItem.Value.TryGetProperty("syncedLyrics", out var syncedVal))
                    {
                        var text = syncedVal.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return ParseLrc(text);
                        }
                    }
                }
            }
            catch { }

            return new List<LyricLine>();
        }

        public static List<LyricLine> ParseLrc(string lrcContent)
        {
            var result = new List<LyricLine>();
            if (string.IsNullOrWhiteSpace(lrcContent)) return result;

            var rawLines = lrcContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var raw in rawLines)
            {
                var match = LrcRegex.Match(raw.Trim());
                if (match.Success)
                {
                    if (int.TryParse(match.Groups["min"].Value, out int min) &&
                        double.TryParse(match.Groups["sec"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double sec))
                    {
                        var ts = TimeSpan.FromMinutes(min) + TimeSpan.FromSeconds(sec);
                        var text = match.Groups["text"].Value.Trim();
                        result.Add(new LyricLine(ts, text));
                    }
                }
            }

            return result.OrderBy(l => l.Timestamp).ToList();
        }

        private static string CleanTrackTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;

            // Remove trailing tags like (Remastered 2021), [Official Audio], feat. etc.
            string cleaned = Regex.Replace(title, @"\s*[\(\[](remaster(ed)?|feat\.|ft\.|bonus|deluxe|official).*?[\)\]]", "", RegexOptions.IgnoreCase);
            return cleaned.Trim();
        }

        private static string GetCacheFilePath(string artist, string title)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            string safeName = $"{artist}_{title}";
            foreach (var c in invalidChars)
            {
                safeName = safeName.Replace(c, '_');
            }
            if (safeName.Length > 80) safeName = safeName.Substring(0, 80);
            return Path.Combine(CacheDir, safeName + ".json");
        }

        private static List<LyricLine>? LoadFromCache(string artist, string title)
        {
            try
            {
                string path = GetCacheFilePath(artist, title);
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<List<LyricLine>>(json);
                }
            }
            catch { }
            return null;
        }

        private static void SaveToCache(string artist, string title, List<LyricLine> lines)
        {
            try
            {
                if (!Directory.Exists(CacheDir))
                {
                    Directory.CreateDirectory(CacheDir);
                }

                string path = GetCacheFilePath(artist, title);
                var json = JsonSerializer.Serialize(lines);
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }
}
