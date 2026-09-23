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
private void BindTextInput ()
{
	AttachTextContextMenu (input);
	AttachTextContextMenu (output);
	input.TextChanged += delegate {
		OnVoiceDocumentChanged ();
		Cancel ();
		Find<FrameworkElement> ("SourcePlaceholder").Visibility = ((input.Text.Length != 0 || original != null) ? Visibility.Collapsed : Visibility.Visible);
		Find<TextBlock> ("CharacterCount").Text = input.Text.Length + " / 5000";
		UpdateDetectedLanguage ();
		UpdateActionAvailability ();
	};
	output.TextChanged += delegate {
		if (busy && !settingOutputText) {
			Cancel ();
			Status ("已停止翻译，保留手动编辑的译文");
		}
		UpdateResult ();
	};
	input.PreviewKeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e) {
		if (e.Key == Key.Return && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) {
			e.Handled = true;
			Translate ();
		}
	};
	source.SelectionChanged += delegate {
		CancelVoiceDraft ();
		Cancel ();
		prefs.Source = Code (source);
		UpdateDetectedLanguage ();
		SavePreferences ();
	};
	target.SelectionChanged += delegate {
		Cancel ();
		prefs.Target = Code (target);
		SavePreferences ();
	};
}


private void BindMainWindowActions ()
{
	Border mainFrame = Find<Border> ("MainWindowFrame");
	Action syncMainCorners = delegate {
		bool maximized = window.WindowState == WindowState.Maximized;
		mainFrame.CornerRadius = new CornerRadius (maximized ? 0 : 32);
		Find<TextBlock> ("MaximizeWindowGlyph").Text = maximized ? "\ue923" : "\ue922";
		Find<System.Windows.Controls.Button> ("MaximizeWindowButton").ToolTip = maximized ? "还原" : "最大化";
	};
	window.StateChanged += delegate {
		syncMainCorners ();
	};
	syncMainCorners ();
	Button ("MinimizeWindowButton", delegate {
		window.WindowState = WindowState.Minimized;
	});
	Button ("MaximizeWindowButton", delegate {
		window.WindowState = ((window.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	});
	Button ("CloseWindowButton", delegate {
		window.Close ();
	});
	UpdateEngineButtonContent ();
	SetButtonIcon ("HistoryButton", "\ue81c", "历史记录");
	SetButtonIconOnly ("SettingsButton", "\ue713", "设置");
	SetButtonIcon ("VoiceButton", "\ue720", "语音输入");
	SetButtonIcon ("ImageButton", "\ue91b", "翻译图片");
	SetButtonIcon ("CaptureButton", "\ue7b3", "截图翻译");
	SetButtonIcon ("OcrButton", "\ue8a5", "OCR / 框选");
	SetButtonIcon ("ClearButton", "\ue74d", "清空");
	SetButtonIcon ("SpeakButton", "\ue767", "朗读");
	SetButtonIcon ("PauseSpeechButton", "\ue769", "暂停");
	SetButtonIcon ("StopSpeechButton", "\ue71a", "停止");
	SetButtonIcon ("CopyButton", "\ue8c8", "复制");
	SetButtonIcon ("ImageToolsButton", "\ue91b", "译图");
	BindVoiceInput ();
	Button ("TranslateButton", delegate {
		if (busy) {
			Cancel ();
		} else {
			Translate ();
		}
	});
	Button ("SettingsButton", Settings);
	Button ("EngineButton", EngineMenu);
	Button ("HistoryButton", History);
	Button ("ClearButton", Clear);
	Button ("CopyButton", delegate {
		if (rendered != null) {
			System.Windows.Clipboard.SetImage (rendered);
		} else if (output.Text.Length > 0) {
			System.Windows.Clipboard.SetText (output.Text);
		}
		Status ("已复制");
	});
	Button ("SwapButton", delegate {
		if (original != null) {
			Status ("图片模式下请直接选择目标语言。");
		} else {
			string text = Code (source);
			string text2 = Code (target);
			string text3 = output.Text;
			Select (source, text2);
			Select (target, (!(text == "auto")) ? text : ((text2 == "ZH-HANS") ? "EN-US" : "ZH-HANS"));
			string text4 = input.Text;
			input.Text = text3;
			output.Text = text4;
			UpdateResult ();
		}
	});
	Button ("ImageButton", delegate {
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog {
			Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff"
		};
		if (openFileDialog.ShowDialog (window) == true) {
			LoadImage (openFileDialog.FileName);
		}
	});
	Button ("OcrButton", delegate {
		OpenCurrentOcr ();
	});
	Button ("CaptureButton", delegate {
		Capture ();
	});
	Button ("ImageToolsButton", ImageTools);
	BindImagePreview (Find<System.Windows.Controls.Image> ("SourceImage"), () => original, "原文图片");
	BindImagePreview (Find<System.Windows.Controls.Image> ("ResultImage"), () => rendered, "译文图片");
	Button ("SpeakButton", delegate {
		SpeakText (output.Text, Code (target));
	});
	Button ("PauseSpeechButton", delegate {
		speech.TogglePause ();
	});
	Button ("StopSpeechButton", delegate {
		speech.Stop ();
	});
	speech.Changed += UpdateSpeechButtons;
	speech.Failed += delegate(string message) {
		SelectionError ("朗读失败：" + message);
	};
	UpdateSpeechButtons ();
	Find<Grid> ("SourcePane").PreviewDragOver += delegate(object s, System.Windows.DragEventArgs e) {
		e.Effects = (e.Data.GetDataPresent (System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None);
		e.Handled = true;
	};
	Find<Grid> ("SourcePane").Drop += delegate(object s, System.Windows.DragEventArgs e) {
		if (e.Data.GetDataPresent (System.Windows.DataFormats.FileDrop)) {
			string[] array = (string[])e.Data.GetData (System.Windows.DataFormats.FileDrop);
			if (array.Length > 0) {
				LoadImage (array [0]);
			}
		}
	};
	UpdateActionAvailability ();
}


private void BindImagePreview (System.Windows.Controls.Image image, Func<BitmapSource> readImage, string title)
{
	image.Cursor = System.Windows.Input.Cursors.Hand;
	image.ToolTip = "点击打开" + title + "；原文与译文图片可同时查看";
	image.AddHandler (Mouse.PreviewMouseUpEvent, (MouseButtonEventHandler)delegate(object s, MouseButtonEventArgs e) {
		if (e.ChangedButton == MouseButton.Left) {
			BitmapSource bitmapSource = readImage ();
			if (bitmapSource == null) {
				Status ("暂无可查看的" + title);
			} else {
				e.Handled = true;
				ImageWindows.PreviewImage (window, bitmapSource, title);
			}
		}
	}, true);
}


private void UpdateDetectedLanguage ()
{
	TextBlock textBlock = Find<TextBlock> ("SourceHeading");
	if (Code (source) != "auto" || string.IsNullOrWhiteSpace (input.Text)) {
		textBlock.Text = "原文";
	} else {
		textBlock.Text = "原文 · " + LanguageDetector.Detect (input.Text).Name;
	}
}


private void UpdateActionAvailability ()
{
	bool flag = !string.IsNullOrWhiteSpace (input.Text);
	bool flag2 = !string.IsNullOrWhiteSpace (output.Text);
	bool flag3 = flag2 || rendered != null;
	Find<System.Windows.Controls.Button> ("TranslateButton").IsEnabled = busy || flag;
	Find<System.Windows.Controls.Button> ("ClearButton").IsEnabled = flag || flag3 || original != null;
	Find<System.Windows.Controls.Button> ("CopyButton").IsEnabled = flag3;
	Find<System.Windows.Controls.Button> ("SpeakButton").IsEnabled = flag2;
	Find<System.Windows.Controls.Button> ("ImageToolsButton").IsEnabled = original != null;
}

}
}
