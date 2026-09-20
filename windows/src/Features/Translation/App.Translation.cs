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
	if (string.IsNullOrWhiteSpace (Store.Key) && remoteSession == null) {
		Status ("请先注册或登录 Yike 账号，或在设置中填写自己的 DeepL 密钥。");
		ShowSettings (false, "account");
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
	Status (remoteSession != null ? "正在优先使用 Yike 公共体验额度…" : "正在使用你的 DeepL 密钥翻译…");
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
		string publicSource = (from == "auto") ? detected.Code : from;
		TranslationExecutionResult execution = await new TranslationRouter (Store.Key).TranslateProgressive (remoteSession, texts, publicSource, requestSource, to, (doc == null) ? null : text, showLine, ct);
		List<string> result = execution.Values;
		if (execution.UsedPublicQuota) {
			RemoteAccountSessionStore.Save (remoteSession);
			ApplyAccountIdentity ();
		}
		if (execution.PublicSessionRejected) {
			remoteSession = null;
			RemoteAccountSessionStore.Clear ();
			ApplyAccountIdentity ();
		}
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
		string engineName = execution.EngineName;
		Status ("翻译完成 · " + engineName + ((from == "auto") ? (" · 已识别 " + detected.Name) : ""));
		if (ticket == revision && prefs.History) {
			history.Insert (0, new Entry {
				Original = text,
				Result = output.Text,
				Source = from,
				Target = to,
				Engine = engineName,
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
			YikeApiException apiError = ex2 as YikeApiException;
			if (apiError != null && apiError.ErrorCode == "login_required") {
				remoteSession = null;
				RemoteAccountSessionStore.Clear ();
				ApplyAccountIdentity ();
			}
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
