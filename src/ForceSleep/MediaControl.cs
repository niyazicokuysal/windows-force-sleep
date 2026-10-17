using Windows.Media.Control;

namespace ForceSleep;

/// <summary>
/// Talks to whatever is playing through Windows' media controls (the same ones the volume
/// flyout shows), so it works for browsers, Spotify, the Media Player and so on.
/// </summary>
internal static class MediaControl
{
    public static async Task<bool> IsAnythingPlayingAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            return manager.GetSessions().Any(IsPlaying);
        }
        catch (Exception e)
        {
            Log.Write($"Could not query media sessions: {e.Message}");
            return false;
        }
    }

    /// <summary>Pauses everything that is playing. Paused players are left alone, so nothing starts.</summary>
    public static async Task PauseAllAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            foreach (var session in manager.GetSessions().Where(IsPlaying))
            {
                await session.TryPauseAsync();
                Log.Write($"Paused media: {session.SourceAppUserModelId}");
            }
        }
        catch (Exception e)
        {
            Log.Write($"Could not pause media: {e.Message}");
        }
    }

    private static bool IsPlaying(GlobalSystemMediaTransportControlsSession session) =>
        session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
}
