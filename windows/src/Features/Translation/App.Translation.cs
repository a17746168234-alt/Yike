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
private void UpdateResult ()
{
	Find<FrameworkElement> ("ResultPlaceholder").Visibility = ((output.Text.Length != 0 || rendered != null) ? Visibility.Collapsed : Visibility.Visible);
	UpdateActionAvailability ();
}


private void SetOutputText (string value)
{
	settingOutputText = true;
	try {
		output.Text = value ?? "";
	} finally {
		settingOutputText = false;
	}
	UpdateResult ();
}


private void ClearOutputText ()
{
	SetOutputText ("");
}


private void Cancel ()
{
	revision++;
	if (pending != null) {
		pending.Cancel ();
		pending.Dispose ();
		pending = null;
	}
	busy = false;
	Find<System.Windows.Controls.Button> ("TranslateButton").Content = "开始翻译    Enter";
	UpdateActionAvailability ();
}


private async void Translate ()
{
	if (string.IsNullOrWhiteSpace (input.Text)) {
		return;
	}
	Cancel ();
	int ticket = revision;
	pending = new CancellationTokenSource ();
	CancellationToken ct = pending.Token;
	string text = input.Text;
	string from = Code (source);
	string to = Code (target);
	DetectedLanguage detected = LanguageDetector.Detect (text);
	string requestSource = LanguageDetector.ResolveForDeepL (from, text);
	OcrDocument doc = document;
	busy = true;
	Find<System.Windows.Controls.Button> ("TranslateButton").Content = "取消";
	UpdateActionAvailability ();
	Status ("正在使用 DeepL 翻译…");
	try {
		TranslationPlan plan = ((doc == null) ? TranslationPlan.Create (text) : null);
		List<string> texts = ((doc == null) ? plan.Units : doc.Regions.Select ((Region r) => r.Text).ToList ());
		string[] displayed = new string[texts.Count];
		ClearOutputText ();
		Action<int, string> showLine = delegate(int index, string value) {
			window.Dispatcher.BeginInvoke ((Action)async delegate {
				int num2 = default(int);
				int num3 = num2;
				int num4 = 0;
				try {
					await Task.Delay (Math.Min (360, index * 35), ct);
					if (ticket == revision) {
						displayed [index] = value;
						SetOutputText ((doc == null) ? plan.Compose (displayed) : string.Join (Environment.NewLine, displayed.Select ((string x) => x ?? "")));
						if (doc != null) {
							doc.Regions [index].Translated = value;
							Render ();
						}
						UpdateResult ();
						Status ("正在翻译 · 已完成 " + displayed.Count ((string x) => x != null) + " / " + displayed.Length + " 行");
					}
				} catch (OperationCanceledException) {
				}
			});
		};
		List<string> result = await new DeepL (Store.Key).TranslateProgressive (texts, requestSource, to, (doc == null) ? null : text, showLine, ct);
		if (ticket != revision) {
			return;
		}
		await Task.Delay (Math.Min (460, texts.Count * 35 + 45), ct);
		if (ticket != revision) {
			return;
		}
		SetOutputText ((doc == null) ? plan.Compose (result) : string.Join (Environment.NewLine, result));
		if (doc != null) {
			for (int num = 0; num < result.Count; num++) {
				doc.Regions [num].Translated = result [num];
			}
			Render ();
			if (prefs.ImageHistory) {
				SaveImageHistory ();
			}
		}
		UpdateResult ();
		Status ("翻译完成 · DeepL" + ((from == "auto") ? (" · 已识别 " + detected.Name) : ""));
		if (ticket == revision && prefs.History) {
			history.Insert (0, new Entry {
				Original = text,
				Result = output.Text,
				Source = from,
				Target = to,
				Engine = "DeepL",
				Date = DateTime.Now
			});
			Store.Write ("history.json", history);
		}
	} catch (OperationCanceledException) {
		if (ticket == revision) {
			Status ("翻译已取消");
		}
	} catch (Exception ex2) {
		if (ticket == revision) {
			Status ("翻译失败：" + ex2.Message);
		}
	} finally {
		if (ticket == revision) {
			busy = false;
			Find<System.Windows.Controls.Button> ("TranslateButton").Content = "开始翻译    Enter";
			UpdateActionAvailability ();
		}
	}
}

}
}
