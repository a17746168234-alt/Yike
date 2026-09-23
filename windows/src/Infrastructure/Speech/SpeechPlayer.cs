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
    private readonly Queue<string> ready = new Queue<string> ();
    private string folder, audioPath;
    private int revision;
    private bool generated, paused;
    private TimeSpan pausedPosition, completedPosition;
    private readonly double volume;
    private readonly Func<string, bool, ProcessStartInfo> createHelper;

    public string State { get; private set; }
    internal TimeSpan Position {
        get { return completedPosition + (player == null ? TimeSpan.Zero : (paused ? pausedPosition : player.Position)); }
    }
    internal bool IsGenerating { get { return pending != null; } }
    public event Action Changed;
    public event Action<string> Failed;

    public SpeechPlayer (double volume = 1.0) : this (volume, null) {}

    internal SpeechPlayer (double volume, Func<string, bool, ProcessStartInfo> createHelper)
    {
        this.volume = volume;
        this.createHelper = createHelper;
        State = "idle";
    }

    private void SetState (string state) { State = state; if (Changed != null) Changed (); }

    public static string Voice (string language, string gender)
    {
        bool male = gender == "male";
        switch (language) {
        case "EN-US": return male ? "en-US-GuyNeural" : "en-US-JennyNeural";
        case "JA": return male ? "ja-JP-KeitaNeural" : "ja-JP-NanamiNeural";
        case "KO": return male ? "ko-KR-InJoonNeural" : "ko-KR-SunHiNeural";
        default: return male ? "zh-CN-YunxiNeural" : "zh-CN-XiaoxiaoNeural";
        }
    }

    public async Task Speak (string text, string language, string gender, int rate, bool online)
    {
        Stop ();
        string[] chunks = SpeechChunks.Split (text);
        if (chunks.Length == 0) return;
        int ticket = revision;
        CancellationTokenSource cancel = pending = new CancellationTokenSource ();
        CancellationToken ct = cancel.Token;
        string requestFolder = System.IO.Path.Combine (System.IO.Path.GetTempPath (), "YikeSpeech", Guid.NewGuid ().ToString ("N"));
        string jobPath = System.IO.Path.Combine (requestFolder, "job.json");
        folder = requestFolder;
        try {
            Directory.CreateDirectory (requestFolder);
            File.WriteAllText (jobPath, Store.Json.Serialize (new {
                chunks = chunks, folder = requestFolder,
                voice = Voice (language, gender), language = language, gender = gender,
                rate = Math.Max (-50, Math.Min (50, rate * 5)),
                proxy = online ? ProxySettings.Current : null
            }), Encoding.UTF8);
            SetState ("loading");
            string root = AppDomain.CurrentDomain.BaseDirectory;
            ProcessStartInfo start = new ProcessStartInfo {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            if (online) {
                start.FileName = System.IO.Path.Combine (root, "speech-runtime", "python.exe");
                start.Arguments = "\"" + System.IO.Path.Combine (root, "speech-online.py") + "\" \"" + jobPath + "\"";
				start.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"] = "1";
            } else {
                start.FileName = System.IO.Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");
                start.Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + System.IO.Path.Combine (root, "speech-local.ps1") + "\" -JobPath \"" + jobPath + "\"";
            }
            if (createHelper != null) start = createHelper (jobPath, online);
            using (Process process = Process.Start (start)) {
                if (process == null) throw new InvalidOperationException ("无法启动语音生成进程。");
                using (ProcessJob.AttachOrTerminate (process))
                using (ct.Register (delegate { Terminate (process); })) {
                    Task<string> errors = process.StandardError.ReadToEndAsync ();
                    int expected = 0;
                    while (true) {
                        Task<string> line = process.StandardOutput.ReadLineAsync ();
                        if (await Task.WhenAny (line, Task.Delay (25000, ct)) != line) {
                            ct.ThrowIfCancellationRequested ();
                            throw new TimeoutException ("语音生成超时，请检查网络或切换本机语音。");
                        }
                        string message = await line;
                        ct.ThrowIfCancellationRequested ();
                        if (message == null) break;
                        int index;
                        if (!message.StartsWith ("READY ", StringComparison.Ordinal) ||
                            !int.TryParse (message.Substring (6), out index) || index != expected || index >= chunks.Length)
                            throw new InvalidOperationException ("语音生成组件返回了无效结果。");
                        string audio = System.IO.Path.Combine (requestFolder, index + (online ? ".mp3" : ".wav"));
                        if (!File.Exists (audio) || new FileInfo (audio).Length < 100)
                            throw new InvalidOperationException ("未能生成完整音频。");
                        if (ticket != revision) return;
                        expected++;
                        ready.Enqueue (audio);
                        if (player == null && !paused) PlayNext (ticket);
                    }
                    if (!await Task.Run (() => process.WaitForExit (5000))) {
                        Terminate (process);
                        throw new TimeoutException ("语音生成组件未正常退出。");
                    }
                    await errors;
                    ct.ThrowIfCancellationRequested ();
                    if (process.ExitCode != 0 || expected != chunks.Length)
                        throw new InvalidOperationException (online ?
                            "在线语音暂时不可用，请检查网络或切换本机语音。" :
                            "本机未能生成该音色，请切换在线自然语音或安装对应语言包。");
                }
            }
            if (ticket == revision) {
                generated = true;
                if (player == null && ready.Count == 0 && !paused) Stop ();
            }
        } catch (OperationCanceledException) {
        } catch (Exception ex) {
            if (ticket == revision) {
                Stop ();
                if (Failed != null) Failed (ex.Message);
            }
        } finally {
            DeleteFile (jobPath);
            if (ticket != revision) DeleteFolder (requestFolder);
            if (pending == cancel) pending = null;
            cancel.Dispose ();
        }
    }

    private void PlayNext (int ticket)
    {
        if (ticket != revision || paused) return;
        if (ready.Count == 0) {
            if (generated) Stop ();
            else SetState ("loading");
            return;
        }
        MediaPlayer current = new MediaPlayer { Volume = volume };
        player = current;
        audioPath = ready.Dequeue ();
        current.MediaOpened += delegate {
            if (ticket != revision || player != current) return;
            if (paused) SetState ("paused");
            else { current.Play (); SetState ("playing"); }
        };
        current.MediaEnded += delegate {
            if (ticket != revision || player != current) return;
            if (current.NaturalDuration.HasTimeSpan) completedPosition += current.NaturalDuration.TimeSpan;
            current.Close ();
            player = null;
            DeleteFile (audioPath);
            audioPath = null;
            pausedPosition = TimeSpan.Zero;
            if (!paused) PlayNext (ticket);
        };
        current.MediaFailed += delegate(object sender, ExceptionEventArgs e) {
            if (ticket != revision || player != current) return;
            Stop ();
            if (Failed != null) Failed ("音频播放失败：" + e.ErrorException.Message);
        };
        current.Open (new Uri (audioPath));
    }

    public void TogglePause ()
    {
        if (State == "playing" || State == "loading") {
            paused = true;
            if (player != null) { player.Pause (); pausedPosition = player.Position; }
            SetState ("paused");
        } else if (State == "paused") {
            paused = false;
            if (player != null) { player.Position = pausedPosition; player.Play (); SetState ("playing"); }
            else PlayNext (revision);
        }
    }

    public void Stop ()
    {
        revision++;
        paused = false;
        generated = false;
        pausedPosition = completedPosition = TimeSpan.Zero;
        if (pending != null) { pending.Cancel (); pending = null; }
        if (player != null) { player.Close (); player = null; }
        ready.Clear ();
        audioPath = null;
        string previous = folder;
        folder = null;
        DeleteFolder (previous);
        SetState ("idle");
    }

    private static void Terminate (Process process)
    {
        try { if (!process.HasExited) process.Kill (); } catch (InvalidOperationException) {} catch (Win32Exception) {}
    }

    private static void DeleteFile (string path)
    {
        try { if (path != null) File.Delete (path); } catch (IOException) {} catch (UnauthorizedAccessException) {}
    }

    private static void DeleteFolder (string path)
    {
        if (path == null) return;
        string parent = System.IO.Path.Combine (System.IO.Path.GetTempPath (), "YikeSpeech");
        Guid id;
        if (!string.Equals (System.IO.Path.GetDirectoryName (path), parent, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact (System.IO.Path.GetFileName (path), "N", out id)) return;
        try { Directory.Delete (path, true); } catch (IOException) {} catch (UnauthorizedAccessException) {}
    }

    public void Dispose () { Stop (); }
}
}
