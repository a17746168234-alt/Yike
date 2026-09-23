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
	public const int StepMilliseconds = 1000;

	public const int WindowMilliseconds = 8000;

	public const int KeepMilliseconds = 500;

	private readonly object sync = new object ();

	private readonly WhisperStreamParser parser = new WhisperStreamParser ();

	private readonly Stopwatch elapsed = new Stopwatch ();
	private readonly Func<ProcessStartInfo> createHelper;
	private readonly Func<int> readLevel;

	private Process process;

	private ProcessJob processJob;

	private Process refinementProcess;

	private ProcessJob refinementJob;

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

	private string language = "auto";

	private string recordingDirectory;

	private string lastLiveText = "";

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

	public static string CliPath {
		get {
			return System.IO.Path.Combine (RuntimeRoot, "Release", "whisper-cli.exe");
		}
	}

	public static string ModelPath {
		get {
			return System.IO.Path.Combine (RuntimeRoot, "ggml-small-q8_0.bin");
		}
	}

	public static bool IsAvailable {
		get {
			if (File.Exists (ExecutablePath) && File.Exists (CliPath)) {
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

	internal static string LanguageCode (string value)
	{
		if (string.IsNullOrWhiteSpace (value) || string.Equals (value.Trim (), "auto", StringComparison.OrdinalIgnoreCase)) {
			return "auto";
		}
		string text = value.Trim ().Split ('-') [0].ToLowerInvariant ();
		return Regex.IsMatch (text, "^[a-z]{2}$") ? text : "auto";
	}

	internal static string StreamingArguments (string value)
	{
		return "-m " + Quote (ModelPath) + " -l " + LanguageCode (value) +
			" --step " + StepMilliseconds + " --length " + WindowMilliseconds +
			" --keep " + KeepMilliseconds + " -mt 64 -bs 3 -kc -t " + ThreadCount + " -ng -sa";
	}

	internal static string RefinementArguments (string value, string audioPath)
	{
		string languageCode = LanguageCode (value);
		string prompt = languageCode == "zh" ? "以下是清晰、自然的简体中文口述。" :
			(languageCode == "en" ? "The following is clear, natural English dictation." : "以下是自然的中英文口述。 Clear Chinese and English dictation.");
		return "-m " + Quote (ModelPath) + " -f " + Quote (audioPath) + " -l " + LanguageCode (value) +
			" -bs 8 -bo 8 -t " + ThreadCount + " -ng -np -nt -sns --prompt " + Quote (prompt);
	}

	private static int ThreadCount {
		get {
			return Math.Max (2, Math.Min (8, Environment.ProcessorCount / 2));
		}
	}

	private static string Quote (string value)
	{
		return "\"" + (value ?? "").Replace ("\"", "\\\"") + "\"";
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
			this.language = LanguageCode (language);
			if (createHelper == null) {
				recordingDirectory = System.IO.Path.Combine (System.IO.Path.GetTempPath (), "Yike-voice-" + Guid.NewGuid ().ToString ("N"));
				Directory.CreateDirectory (recordingDirectory);
			}
			ProcessStartInfo processStartInfo = new ProcessStartInfo ();
			processStartInfo.FileName = ExecutablePath;
			processStartInfo.WorkingDirectory = recordingDirectory ?? System.IO.Path.GetDirectoryName (ExecutablePath);
			processStartInfo.UseShellExecute = false;
			processStartInfo.CreateNoWindow = true;
			processStartInfo.RedirectStandardOutput = true;
			processStartInfo.RedirectStandardError = true;
			processStartInfo.StandardOutputEncoding = Encoding.UTF8;
			processStartInfo.StandardErrorEncoding = Encoding.UTF8;
			processStartInfo.Arguments = StreamingArguments (this.language);
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
		bool refine = false;
		lock (sync) {
			if (!disposed) {
				parser.Complete ();
				refine = createHelper == null && stopping && heardAudio;
			}
		}
		if (refine) {
			string refinedText = await RefineRecording ().ConfigureAwait (false);
			if (string.IsNullOrWhiteSpace (refinedText)) {
				lock (sync) {
					refinedText = lastLiveText;
				}
			}
			PublishFinal (refinedText);
		}
		string text = null;
		ProcessJob processJob;
		lock (sync) {
			if (!disposed) {
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
			if (process == active) {
				process = null;
			}
		}
		active.Dispose ();
		if (processJob != null) {
			processJob.Dispose ();
		}
		if (text != null && this.Failed != null) {
			this.Failed (text);
		}
		CleanupRecordingDirectory ();
	}

	private async Task<string> RefineRecording ()
	{
		string directory;
		lock (sync) {
			if (disposed) {
				return "";
			}
			directory = recordingDirectory;
		}
		try {
			string audioPath = Directory.GetFiles (directory, "*.wav").OrderByDescending (File.GetLastWriteTimeUtc).FirstOrDefault ();
			if (string.IsNullOrWhiteSpace (audioPath) || !RepairWaveHeader (audioPath)) {
				return "";
			}
			string enhancedPath;
			if (WaveAudioEnhancer.TryEnhance (audioPath, out enhancedPath)) {
				audioPath = enhancedPath;
			}
			ProcessStartInfo info = new ProcessStartInfo {
				FileName = CliPath,
				WorkingDirectory = System.IO.Path.GetDirectoryName (CliPath),
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8,
				Arguments = RefinementArguments (language, audioPath)
			};
			Process active = Process.Start (info);
			if (active == null) {
				return "";
			}
			ProcessJob job = null;
			try {
				job = ProcessJob.AttachOrTerminate (active);
				lock (sync) {
					if (disposed) {
						return "";
					}
					refinementProcess = active;
					refinementJob = job;
				}
				Task<string> output = active.StandardOutput.ReadToEndAsync ();
				Task<string> errors = active.StandardError.ReadToEndAsync ();
				await Task.WhenAll (output, errors).ConfigureAwait (false);
				active.WaitForExit ();
				if (active.ExitCode != 0) {
					return "";
				}
				string combined = "";
				foreach (string line in output.Result.Split (new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
					string cleaned = Clean (line);
					if (cleaned.Length > 0) {
						combined += SpeechText.Insertion (combined, cleaned, "");
					}
				}
				return combined.Trim ();
			} finally {
				lock (sync) {
					if (refinementProcess == active) refinementProcess = null;
					if (refinementJob == job) refinementJob = null;
				}
				if (job != null) job.Dispose ();
				active.Dispose ();
			}
		} catch (Exception ex) {
			lock (sync) {
				if (!disposed) lastError = ex.Message;
			}
			return "";
		}
	}

	private void PublishFinal (string text)
	{
		Action<string> handler = null;
		lock (sync) {
			if (!disposed && !string.IsNullOrWhiteSpace (text)) {
				receivedText = true;
				handler = this.Recognized;
			}
		}
		if (handler != null) {
			handler (text);
		}
	}

	internal static bool RepairWaveHeader (string path)
	{
		try {
			using (FileStream stream = new FileStream (path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) {
				if (stream.Length < 44) return false;
				byte[] header = new byte[12];
				if (stream.Read (header, 0, header.Length) != header.Length || Encoding.ASCII.GetString (header, 0, 4) != "RIFF" || Encoding.ASCII.GetString (header, 8, 4) != "WAVE") return false;
				long offset = 12;
				long dataSizeOffset = -1;
				long dataOffset = -1;
				byte[] chunk = new byte[8];
				while (offset + 8 <= stream.Length) {
					stream.Position = offset;
					if (stream.Read (chunk, 0, chunk.Length) != chunk.Length) break;
					uint size = BitConverter.ToUInt32 (chunk, 4);
					if (Encoding.ASCII.GetString (chunk, 0, 4) == "data") {
						dataSizeOffset = offset + 4;
						dataOffset = offset + 8;
						break;
					}
					offset += 8L + size + (size & 1u);
				}
				if (dataOffset < 0 || dataOffset >= stream.Length) return false;
				using (BinaryWriter writer = new BinaryWriter (stream, Encoding.ASCII, true)) {
					stream.Position = 4;
					writer.Write ((uint)Math.Min (uint.MaxValue, stream.Length - 8));
					stream.Position = dataSizeOffset;
					writer.Write ((uint)Math.Min (uint.MaxValue, stream.Length - dataOffset));
					writer.Flush ();
				}
				return true;
			}
		} catch (IOException) {
			return false;
		} catch (UnauthorizedAccessException) {
			return false;
		}
	}

	private void CleanupRecordingDirectory ()
	{
		string directory;
		lock (sync) {
			directory = recordingDirectory;
			recordingDirectory = null;
		}
		if (string.IsNullOrWhiteSpace (directory)) return;
		try {
			string full = System.IO.Path.GetFullPath (directory).TrimEnd (System.IO.Path.DirectorySeparatorChar);
			string temp = System.IO.Path.GetFullPath (System.IO.Path.GetTempPath ()).TrimEnd (System.IO.Path.DirectorySeparatorChar);
			if (string.Equals (System.IO.Path.GetDirectoryName (full), temp, StringComparison.OrdinalIgnoreCase) && Regex.IsMatch (System.IO.Path.GetFileName (full), "^Yike-voice-[0-9a-f]{32}$", RegexOptions.IgnoreCase)) {
				Directory.Delete (full, true);
			}
		} catch (IOException) {
		} catch (UnauthorizedAccessException) {
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
		lastLiveText = text;
		receivedText = true;
		if (final && createHelper != null) {
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
				if (level >= 2) { heardAudio = true; lastAudio = elapsedMilliseconds; }
				if (this.AudioLevelChanged != null) this.AudioLevelChanged (level);
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
					flag = (flag2 ? (elapsedMilliseconds - readyAt >= SpeechInput.StartupSilenceMilliseconds) : (elapsedMilliseconds - lastAudio >= SpeechInput.SilenceMilliseconds));
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
		Process refinementProcess;
		ProcessJob refinementJob;
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
			refinementProcess = this.refinementProcess;
			this.refinementProcess = null;
			refinementJob = this.refinementJob;
			this.refinementJob = null;
			ReleasePolling ();
			if (stopGuard != null) { stopGuard.Dispose (); stopGuard = null; }
		}
		if (processJob != null) {
			processJob.Dispose ();
		}
		if (refinementJob != null) {
			refinementJob.Dispose ();
		}
		Terminate (process);
		Terminate (refinementProcess);
		if (process != null) {
			try {
				process.WaitForExit (2000);
			} catch (InvalidOperationException) {
			}
		}
		if (refinementProcess != null) {
			try {
				refinementProcess.WaitForExit (2000);
			} catch (InvalidOperationException) {
			}
		}
		CleanupRecordingDirectory ();
	}
}

}
