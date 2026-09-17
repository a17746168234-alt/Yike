using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Speech.Recognition;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;
namespace WindowsTranslator {
public sealed class SpeechPlayer : IDisposable
{
	private MediaPlayer player;

	private CancellationTokenSource pending;

	private string audioPath;

	private int revision;

	private TimeSpan pausedPosition;

	private readonly double volume;

	public string State { get; private set; }

	internal TimeSpan Position {
		get {
			if (player != null) {
				if (!(State == "paused")) {
					return player.Position;
				}
				return pausedPosition;
			}
			return TimeSpan.Zero;
		}
	}

	public event Action Changed;

	public event Action<string> Failed;

	public SpeechPlayer (double volume = 1.0)
	{
		this.volume = volume;
		State = "idle";
	}

	private void SetState (string state)
	{
		State = state;
		if (this.Changed != null) {
			this.Changed ();
		}
	}

	public static string Voice (string language, string gender)
	{
		bool flag = gender == "male";
		switch (language) {
		case "EN-US":
			if (!flag) {
				return "en-US-JennyNeural";
			}
			return "en-US-GuyNeural";
		case "JA":
			if (!flag) {
				return "ja-JP-NanamiNeural";
			}
			return "ja-JP-KeitaNeural";
		case "KO":
			if (!flag) {
				return "ko-KR-SunHiNeural";
			}
			return "ko-KR-InJoonNeural";
		default:
			if (!flag) {
				return "zh-CN-XiaoxiaoNeural";
			}
			return "zh-CN-YunxiNeural";
		}
	}

	public async Task Speak (string text, string language, string gender, int rate, bool online)
	{
		Stop ();
		int ticket = revision;
		CancellationTokenSource cancel = (pending = new CancellationTokenSource ());
		CancellationToken ct = cancel.Token;
		string folder = System.IO.Path.Combine (System.IO.Path.GetTempPath (), "YikeSpeech");
		Directory.CreateDirectory (folder);
		string stem = System.IO.Path.Combine (folder, Guid.NewGuid ().ToString ("N"));
		string jobPath = stem + ".json";
		string outputPath = stem + (online ? ".mp3" : ".wav");
		bool retained = false;
		try {
			File.WriteAllText (jobPath, Store.Json.Serialize (new {
				text = text,
				voice = Voice (language, gender),
				language = language,
				gender = gender,
				rate = Math.Max (-50, Math.Min (50, rate * 5)),
				output = outputPath,
				proxy = (online ? ProxySettings.Current : null)
			}));
			SetState ("loading");
			string root = AppDomain.CurrentDomain.BaseDirectory;
			ProcessStartInfo start = new ProcessStartInfo {
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true
			};
			if (online) {
				start.FileName = System.IO.Path.Combine (root, "speech-runtime", "python.exe");
				start.Arguments = "\"" + System.IO.Path.Combine (root, "speech-online.py") + "\" \"" + jobPath + "\"";
			} else {
				start.FileName = System.IO.Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");
				start.Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + System.IO.Path.Combine (root, "speech-local.ps1") + "\" -JobPath \"" + jobPath + "\"";
			}
			Process process = Process.Start (start);
			try {
				if (process == null) {
					throw new InvalidOperationException ("无法启动语音生成进程。");
				}
				using (ProcessJob.AttachOrTerminate (process)) {
					Task<string> errors = process.StandardError.ReadToEndAsync ();
					using (ct.Register (delegate {
						try {
							if (!process.HasExited) {
								process.Kill ();
							}
						} catch {
						}
					})) {
						if (!(await Task.Run (() => process.WaitForExit (65000)))) {
							try {
								process.Kill ();
							} catch {
							}
							throw new TimeoutException ("语音生成超时，请检查网络或切换本机语音。");
						}
						ct.ThrowIfCancellationRequested ();
						await errors;
						if (process.ExitCode != 0 || !File.Exists (outputPath) || new FileInfo (outputPath).Length < 100) {
							throw new InvalidOperationException (online ? "在线语音暂时不可用，请检查网络或切换本机语音。" : "本机未能生成该音色，请切换在线自然语音或安装对应语言包。");
						}
					}
				}
			} finally {
				if (process != null) {
					((IDisposable)process).Dispose ();
				}
			}
			if (ticket != revision) {
				return;
			}
			MediaPlayer current = new MediaPlayer {
				Volume = volume
			};
			player = current;
			audioPath = outputPath;
			retained = true;
			current.MediaOpened += delegate {
				if (ticket == revision) {
					current.Play ();
					SetState ("playing");
				}
			};
			current.MediaEnded += delegate {
				if (ticket == revision) {
					Stop ();
				}
			};
			current.MediaFailed += delegate(object s, ExceptionEventArgs e) {
				if (ticket == revision) {
					Stop ();
					if (this.Failed != null) {
						this.Failed ("音频播放失败：" + e.ErrorException.Message);
					}
				}
			};
			current.Open (new Uri (outputPath));
		} catch (OperationCanceledException) {
		} catch (Exception ex2) {
			if (ticket == revision) {
				Stop ();
				if (this.Failed != null) {
					this.Failed (ex2.Message);
				}
			}
		} finally {
			try {
				File.Delete (jobPath);
				if (!retained) {
					File.Delete (outputPath);
				}
			} catch {
			}
			if (pending == cancel) {
				pending = null;
			}
			cancel.Dispose ();
		}
	}

	public void TogglePause ()
	{
		if (player != null) {
			if (State == "playing") {
				player.Pause ();
				pausedPosition = player.Position;
				SetState ("paused");
			} else if (State == "paused") {
				player.Position = pausedPosition;
				player.Play ();
				SetState ("playing");
			}
		}
	}

	public void Stop ()
	{
		revision++;
		pausedPosition = TimeSpan.Zero;
		if (pending != null) {
			pending.Cancel ();
			pending = null;
		}
		if (player != null) {
			player.Close ();
			player = null;
		}
		if (audioPath != null) {
			try {
				File.Delete (audioPath);
			} catch {
			}
			audioPath = null;
		}
		SetState ("idle");
	}

	public void Dispose ()
	{
		Stop ();
	}
}

}
