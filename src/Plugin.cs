using Rainmeter;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;
using AutoRepeatMode = Windows.Media.MediaPlaybackAutoRepeatMode;
using MediaProperties = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties;
using PlaybackInfo = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackInfo;
using PlaybackStatus = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;
using SessionManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;
using TimelineProperties = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionTimelineProperties;

namespace Plugin
{
    enum ReturnType
    {
        ARTIST,
        ALBUM,
        TITLE,
        GENRE,
        COVER,
        NUMBER,
        DURATION,
        POSITION,
        PROGRESS,
        REPEAT,
        SHUFFLE,
        STATE,
        STATUS,
        // legacy support
        RATING
    }
    enum ActionType
    {
        PAUSE,
        PLAY,
        PLAYPAUSE,
        STOP,
        NEXT,
        PREVIOUS,
        SETPOSITION,
        SETSHUFFLE,
        SETREPEAT,
        // legacy support
        SETVOLUME,
        SETRATING,
        OPENPLAYER,
        CLOSEPLAYER,
        TOGGLEPLAYER
    }
    static class MediaPlayer
    {
        static SessionManager manager = null;
        static Session currentSession = null;
        static MediaProperties mediaProperties = null;
        static PlaybackInfo playbackInfo = null;
        static TimelineProperties timelineProperties = null;
        private static bool _started = false;
        public static bool Started => _started;
        private static string _err = string.Empty;
        public static string Error => _err;
        static async void CurrentSession_MediaPropertiesChanged(Session session, MediaPropertiesChangedEventArgs e = null)
        {
            try
            {
                mediaProperties = await session.TryGetMediaPropertiesAsync();
            }
            catch (Exception err)
            {
                mediaProperties = null;
                _err = err.Message;
            }
        }
        static void CurrentSession_PlaybackInfoChanged(Session session, PlaybackInfoChangedEventArgs e = null)
        {
            try
            {
                playbackInfo = session.GetPlaybackInfo();
            }
            catch (Exception err)
            {
                playbackInfo = null;
                _err = err.Message;
            }
        }
        static void CurrentSession_TimelinePropertiesChanged(Session session, TimelinePropertiesChangedEventArgs e = null)
        {
            try
            {
                timelineProperties = session.GetTimelineProperties();
            }
            catch (Exception err)
            {
                timelineProperties = null;
                _err = err.Message;
            }
        }
        static void CurrentSession_Changed(SessionManager sessionManager, CurrentSessionChangedEventArgs e = null)
        {
            try
            {
                if (null != currentSession)
                {
                    currentSession.MediaPropertiesChanged -= CurrentSession_MediaPropertiesChanged;
                    currentSession.PlaybackInfoChanged -= CurrentSession_PlaybackInfoChanged;
                    currentSession.TimelinePropertiesChanged -= CurrentSession_TimelinePropertiesChanged;
                }
                currentSession = sessionManager.GetCurrentSession();
                if (null != currentSession)
                {
                    CurrentSession_MediaPropertiesChanged(currentSession);
                    CurrentSession_PlaybackInfoChanged(currentSession);
                    CurrentSession_TimelinePropertiesChanged(currentSession);
                    currentSession.MediaPropertiesChanged += CurrentSession_MediaPropertiesChanged;
                    currentSession.PlaybackInfoChanged += CurrentSession_PlaybackInfoChanged;
                    currentSession.TimelinePropertiesChanged += CurrentSession_TimelinePropertiesChanged;
                }
            }
            catch (Exception err)
            {
                currentSession = null;
                _err = err.Message;
            }
        }
        public static async void Start()
        {
            if (!_started)
            {
                try
                {
                    manager = await SessionManager.RequestAsync();
                    CurrentSession_Changed(manager);
                    manager.CurrentSessionChanged += CurrentSession_Changed;
                    _started = true;
                }
                catch (Exception err)
                {
                    _started = false;
                    _err = err.Message;
                }
            }
        }
    }
    internal class Measure
    {
        static public implicit operator Measure(IntPtr data)
        {
            return (Measure)GCHandle.FromIntPtr(data).Target;
        }
        public API api = null;
    }
    public class Plugin
    {
        [DllExport]
        public static void Initialize(ref IntPtr data, IntPtr _1)
        {
            data = GCHandle.ToIntPtr(GCHandle.Alloc(new Measure()));
            if (!MediaPlayer.Started)
                 MediaPlayer.Start();
        }
        [DllExport]
        public static void Reload(IntPtr data, IntPtr rm, ref double _1)
        {
            Measure measure = (Measure)data;
            measure.api = (API)rm;
        }
        [DllExport]
        public static double Update(IntPtr data)
        {
            Measure measure = (Measure)data;
            if (!string.IsNullOrEmpty(MediaPlayer.Error))
                measure.api.LogF(API.LogType.Error, "MediaPlayer: {0}", MediaPlayer.Error);
            return 0.0;
        }
        [DllExport]
        public static void Finalize(IntPtr data)
        {
            Measure measure = (Measure)data;
            GCHandle.FromIntPtr(data).Free();
        }
    }
}