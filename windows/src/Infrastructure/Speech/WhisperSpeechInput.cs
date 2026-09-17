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
internal sealed class WhisperSpeechInput : ISpeechInputBackend, IDisposable
{
	public const int StepMilliseconds = 600;

	public const int WindowMilliseconds = 2400;

	private readonly object sync = new object ();

	private readonly WhisperStreamParser parser = new WhisperStreamParser ();

	private readonly Stopwatch elapsed = new Stopwatch ();
	private readonly Func<ProcessStartInfo> createHelper;
	private readonly Func<int> readLevel;

	private Process process;

	private ProcessJob processJob;

	private Task completion;

	private System.Threading.Timer poll;

	private System.Threading.Timer stopGuard;

	private MicrophoneLevel meter;

	private bool listening;

	private bool disposed;

	private bool stopping;

	private bool ready;

	private bool heardAudio;
	private bool receivedText;

	private long readyAt;

	private long lastAudio;

	private string lastError = "";

	public bool IsListening {
		get {
			lock (sync) {
				return listening;
			}
		}
	}

	internal bool IsReady {
		get {
			lock (sync) {
				return ready && listening;
			}
		}
	}

	internal Task Completion {
		get {
			return completion ?? Task.FromResult (0);
		}
	}

	public static string RuntimeRoot {
		get {
			return System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "whisper-runtime");
		}
	}

	public static string ExecutablePath {
		get {
			return System.IO.Path.Combine (RuntimeRoot, "Release", "whisper-stream.exe");
		}
	}

	public static string ModelPath {
		get {
			return System.IO.Path.Combine (RuntimeRoot, "ggml-base-q5_1.bin");
		}
	}

	public static bool IsAvailable {
		get {
			if (File.Exists (ExecutablePath)) {
				return File.Exists (ModelPath);
			}
			return false;
		}
	}

	public event Action<string> Hypothesized;

	public event Action<string> Recognized;

	public event Action<string> Failed;

	public event Action AutoStopped;

	public event Action<int> AudioLevelChanged;

	public WhisperSpeechInput () : this (null, null) {}

	internal WhisperSpeechInput (Func<ProcessStartInfo> createHelper, Func<int> readLevel)
	{
		this.createHelper = createHelper;
		this.readLevel = readLevel;
		WhisperStreamParser whisperStreamParser = parser;
		Action value = delegate {
			ready = true;
			readyAt = elapsed.ElapsedMilliseconds;
		};
		whisperStreamParser.Ready += value;
		parser.Text += Publish;
	}

	public bool Start (out string error)
	{
		return Start ("auto", out error);
	}

	public bool Start (string language, out string error)
	{
		error = null;
		if (createHelper == null && !IsAvailable) {
			error = "离线识别组件缺失，请重新安装完整版本。";
			return false;
		}
		try {
			ProcessStartInfo processStartInfo = new ProcessStartInfo ();
			processStartInfo.FileName = ExecutablePath;
			processStartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName (ExecutablePath);
			processStartInfo.UseShellExecute = false;
			processStartInfo.CreateNoWindow = true;
			processStartInfo.RedirectStandardOutput = true;
			processStartInfo.RedirectStandardError = true;
			processStartInfo.StandardOutputEncoding = Encoding.UTF8;
			processStartInfo.StandardErrorEncoding = Encoding.UTF8;
			processStartInfo.Arguments = "-m \"..\\ggml-base-q5_1.bin\" -l auto --step " + StepMilliseconds + " --length " + WindowMilliseconds + " --keep 0 -ac 256 -t " + Math.Max (2, Math.Min (6, Environment.ProcessorCount / 2)) + " -ng -nf";
			ProcessStartInfo startInfo = processStartInfo;
			if (createHelper != null) startInfo = createHelper ();
			lock (sync) {
				if (process != null || disposed) {
					throw new InvalidOperationException ("录音会话不能重复启动。");
				}
				if (readLevel == null) meter = new MicrophoneLevel ();
				process = Process.Start (startInfo);
				if (process == null) {
					throw new InvalidOperationException ("无法启动离线识别进程。");
				}
				try {
					processJob = ProcessJob.AttachOrTerminate (process);
				} catch (Exception innerException) {
					process.Dispose ();
					process = null;
					throw new InvalidOperationException ("无法建立离线识别进程的退出保护。", innerException);
				}
				listening = true;
				elapsed.Start ();
				poll = new System.Threading.Timer (Poll, null, 0, 60);
				completion = Task.Run (() => Run (process));
			}
			return true;
		} catch (Exception ex) {
			error = ex.Message;
			Dispose ();
			return false;
		}
	}

	private async Task Run (Process active)
	{
		int num = default(int);
		int num2 = num;
		int num3 = 0;
		try {
			int num4 = num;
			int num5 = 0;
			try {
				Task output = ReadOutput (active);
				Task errors = ReadErrors (active);
				await Task.WhenAll (output, errors).ConfigureAwait (false);
				active.WaitForExit ();
			} catch (Exception ex) {
				lock (sync) {
					lastError = ex.Message;
				}
			}
		} finally {
			string text = null;
			ProcessJob processJob;
			lock (sync) {
				if (!disposed) {
					parser.Complete ();
					if (!stopping) {
						text = (string.IsNullOrWhiteSpace (lastError) ? "离线识别已中断，请检查麦克风或重新安装应用。" : lastError);
					} else if (heardAudio && !receivedText) {
						text = "未识别到清晰语音，请靠近麦克风后重试。";
					}
				}
				listening = false;
				ReleasePolling ();
				if (stopGuard != null) { stopGuard.Dispose (); stopGuard = null; }
				processJob = this.processJob;
				this.processJob = null;
			}
			active.Dispose ();
			if (processJob != null) {
				processJob.Dispose ();
			}
			if (text != null && this.Failed != null) {
				this.Failed (text);
			}
		}
	}

	private async Task ReadOutput (Process active)
	{
		await Utf8PipeReader.Read (active.StandardOutput.BaseStream, delegate(string chunk) {
			lock (sync) {
				if (!disposed) {
					parser.Feed (chunk);
					parser.Preview ();
				}
			}
		}).ConfigureAwait (false);
	}

	private async Task ReadErrors (Process active)
	{
		while (true) {
			string text;
			string line = (text = await active.StandardError.ReadLineAsync ().ConfigureAwait (false));
			if (text == null) {
				break;
			}
			if (line.IndexOf ("error", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf ("failed", StringComparison.OrdinalIgnoreCase) >= 0) {
				lock (sync) {
					lastError = line;
				}
			}
		}
	}

	private void Publish (string text, bool final)
	{
		if (disposed || !heardAudio) {
			return;
		}
		receivedText = true;
		if (final) {
			if (this.Recognized != null) {
				this.Recognized (text);
			}
		} else if (this.Hypothesized != null) {
			this.Hypothesized (text);
		}
	}

	private void Poll (object state)
	{
		bool flag = false;
		bool flag2 = false;
		string text = null;
		lock (sync) {
			if (disposed || !listening) {
				return;
			}
			long elapsedMilliseconds = elapsed.ElapsedMilliseconds;
			try {
				int level = readLevel == null ? meter.Read () : readLevel ();
				if (level > 0) { heardAudio = true; lastAudio = elapsedMilliseconds; }
				if (ready && this.AudioLevelChanged != null) this.AudioLevelChanged (level);
			} catch (Exception ex) {
				text = "无法读取麦克风音量：" + ex.Message;
			}
			if (!ready) {
				if (elapsedMilliseconds > 20000) {
					text = "离线语音模型启动超时，请重新尝试。";
				}
			} else {
				try {
					parser.Preview ();
					flag2 = !heardAudio;
					flag = (flag2 ? (elapsedMilliseconds - readyAt >= 8000) : (elapsedMilliseconds - lastAudio >= 2000));
				} catch (Exception ex) {
					text = "无法读取麦克风音量：" + ex.Message;
				}
			}
		}
		if (text != null) {
			if (Stop () && this.Failed != null) {
				this.Failed (text);
			}
		} else {
			if (!flag || !Stop ()) {
				return;
			}
			if (flag2) {
				if (this.Failed != null) {
					this.Failed ("未检测到麦克风声音，请检查默认输入设备和麦克风权限。");
				}
			} else if (this.AutoStopped != null) {
				this.AutoStopped ();
			}
		}
	}

	public bool Stop ()
	{
		Process active;
		bool drain;
		lock (sync) {
			if (!listening) {
				return false;
			}
			stopping = true;
			listening = false;
			active = process;
			drain = ready && heardAudio;
			parser.Preview ();
			ReleasePolling ();
			// Let the already captured final words finish before closing the pipe.
			// Cancel/dispose still terminates immediately when a new session starts.
			if (drain) stopGuard = new System.Threading.Timer (delegate { Terminate (active); }, null, SpeechInput.GracefulStopMilliseconds, -1);
		}
		if (!drain) Terminate (active);
		return true;
	}

	private void ReleasePolling ()
	{
		if (poll != null) {
			poll.Dispose ();
			poll = null;
		}
		if (meter != null) {
			meter.Dispose ();
			meter = null;
		}
	}

	private static void Terminate (Process active)
	{
		if (active == null) {
			return;
		}
		try {
			if (!active.HasExited) {
				active.Kill ();
			}
		} catch (InvalidOperationException) {
		} catch (Win32Exception) {
		}
	}

	internal static string Clean (string raw)
	{
		return WhisperStreamParser.Clean (raw);
	}

	public void Dispose ()
	{
		Process process;
		ProcessJob processJob;
		lock (sync) {
			if (disposed) {
				return;
			}
			disposed = true;
			listening = false;
			stopping = true;
			process = this.process;
			processJob = this.processJob;
			this.processJob = null;
			ReleasePolling ();
			if (stopGuard != null) { stopGuard.Dispose (); stopGuard = null; }
		}
		if (processJob != null) {
			processJob.Dispose ();
		}
		Terminate (process);
		if (process != null) {
			try {
				process.WaitForExit (2000);
			} catch (InvalidOperationException) {
			}
		}
	}
}

}
