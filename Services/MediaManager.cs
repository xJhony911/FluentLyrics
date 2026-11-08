using System;
using System.Threading.Tasks;
using Windows.Media.Control;
using FluentLyrics.Models;

namespace FluentLyrics.Services
{
    public class MediaManager : IDisposable
    {
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;

        public event Action<TrackInfo>? TrackChanged;
        public event Action<bool>? PlaybackStateChanged;

        private TrackInfo _currentTrack = new();
        private readonly object _lock = new();

        public async Task InitializeAsync()
        {
            try
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_sessionManager != null)
                {
                    _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
                    HookSession(_sessionManager.GetCurrentSession());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to init GSMTC: {ex.Message}");
            }
        }

        private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            HookSession(sender.GetCurrentSession());
        }

        private void HookSession(GlobalSystemMediaTransportControlsSession? session)
        {
            lock (_lock)
            {
                if (_currentSession != null)
                {
                    _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                    _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                    _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
                }

                _currentSession = session;

                if (_currentSession != null)
                {
                    _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                    _currentSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
                }
            }

            _ = UpdateCurrentTrackAsync();
        }

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            _ = UpdateCurrentTrackAsync();
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            _ = UpdatePlaybackInfoAsync();
        }

        private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        {
            _ = UpdateTimelineAsync();
        }

        public async Task UpdateCurrentTrackAsync()
        {
            if (_currentSession == null)
            {
                lock (_lock)
                {
                    _currentTrack = new TrackInfo();
                }
                TrackChanged?.Invoke(_currentTrack);
                return;
            }

            try
            {
                var media = await _currentSession.TryGetMediaPropertiesAsync();
                var playback = _currentSession.GetPlaybackInfo();
                var timeline = _currentSession.GetTimelineProperties();

                var track = new TrackInfo
                {
                    Title = media?.Title ?? string.Empty,
                    Artist = media?.Artist ?? string.Empty,
                    AlbumTitle = media?.AlbumTitle ?? string.Empty,
                    IsPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    Position = timeline?.Position ?? TimeSpan.Zero,
                    Duration = timeline?.EndTime ?? TimeSpan.Zero,
                    LastUpdated = DateTime.UtcNow
                };

                bool changed = false;
                lock (_lock)
                {
                    if (!_currentTrack.Equals(track) || _currentTrack.IsPlaying != track.IsPlaying)
                    {
                        changed = true;
                    }
                    _currentTrack = track;
                }

                if (changed)
                {
                    TrackChanged?.Invoke(track);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateCurrentTrack error: {ex.Message}");
            }
        }

        private Task UpdatePlaybackInfoAsync()
        {
            if (_currentSession == null) return Task.CompletedTask;

            try
            {
                var playback = _currentSession.GetPlaybackInfo();
                bool isPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                lock (_lock)
                {
                    _currentTrack.IsPlaying = isPlaying;
                }

                PlaybackStateChanged?.Invoke(isPlaying);
            }
            catch { }

            return Task.CompletedTask;
        }

        private Task UpdateTimelineAsync()
        {
            if (_currentSession == null) return Task.CompletedTask;

            try
            {
                var timeline = _currentSession.GetTimelineProperties();
                if (timeline != null)
                {
                    lock (_lock)
                    {
                        _currentTrack.Position = timeline.Position;
                        _currentTrack.Duration = timeline.EndTime;
                        _currentTrack.LastUpdated = DateTime.UtcNow;
                    }
                }
            }
            catch { }

            return Task.CompletedTask;
        }

        public TimeSpan GetCurrentPosition(int offsetMs = 0)
        {
            lock (_lock)
            {
                if (!_currentTrack.IsPlaying)
                {
                    return _currentTrack.Position + TimeSpan.FromMilliseconds(offsetMs);
                }

                var elapsed = DateTime.UtcNow - _currentTrack.LastUpdated;
                var estimated = _currentTrack.Position + elapsed + TimeSpan.FromMilliseconds(offsetMs);
                if (estimated < TimeSpan.Zero) return TimeSpan.Zero;
                return estimated;
            }
        }

        public TrackInfo GetCurrentTrack()
        {
            lock (_lock)
            {
                return _currentTrack;
            }
        }

        public void Dispose()
        {
            if (_sessionManager != null)
            {
                _sessionManager.CurrentSessionChanged -= OnCurrentSessionChanged;
            }
            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            }
        }
    }
}
