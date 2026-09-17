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
public partial class App {
private bool voiceReady;
private void PostVoiceEvent (Action action)
{
	int generation = voiceInput.Revision;
	if (window.Dispatcher.HasShutdownStarted) {
		return;
	}
	window.Dispatcher.BeginInvoke ((Action)delegate {
		if (!exiting && generation == voiceInput.Revision) {
			action ();
		}
	});
}


private void BindVoiceInput ()
{
	voiceInput.Hypothesized += delegate(string text) {
		PostVoiceEvent (delegate {
			ApplyVoiceDraft (text, false);
		});
	};
	voiceInput.Recognized += delegate(string text) {
		PostVoiceEvent (delegate {
			ApplyVoiceDraft (text, true);
		});
	};
	voiceInput.AudioLevelChanged += delegate(int level) {
		PostVoiceEvent (delegate {
			if (voiceWaveform != null && voiceInput.IsListening) {
				if (!voiceReady) {
					voiceReady = true;
					Status ("正在监听麦克风 · 识别结果实时写入");
				}
				voiceWaveform.Update (level);
			}
		});
	};
	voiceInput.Failed += delegate(string message) {
		PostVoiceEvent (delegate {
			ResetVoiceIndicator ();
			Status ("语音输入失败：" + message + " · 可手动按 Win+H 使用系统语音输入");
		});
	};
	voiceInput.AutoStopped += delegate {
		PostVoiceEvent (delegate {
			ResetVoiceIndicator ();
			Status ("语音输入已自动停止 · 连续 2 秒未检测到声音");
		});
	};
	Button ("VoiceButton", delegate {
		if (voiceInput.IsListening) {
			StopVoiceInput ("语音输入已停止");
		} else {
			StartVoiceInput ();
		}
	});
	spaceHoldTimer = new DispatcherTimer {
		Interval = TimeSpan.FromMilliseconds (300.0)
	};
	spaceHoldTimer.Tick += delegate {
		StartHoldVoiceInput ();
	};
	window.AddHandler (Keyboard.PreviewKeyDownEvent, new System.Windows.Input.KeyEventHandler (VoiceShortcutKeyDown), true);
	window.AddHandler (Keyboard.PreviewKeyUpEvent, new System.Windows.Input.KeyEventHandler (VoiceShortcutKeyUp), true);
	window.Deactivated += delegate {
		ResetVoiceShortcut (true);
	};
}


private void VoiceShortcutKeyDown (object sender, System.Windows.Input.KeyEventArgs e)
{
	if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.None && Keyboard.FocusedElement == input && (!voiceInput.IsListening || spaceVoiceMode)) {
		e.Handled = true;
		if (!spaceKeyDown) {
			spaceKeyDown = true;
			spaceVoiceMode = false;
			spaceHoldTimer.Start ();
		}
	}
}


private void VoiceShortcutKeyUp (object sender, System.Windows.Input.KeyEventArgs e)
{
	if (e.Key == Key.Space && spaceKeyDown) {
		e.Handled = true;
		spaceHoldTimer.Stop ();
		spaceKeyDown = false;
		if (spaceVoiceMode) {
			FinishHoldVoiceInput ();
		} else {
			InsertSpace ();
		}
	}
}


private void StartHoldVoiceInput ()
{
	spaceHoldTimer.Stop ();
	if (spaceKeyDown && !spaceVoiceMode) {
		spaceVoiceMode = true;
		if (StartApplicationVoice ("松开空格停止")) {
			Status (VoiceListeningStatus () + " · 松开空格停止");
		} else {
			Status ("语音输入失败：" + voiceInput.LastError + " · 可手动按 Win+H 使用系统语音输入");
		}
	}
}


private void FinishHoldVoiceInput ()
{
	spaceKeyDown = false;
	spaceVoiceMode = false;
	spaceHoldTimer.Stop ();
	if (voiceInput.IsListening) {
		StopVoiceInput ("语音输入已停止");
	} else {
		ResetVoiceIndicator ();
	}
}


private void ResetVoiceShortcut (bool stopListening)
{
	spaceHoldTimer.Stop ();
	spaceKeyDown = false;
	spaceVoiceMode = false;
	if (stopListening && voiceInput.IsListening) {
		StopVoiceInput ("语音输入已停止");
	}
}


private void InsertSpace ()
{
	int selectionStart = input.SelectionStart;
	input.SelectedText = " ";
	input.CaretIndex = selectionStart + 1;
}


private void BeginVoiceDraft ()
{
	voiceDraft.Begin (input.Text, input.SelectionStart, input.SelectionLength);
}


private void OnVoiceDocumentChanged ()
{
	if (!applyingVoiceDraft && voiceDraft.Active) {
		CancelVoiceDraft ();
	}
}


private void CancelVoiceDraft ()
{
	if (voiceDraft.Active) {
		voiceInput.Cancel ();
		voiceDraft.Cancel ();
		ResetVoiceIndicator ();
	}
}


private void ApplyVoiceDraft (string text, bool final)
{
	string updated;
	int caret;
	if (voiceDraft.TryApply (input.Text, text, final, out updated, out caret)) {
		applyingVoiceDraft = true;
		try {
			input.Text = updated;
			input.CaretIndex = caret;
		} finally {
			applyingVoiceDraft = false;
		}
		string text2 = SpeechText.LanguageLabel (text);
		Status ((!final) ? ("正在识别 · " + text2) : (voiceInput.IsListening ? ("语音输入中 · 已识别 " + text2) : ("语音已输入 · " + text2)));
	}
}


private void ResetVoiceIndicator ()
{
	voiceWaveform = null;
	SetButtonIcon ("VoiceButton", "\ue720", "语音输入");
}


private void StopVoiceInput (string message)
{
	voiceInput.Stop ();
	ResetVoiceIndicator ();
	Status (message);
}


private bool StartApplicationVoice (string tooltip)
{
	BeginVoiceDraft ();
	voiceReady = false;
	if (!voiceInput.Start (Code (source))) {
		voiceDraft.Cancel ();
		ResetVoiceIndicator ();
		return false;
	}
	voiceWaveform = new VoiceWaveform ();
	System.Windows.Controls.Button button = Find<System.Windows.Controls.Button> ("VoiceButton");
	button.Content = voiceWaveform.Content;
	button.ToolTip = tooltip;
	button.VerticalContentAlignment = VerticalAlignment.Center;
	return true;
}


private string VoiceListeningStatus ()
{
	if (!(Code (source) == "auto")) {
		return "正在监听麦克风 · 识别结果实时写入";
	}
	return "正在准备麦克风 · 自动区分中文 / English · 识别结果实时写入";
}


private void StartVoiceInput ()
{
	input.Focus ();
	if (StartApplicationVoice ("点击停止语音输入")) {
		Status (VoiceListeningStatus ());
	} else {
		Status ("语音输入失败：" + voiceInput.LastError + " · 可手动按 Win+H 使用系统语音输入");
	}
}

}
}
