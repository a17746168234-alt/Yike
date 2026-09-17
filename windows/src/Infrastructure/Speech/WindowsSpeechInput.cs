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
internal sealed class WindowsSpeechInput : ISpeechInputBackend, IDisposable
{
	private readonly object sync = new object ();

	private SpeechRecognitionEngine engine;

	private System.Threading.Timer silence;

	private System.Threading.Timer stopGuard;

	private bool listening;

	private bool disposed;

	private bool heardAudio;

	private bool speechActive;

	public bool IsListening {
		get {
			lock (sync) {
				return listening;
			}
		}
	}

	public event Action<string> Hypothesized;

	public event Action<string> Recognized;

	public event Action<string> Failed;

	public event Action AutoStopped;

	public event Action<int> AudioLevelChanged;

	public bool Start (string language, out string error)
	{
		error = null;
		try {
			string prefix = language.Split ('-') [0];
			RecognizerInfo recognizerInfo = SpeechRecognitionEngine.InstalledRecognizers ().FirstOrDefault ((RecognizerInfo item) => item.Culture.TwoLetterISOLanguageName.Equals (prefix, StringComparison.OrdinalIgnoreCase));
			if (recognizerInfo == null) {
				throw new InvalidOperationException ("未安装该语言的 Windows 语音识别组件，请切换为自动检测。");
			}
			engine = new SpeechRecognitionEngine (recognizerInfo);
			engine.EndSilenceTimeout = TimeSpan.FromMilliseconds (350);
			engine.EndSilenceTimeoutAmbiguous = TimeSpan.FromMilliseconds (600);
			engine.LoadGrammar (new DictationGrammar ());
			engine.SpeechDetected += delegate {
				SpeechActivity ();
			};
			engine.AudioStateChanged += delegate(object s, AudioStateChangedEventArgs e) {
				AudioState (e.AudioState);
			};
			engine.AudioLevelUpdated += delegate(object s, AudioLevelUpdatedEventArgs e) {
				AudioLevel (e.AudioLevel);
			};
			engine.SpeechHypothesized += delegate(object s, SpeechHypothesizedEventArgs e) {
				Publish (e.Result, false);
			};
			engine.SpeechRecognized += delegate(object s, SpeechRecognizedEventArgs e) {
				Publish (e.Result, true);
			};
			engine.RecognizeCompleted += delegate(object s, RecognizeCompletedEventArgs e) {
				Complete (e.Error);
			};
			engine.SetInputToDefaultAudioDevice ();
			lock (sync) {
				listening = true;
				silence = new System.Threading.Timer (TimeoutSilence, null, 8000, -1);
			}
			engine.RecognizeAsync (RecognizeMode.Multiple);
			return true;
		} catch (Exception ex) {
			error = ex.Message;
			Dispose ();
			return false;
		}
	}

	private void AudioState (AudioState state)
	{
		lock (sync) {
			if (!disposed && listening) {
				speechActive = state == System.Speech.Recognition.AudioState.Speech;
				if (speechActive) {
					ResetSilence ();
				}
			}
		}
	}

	private void SpeechActivity ()
	{
		lock (sync) {
			if (!disposed && listening) {
				speechActive = true;
				ResetSilence ();
			}
		}
	}

	private void AudioLevel (int level)
	{
		lock (sync) {
			if (!disposed && listening) {
				if (speechActive && level > 0) {
					ResetSilence ();
				}
				if (this.AudioLevelChanged != null) {
					this.AudioLevelChanged (level);
				}
			}
		}
	}

	private void ResetSilence ()
	{
		heardAudio = true;
		if (silence != null) {
			silence.Change (2000, -1);
		}
	}

	private void Publish (RecognitionResult result, bool final)
	{
		lock (sync) {
			if (disposed || result == null || string.IsNullOrWhiteSpace (result.Text)) {
				return;
			}
			if (final) {
				if (this.Recognized != null) {
					this.Recognized (result.Text.Trim ());
				}
			} else if (this.Hypothesized != null) {
				this.Hypothesized (result.Text.Trim ());
			}
		}
	}

	private void TimeoutSilence (object state)
	{
		bool flag;
		lock (sync) {
			if (disposed || !listening) {
				return;
			}
			flag = !heardAudio;
		}
		if (!Stop ()) {
			return;
		}
		if (flag) {
			if (this.Failed != null) {
				this.Failed ("未检测到麦克风声音，请检查默认输入设备和麦克风权限。");
			}
		} else if (this.AutoStopped != null) {
			this.AutoStopped ();
		}
	}

	private void Complete (Exception error)
	{
		bool flag;
		lock (sync) {
			if (disposed) {
				return;
			}
			flag = listening;
			listening = false;
		}
		ThreadPool.QueueUserWorkItem (delegate {
			Dispose ();
		});
		if (flag && this.Failed != null) {
			this.Failed ((error == null) ? "Windows 语音识别已中断。" : error.Message);
		}
	}

	public bool Stop ()
	{
		SpeechRecognitionEngine speechRecognitionEngine;
		lock (sync) {
			if (disposed || !listening) {
				return false;
			}
			listening = false;
			speechRecognitionEngine = engine;
			if (silence != null) {
				silence.Dispose ();
				silence = null;
			}
			stopGuard = new System.Threading.Timer (delegate {
				Dispose ();
			}, null, 1800, -1);
		}
		try {
			speechRecognitionEngine.RecognizeAsyncStop ();
		} catch {
			Dispose ();
		}
		return true;
	}

	public void Dispose ()
	{
		SpeechRecognitionEngine speechRecognitionEngine;
		lock (sync) {
			if (disposed) {
				return;
			}
			disposed = true;
			listening = false;
			speechRecognitionEngine = engine;
			engine = null;
			if (silence != null) {
				silence.Dispose ();
			}
			if (stopGuard != null) {
				stopGuard.Dispose ();
			}
		}
		if (speechRecognitionEngine != null) {
			try {
				speechRecognitionEngine.RecognizeAsyncCancel ();
			} catch {
			}
			try {
				speechRecognitionEngine.Dispose ();
			} catch {
			}
		}
	}
}

}
