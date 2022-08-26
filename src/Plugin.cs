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
		STATE
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
	static class MediaController
	{
		private static bool _Started = false;
		public static bool Started()
		{
			return _Started;
		}
		public static SessionManager sessionManager = null;
		public static Session session = null;
		public static MediaProperties mediaProperties = null;
		public static PlaybackInfo playbackInfo = null;
		public static TimelineProperties timelineProperties = null;
		public static string coverPath = String.Empty;
		public static bool coverAvailable = false;
        private static async Task<SessionManager> GetSessionManager()
            => await SessionManager.RequestAsync();
        private static async Task<MediaProperties> GetMediaProperties(Session session)
            => await session.TryGetMediaPropertiesAsync();
        private static async Task<IRandomAccessStreamWithContentType> GetThumbnail(IRandomAccessStreamReference stream)
            => await stream.OpenReadAsync();
        static async void SaveCover()
		{
			try
			{
				var stream = await GetThumbnail(mediaProperties.Thumbnail);
				var bytes = new byte[stream.Size];
				var reader = new DataReader(stream.GetInputStreamAt(0));
				await reader.LoadAsync((uint)stream.Size);
				reader.ReadBytes(bytes);
				MemoryStream mStream = new MemoryStream(bytes);
				Image cover = Bitmap.FromStream(mStream);
				mStream.Flush();
				cover.Save(coverPath, ImageFormat.Png);
				reader.DetachStream();
				stream.FlushAsync().GetAwaiter().GetResult();
				coverAvailable = true;
			}
			catch
			{
				coverAvailable = false;
			}
		}
		static async void CurrentSessionChanged(SessionManager sender, CurrentSessionChangedEventArgs e = null)
		{
			if (session != null)
			{
				session.MediaPropertiesChanged -= MediaPropertiesChanged;
				session.PlaybackInfoChanged -= PlaybackInfoChanged;
				session.TimelinePropertiesChanged -= TimelinePropertiesChanged;
			}
			Session temp = sessionManager.GetCurrentSession();
			if (temp != null)
			{
				session = temp;
				session.MediaPropertiesChanged += MediaPropertiesChanged;
				session.PlaybackInfoChanged += PlaybackInfoChanged;
				session.TimelinePropertiesChanged += TimelinePropertiesChanged;
				mediaProperties = await GetMediaProperties(session);
				playbackInfo = session.GetPlaybackInfo();
				timelineProperties = session.GetTimelineProperties();
				SaveCover();
			}
			else
			{
				session = null;
			}
		}
		static void MediaPropertiesChanged(Session sender, MediaPropertiesChangedEventArgs e)
		{
			mediaProperties = session.TryGetMediaPropertiesAsync().GetResults();
			SaveCover();
		}
		static void PlaybackInfoChanged(Session sender, PlaybackInfoChangedEventArgs e)
		{
			playbackInfo = session.GetPlaybackInfo();
		}
		static void TimelinePropertiesChanged(Session sender, TimelinePropertiesChangedEventArgs e)
		{
			timelineProperties = session.GetTimelineProperties();
		}
		public static string StrProps(ReturnType t)
		{
			try
			{
				if (mediaProperties != null)
				{
					switch (t)
					{
						case ReturnType.ALBUM:
							return mediaProperties.AlbumTitle;
						case ReturnType.ARTIST:
							return mediaProperties.Artist;
						case ReturnType.COVER:
							return coverAvailable ? coverPath : "";
						case ReturnType.GENRE:
							return String.Join(", ", mediaProperties.Genres);
						case ReturnType.TITLE:
							return mediaProperties.Title;
						case ReturnType.DURATION:
							try
							{
								var ts = timelineProperties.MaxSeekTime.Subtract(timelineProperties.MinSeekTime);
								return String.Format("{0:00}:{1:00}", (int)Math.Floor(ts.TotalMinutes), ts.Seconds);
							}
							catch
							{
								return "00:00";
							}
						case ReturnType.POSITION:
							try
							{
								var ts = timelineProperties.Position;
								return String.Format("{0:00}:{1:00}", (int)Math.Floor(ts.TotalMinutes), ts.Seconds);
							}
							catch
							{
								return "00:00";
							}
						default:
							break;
					}
				}
			}
			catch { }
			return t == ReturnType.DURATION || t == ReturnType.POSITION ? "00:00" : "";
		}
		public static double NumProps(ReturnType t)
		{
			try
			{
				if (t == ReturnType.NUMBER)
				{
					if (mediaProperties != null)
						return mediaProperties.TrackNumber;
				}
				else if (t >= ReturnType.DURATION && t <= ReturnType.PROGRESS)
				{
					if (timelineProperties != null)
					{
						switch (t)
						{
							case ReturnType.DURATION:
								return timelineProperties.MaxSeekTime.TotalSeconds - timelineProperties.MinSeekTime.TotalSeconds;
							case ReturnType.POSITION:
								return timelineProperties.Position.TotalSeconds;
							case ReturnType.PROGRESS:
								return
									timelineProperties.Position.TotalSeconds * 100 /
									(timelineProperties.MaxSeekTime.TotalSeconds - timelineProperties.MinSeekTime.TotalSeconds);
						}
					}
				}
				else if (t >= ReturnType.REPEAT)
				{
					if (playbackInfo != null)
					{
						switch (t)
						{
							case ReturnType.REPEAT:
								switch (playbackInfo.AutoRepeatMode.GetValueOrDefault(AutoRepeatMode.None))
								{
									case AutoRepeatMode.Track:
										return 1.0;
									case AutoRepeatMode.List:
										return 2.0;
									default:
										return 0.0;
								}
							case ReturnType.SHUFFLE:
								return playbackInfo.IsShuffleActive.GetValueOrDefault(false) ? 1.0 : 0.0;
							case ReturnType.STATE:
								switch (playbackInfo.PlaybackStatus)
								{
									case PlaybackStatus.Playing:
										return 1.0;
									case PlaybackStatus.Paused:
										return 2.0;
									default:
										return 0.0;
								}
						}
					}
				}
			}
			catch
			{
				return 0.0;
			}
			return 0.0;
		}
		public static async void DoAction(ActionType t, int value, bool absolute)
		{
			if (session != null)
			{
				switch (t)
				{
					case ActionType.PAUSE:
						await session.TryPauseAsync();
						break;
					case ActionType.PLAY:
						await session.TryPlayAsync();
						break;
					case ActionType.PLAYPAUSE:
						await session.TryTogglePlayPauseAsync();
						break;
					case ActionType.NEXT:
						await session.TrySkipNextAsync();
						break;
					case ActionType.PREVIOUS:
						await session.TrySkipPreviousAsync();
						break;
					case ActionType.STOP:
						await session.TryStopAsync();
						break;
					case ActionType.SETPOSITION:
						double position = (absolute ? 0 : NumProps(ReturnType.POSITION)) + (value / 100.0) * NumProps(ReturnType.DURATION);
						TimeSpan ts = timelineProperties.MinSeekTime.Add(TimeSpan.FromSeconds(position));
						await session.TryChangePlaybackPositionAsync(ts.Ticks);
						break;
					case ActionType.SETREPEAT:
						if (value < -1 || value > 2) { return; }
						AutoRepeatMode mode = AutoRepeatMode.None;
						switch (value)
						{
							case -1:
								AutoRepeatMode m = (AutoRepeatMode)(int)NumProps(ReturnType.REPEAT);
								switch (m)
								{
									case AutoRepeatMode.None:
										mode = AutoRepeatMode.Track;
										break;
									case AutoRepeatMode.Track:
										mode = AutoRepeatMode.List;
										break;
									case AutoRepeatMode.List:
										mode = AutoRepeatMode.None;
										break;
								}
								break;
							case 0:
								mode = AutoRepeatMode.None;
								break;
							case 1:
								mode = AutoRepeatMode.List;
								break;
							case 2:
								mode = AutoRepeatMode.List;
								break;
							default:
								break;
						}
						await session.TryChangeAutoRepeatModeAsync(mode);
						break;
					case ActionType.SETSHUFFLE:
						bool prevShuffle = NumProps(ReturnType.SHUFFLE) == 1.0;
						await session.TryChangeShuffleActiveAsync(value == -1 ? !prevShuffle : value != 0);
						break;
					default:
						break;
				}
			}
		}
        public static async void Start()
		{
			coverPath = API.GetSettingsFile().Replace("Rainmeter.data", "WindowsNowPlaying");
			Directory.CreateDirectory(coverPath);
			coverPath += "\\cover.png";
			sessionManager = await GetSessionManager();
			if (sessionManager != null)
			{
				sessionManager.CurrentSessionChanged += CurrentSessionChanged;
				session = sessionManager.GetCurrentSession();
				if (session != null)
				{
					CurrentSessionChanged(sessionManager);
				}
				_Started = true;
			}
		}
	}
	class Measure
	{
		static public implicit operator Measure(IntPtr data)
		{
			return (Measure)GCHandle.FromIntPtr(data).Target;
		}
		public IntPtr buffer = IntPtr.Zero;
		public API api;
		public ReturnType type = ReturnType.TITLE;
		public string defCoverPath;
	}
	public class Plugin
	{
		[DllExport]
		public static void Initialize(ref IntPtr data, IntPtr _1)
		{
			data = GCHandle.ToIntPtr(GCHandle.Alloc(new Measure()));
			if (!MediaController.Started())
				MediaController.Start();
		}
		[DllExport]
		public static void Finalize(IntPtr data)
		{
			Measure measure = (Measure)data;
			if (measure.buffer != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(measure.buffer);
			}
			GCHandle.FromIntPtr(data).Free();
		}
		[DllExport]
		public static void Reload(IntPtr data, IntPtr rm, ref double _1)
		{
			Measure measure = (Measure)data;
			API api = (API)rm;
			measure.api = api;
			string type = api.ReadString("PlayerType", "title");
			switch (type.ToLower())
			{
				case "artist":
					measure.type = ReturnType.ARTIST;
					break;
				case "album":
					measure.type = ReturnType.ALBUM;
					break;
				case "title":
					measure.type = ReturnType.TITLE;
					break;
				case "genre":
					measure.type = ReturnType.GENRE;
					break;
				case "cover":
					measure.type = ReturnType.COVER;
					measure.defCoverPath = api.ReadPath("DefaultCoverPath", "");
					break;
				case "number":
					measure.type = ReturnType.NUMBER;
					break;
				case "duration":
					measure.type = ReturnType.DURATION;
					break;
				case "position":
					measure.type = ReturnType.POSITION;
					break;
				case "progress":
					measure.type = ReturnType.PROGRESS;
					break;
				case "repeat":
					measure.type = ReturnType.REPEAT;
					break;
				case "shuffle":
					measure.type = ReturnType.SHUFFLE;
					break;
				case "state":
					measure.type = ReturnType.STATE;
					break;
				default:
					api.LogF(API.LogType.Error, "Invalid player type: {0}", type);
					measure.type = ReturnType.TITLE;
					break;
			}
		}
		[DllExport]
		public static double Update(IntPtr data)
		{
			Measure measure = (Measure)data;

			if (measure.type >= ReturnType.NUMBER)
			{
				double buff = MediaController.NumProps(measure.type);
				if (!Double.IsNaN(buff) || !Double.IsInfinity(buff))
				{
					return buff;
				}
			}
			return 0.0;
		}
		[DllExport]
		public static IntPtr GetString(IntPtr data)
		{
			Measure measure = (Measure)data;
			if (measure.buffer != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(measure.buffer);
				measure.buffer = IntPtr.Zero;
			}

			string buff = string.Empty;
			if (measure.type <= ReturnType.COVER || measure.type == ReturnType.DURATION || measure.type == ReturnType.POSITION)
			{
				buff = MediaController.StrProps(measure.type);
			}
			if (String.IsNullOrEmpty(buff) && measure.type == ReturnType.COVER)
				buff = measure.defCoverPath;
			measure.buffer = string.IsNullOrEmpty(buff) ? IntPtr.Zero : Marshal.StringToHGlobalUni(buff);

			return measure.buffer;
		}
		[DllExport]
		public static void ExecuteBang(IntPtr data, [MarshalAs(UnmanagedType.LPWStr)] String args)
		{
			Measure measure = (Measure)data;
			var argTokens = args.Split(' ');
			if (argTokens.Length <= 0)
			{ return; }

			ActionType t = ActionType.PAUSE;
			int value = 0;
			bool absolute = true;
			switch (argTokens[0].ToLower())
			{
				case "play":
					t = ActionType.PLAY;
					break;
				case "pause":
					t = ActionType.PAUSE;
					break;
				case "playpause":
					t = ActionType.PLAYPAUSE;
					break;
				case "next":
					t = ActionType.NEXT;
					break;
				case "previous":
					t = ActionType.PREVIOUS;
					break;
				case "stop":
					t = ActionType.STOP;
					break;
				case "setposition":
					t = ActionType.SETPOSITION;
					if (argTokens.Length < 2)
					{
						measure.api.LogF(API.LogType.Error, "Invalid command: {0}", args);
						return;
					}
					if (argTokens[1].StartsWith("+") || argTokens[1].StartsWith("-"))
					{
						absolute = false;
					}
					if (!int.TryParse(argTokens[1], out value))
					{
						measure.api.LogF(API.LogType.Error, "Invalid command: {0}", args);
						return;
					}
					break;
				case "setshuffle":
					t = ActionType.SETSHUFFLE;
					if (argTokens.Length < 2)
					{
						measure.api.LogF(API.LogType.Error, "Invalid command: {0}", args);
						return;
					}
					if (!int.TryParse(argTokens[1], out value))
					{
						measure.api.LogF(API.LogType.Error, "Invalid command: {0}", args);
						return;
					}
					break;
				case "setrepeat":
					t = ActionType.SETREPEAT;
					if (argTokens.Length < 2)
					{
						measure.api.LogF(API.LogType.Error, "Invalid command: {0}", args);
						return;
					}
					if (!int.TryParse(argTokens[1], out value))
					{
						measure.api.LogF(API.LogType.Error, "Invalid command: {0}", args);
						return;
					}
					if (value < -1 || value > 2)
					{
						measure.api.LogF(API.LogType.Error, "Invalid command: {0}", args);
						return;
					}
					break;
				case "openplayer":
				case "closeplayer":
				case "toggleplayer":
				case "setrating":
				case "setvolume":
					return;
				default:
					measure.api.LogF(API.LogType.Error, "Invalid command: {0}", args);
					return;
			}
			MediaController.DoAction(t, value, absolute);
		}
	}
}