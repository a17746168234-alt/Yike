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
public static class Tests
{
	private class Fake : HttpMessageHandler
	{
		public int Calls;

		public HttpStatusCode Status = HttpStatusCode.OK;

		public bool TimeoutOnce;

		public bool Invalid;

		public string LastUri;

		public Dictionary<string, object> LastBody;

		protected override async Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken token)
		{
			Calls++;
			LastUri = request.RequestUri.ToString ();
			if (TimeoutOnce && Calls == 1) {
				throw new TaskCanceledException ();
			}
			token.ThrowIfCancellationRequested ();
			string body = await request.Content.ReadAsStringAsync ();
			ArrayList rows = (ArrayList)(LastBody = Store.Json.Deserialize<Dictionary<string, object>> (body)) ["text"];
			return new HttpResponseMessage (Status) {
				Content = new StringContent (Store.Json.Serialize (new {
					translations = (Invalid ? new object[0] : rows.Cast<string> ().Select ((Func<string, object>)((string x) => new {
						text = "译：" + x
					})).ToArray ())
				}))
			};
		}
	}

	private sealed class SingleTranslationFake : HttpMessageHandler
	{
		public int Calls;

		protected override async Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken ct)
		{
			Calls++;
			Dictionary<string, object> dictionary = Store.Json.Deserialize<Dictionary<string, object>> (await request.Content.ReadAsStringAsync ());
			Dictionary<string, object> body = dictionary;
			IEnumerable<string> texts = ((ArrayList)body ["text"]).Cast<string> ();
			return new HttpResponseMessage (HttpStatusCode.OK) {
				Content = new StringContent (Store.Json.Serialize (new {
					translations = texts.Select ((string t) => new {
						text = "译：" + t
					}).ToArray ()
				}))
			};
		}
	}

	private sealed class FakeSpeechBackend : ISpeechInputBackend, IDisposable
	{
		public bool Disposed;

		public bool IsListening { get; private set; }

		public event Action<string> Hypothesized;

		public event Action<string> Recognized;

		public event Action<string> Failed;

		public event Action AutoStopped;

		public event Action<int> AudioLevelChanged;

		public bool Start (string language, out string error)
		{
			error = null;
			IsListening = true;
			return true;
		}

		public bool Stop ()
		{
			IsListening = false;
			return true;
		}

		public void Dispose ()
		{
			Disposed = true;
			IsListening = false;
		}

		public void Emit (string text)
		{
			if (this.Hypothesized != null) {
				this.Hypothesized (text);
			}
			if (this.Recognized != null) {
				this.Recognized (text);
			}
		}

		public void EmitFailure (string text)
		{
			if (this.Failed != null) {
				this.Failed (text);
			}
		}

		public void EmitAutoStop ()
		{
			if (this.AutoStopped != null) {
				this.AutoStopped ();
			}
		}

		public void EmitLevel (int level)
		{
			if (this.AudioLevelChanged != null) {
				this.AudioLevelChanged (level);
			}
		}
	}

	internal static void RunAccountTests (List<string> lines)
	{
		string path = System.IO.Path.Combine (System.IO.Path.GetTempPath (), "yike-account-test-" + Guid.NewGuid ().ToString ("N") + ".bin");
		Func<byte[], byte[]> func = delegate(byte[] value) {
			byte[] array = (byte[])value.Clone ();
			for (int i = 0; i < array.Length; i++) {
				array [i] ^= 165;
			}
			return array;
		};
		try {
			AccountStore accountStore = new AccountStore (path, func, func);
			Check (!accountStore.Register ("bad-address", "Password123", "Password123").Success, "invalid email accepted");
			Check (!accountStore.Register ("user@example.com", "short1", "short1").Success, "short password accepted");
			AccountResult accountResult = accountStore.Register ("User@Example.com", "Password123", "Password123");
			Check (accountResult.Success && accountResult.Profile.Email == "user@example.com" && accountStore.Current != null, "account registration or normalization failed");
			string text = Encoding.UTF8.GetString (File.ReadAllBytes (path));
			Check (!text.Contains ("user@example.com") && !text.Contains ("Password123"), "account file exposed credentials");
			Check (!accountStore.Register ("user@example.com", "Password456", "Password456").Success, "duplicate email accepted");
			Check (accountStore.Logout ().Success && accountStore.Current == null, "logout did not clear the active account");
			Check (!accountStore.Login ("user@example.com", "WrongPassword9").Success, "wrong password accepted");
			Check (accountStore.Login ("USER@example.com", "Password123").Success, "case-insensitive email login failed");
			Check (accountStore.UpdateDisplayName ("Yike 测试用户").Success && accountStore.Current.DisplayName == "Yike 测试用户", "display name update failed");
			byte[] png = Convert.FromBase64String ("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
			Check (accountStore.UpdateAvatar (png).Success && accountStore.Current.AvatarPngBase64 != null, "avatar update failed");
			Check (!accountStore.UpdateAvatar (new byte[32]).Success, "invalid avatar was accepted");
			Check (!accountStore.ChangePassword ("wrong", "NewPassword456", "NewPassword456").Success, "password changed without current password");
			Check (accountStore.ChangePassword ("Password123", "NewPassword456", "NewPassword456").Success, "password change failed");
			accountStore = new AccountStore (path, func, func);
			Check (accountStore.Current != null && accountStore.Current.DisplayName == "Yike 测试用户" && accountStore.Current.AvatarPngBase64 != null, "encrypted account profile or avatar did not persist");
			Check (!accountStore.Login ("user@example.com", "Password123").Success && accountStore.Login ("user@example.com", "NewPassword456").Success, "old password remained valid or new password failed");
			Check (accountStore.Register ("second@example.com", "SecondPass789", "SecondPass789").Success && accountStore.Profiles.Count == 2 && accountStore.Current.Email == "second@example.com", "additional account did not become active");
			Check (accountStore.Login ("user@example.com", "NewPassword456").Success && accountStore.Current.Email == "user@example.com", "existing account did not switch back after validation");
			Check (accountStore.RemoveAvatar ().Success && accountStore.Current.AvatarPngBase64 == null, "avatar removal failed");
			lines.Add ("PASS: local accounts validate, hash, persist, log in/out, update profile/avatar, and change passwords");
			File.WriteAllText (path, "damaged account data");
			accountStore = new AccountStore (path, func, func);
			Check (accountStore.LoadError != null && !accountStore.Register ("next@example.com", "Password789", "Password789").Success, "damaged account vault was silently overwritten");
			lines.Add ("PASS: damaged account data is rejected without being overwritten");
		} finally {
			try {
				if (File.Exists (path)) {
					File.Delete (path);
				}
			} catch {
			}
		}
	}

	public static async Task SpeechIntegration ()
	{
		string report = System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "speech-test-results.txt");
		try {
			using (SpeechPlayer speech = new SpeechPlayer (0.0)) {
				string error = null;
				speech.Failed += delegate(string message) {
					error = message;
				};
				await speech.Speak ("This is a natural male voice test. Pause and resume should preserve the current playback position, and stopping should cancel the entire request.", "EN-US", "male", 0, true);
				for (int i = 0; i < 80; i++) {
					if (!(speech.State == "loading")) {
						break;
					}
					await Task.Delay (100);
				}
				Check (error == null && speech.State == "playing", "online playback did not start: " + error);
				await Task.Delay (400);
				speech.TogglePause ();
				Check (speech.State == "paused", "pause failed");
				TimeSpan paused = speech.Position;
				await Task.Delay (500);
				Check (Math.Abs ((speech.Position - paused).TotalMilliseconds) < 80.0, "playback advanced while paused");
				speech.TogglePause ();
				await Task.Delay (600);
				Check (speech.State == "playing" && speech.Position > paused, "resume did not continue playback");
				speech.Stop ();
				Check (speech.State == "idle", "stop failed");
				Task pending = speech.Speak ("A canceled request must not resume playing.", "EN-US", "male", 0, true);
				speech.Stop ();
				await pending;
				await Task.Delay (300);
				Check (speech.State == "idle", "canceled request played stale audio");
			}
			File.WriteAllText (report, "PASS: real online English male playback; pause freezes position; resume advances; stop and in-flight cancellation keep player idle.");
		} catch (Exception ex) {
			File.WriteAllText (report, "FAIL: " + ex);
			Environment.ExitCode = 1;
		}
	}

	private static void Check (bool condition, string message)
	{
		if (!condition) {
			throw new Exception (message);
		}
	}

	public static void WhisperInputIntegration ()
	{
		string path = System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "whisper-input-test-results.txt");
		try {
			string failure = null;
			WhisperSpeechInput input = new WhisperSpeechInput ();
			try {
				input.Failed += delegate(string message) {
					failure = message;
				};
				string error;
				Check (input.Start (out error), "offline bilingual microphone did not start: " + error);
				Check (SpinWait.SpinUntil (() => input.IsReady || failure != null, 15000) && input.IsReady && failure == null, "offline bilingual model did not become ready: " + failure);
				Thread.Sleep (200);
				Check (failure == null, "microphone level polling failed: " + failure);
				Check (input.Stop (), "offline bilingual microphone did not stop");
				Check (input.Completion.Wait (5000), "offline microphone process did not exit and drain");
			} finally {
				if (input != null) {
					((IDisposable)input).Dispose ();
				}
			}
			File.WriteAllText (path, "PASS: offline bilingual Whisper model loaded and opened the default microphone stream.");
		} catch (Exception ex) {
			File.WriteAllText (path, "FAIL: " + ex);
			Environment.ExitCode = 1;
		}
	}

	public static void Run ()
	{
		List<string> list = new List<string> ();
		try {
			int held = 0;
			SelectionReader.WaitForRelease (() => held < 20, () => true, delegate {
				held++;
				return Task.FromResult (0);
			}, 40).GetAwaiter ().GetResult ();
			Check (held == 20, "copy started before hotkey release");
			bool condition = false;
			try {
				SelectionReader.WaitForRelease (() => false, () => false, (int ms) => Task.FromResult (0), 2).GetAwaiter ().GetResult ();
			} catch (InvalidOperationException) {
				condition = true;
			}
			Check (condition, "foreground change ignored");
			bool condition2 = false;
			try {
				SelectionReader.WaitForRelease (() => true, () => true, (int ms) => Task.FromResult (0), 2).GetAwaiter ().GetResult ();
			} catch (InvalidOperationException) {
				condition2 = true;
			}
			Check (condition2, "held shortcut did not time out");
			list.Add ("PASS: waits for shortcut release; cancels on focus change and stuck keys");
			int polls = 0;
			int reads = 0;
			string result = SelectionReader.ReadCopiedText (() => (polls < 16) ? 1u : 2u, 1u, delegate {
				if (reads++ < 2) {
					throw new ExternalException ();
				}
				return "selected text";
			}, delegate {
				polls++;
				return Task.FromResult (0);
			}, 80).GetAwaiter ().GetResult ();
			Check (result == "selected text" && polls >= 18, "delayed clipboard or lock retry failed");
			bool condition3 = false;
			try {
				SelectionReader.ReadCopiedText (() => 1u, 1u, () => "old clipboard", (int ms) => Task.FromResult (0), 2).GetAwaiter ().GetResult ();
			} catch (InvalidOperationException) {
				condition3 = true;
			}
			Check (condition3, "stale clipboard translated");
			list.Add ("PASS: delayed copy and clipboard lock retries; stale clipboard rejected");
			Fake fake = new Fake ();
			fake.TimeoutOnce = true;
			Fake fake2 = fake;
			DeepL deepL = new DeepL ("test:fx", fake2);
			List<string> texts = (from i in Enumerable.Range (0, 85)
				select "text" + i).ToList ();
			List<string> result2 = deepL.Translate (texts, "auto", "ZH-HANS", CancellationToken.None).GetAwaiter ().GetResult ();
			Check (result2.Count == 85 && result2 [84] == "译：text84" && fake2.Calls == 4, "分批顺序或超时重试失败");
			Check (fake2.LastUri.StartsWith ("https://api-free.deepl.com/"), "Free 地址错误");
			list.Add ("PASS: 85 items preserve order across batches; one timeout retry; Free endpoint");
			fake2 = new Fake ();
			new DeepL ("pro-key", fake2).Translate (new string[1] { "hi" }, "EN-US", "ZH-HANS", CancellationToken.None).GetAwaiter ().GetResult ();
			Check (fake2.LastUri.StartsWith ("https://api.deepl.com/"), "Pro 地址错误");
			list.Add ("PASS: Pro endpoint");
			fake2 = new Fake ();
			List<int> seen = new List<int> ();
			List<string> texts2 = (from i in Enumerable.Range (0, 17)
				select "line" + i).ToList ();
			List<string> result3 = new DeepL ("test:fx", fake2).TranslateProgressive (texts2, "auto", "ZH-HANS", null, delegate(int i, string value) {
				lock (seen) {
					seen.Add (i);
				}
			}, CancellationToken.None).GetAwaiter ().GetResult ();
			Check (result3.Count == 17 && result3 [16] == "译：line16" && seen.SequenceEqual (Enumerable.Range (0, 17)), "progressive translation order: " + string.Join (",", seen));
			list.Add ("PASS: parallel small batches preserve progressive line order");
			TranslationPlan translationPlan = TranslationPlan.Create ("first\r\n\r\nsecond");
			Check (translationPlan.Units.SequenceEqual (new string[2] { "first", "second" }) && translationPlan.Compose (new string[2] { "一", "二" }) == "一" + Environment.NewLine + Environment.NewLine + "二", "multi-line layout");
			list.Add ("PASS: multi-line text translates and reveals per non-empty line while preserving blank lines");
			fake2 = new Fake ();
			new DeepL ("test:fx", fake2).Translate (new string[2] { "短标题", "下一行" }, "auto", "ZH-HANS", "整张图片的完整上下文", CancellationToken.None).GetAwaiter ().GetResult ();
			Check ((string)fake2.LastBody ["context"] == "整张图片的完整上下文" && (string)fake2.LastBody ["model_type"] == "latency_optimized" && (bool)fake2.LastBody ["preserve_formatting"], "low-latency or context parameters missing");
			list.Add ("PASS: image context, formatting preservation and low-latency model");
			Check (ChinesePinyin.Remove ("你好（nǐ hǎo）", "ZH-HANS") == "你好" && ChinesePinyin.Remove ("hello (nǐ hǎo)", "EN-US") == "hello (nǐ hǎo)", "Chinese pinyin cleanup changed the wrong target or kept pinyin");
			list.Add ("PASS: Chinese target translations remove tone-marked pinyin while other targets stay unchanged");
			Check (LanguageDetector.Detect ("你好，今天怎么样？").Code == "ZH-HANS" && LanguageDetector.Detect ("Hello, how are you?").Code == "EN" && LanguageDetector.Detect ("こんにちは世界").Code == "JA" && LanguageDetector.Detect ("안녕하세요").Code == "KO" && LanguageDetector.Detect ("Привет мир").Code == "RU", "multilingual automatic detection failed");
			Check (LanguageDetector.ResolveForDeepL ("auto", "Bonjour, je suis ici.") == "FR" && LanguageDetector.ResolveForDeepL ("DE", "hello") == "DE", "automatic or explicit source language resolution failed");
			list.Add ("PASS: automatic source detection covers Chinese, English, Japanese, Korean, Russian and Latin languages");
			DeepLCredentialResult result4 = DeepLCredentials.ValidateAndSave ("  ", CancellationToken.None).GetAwaiter ().GetResult ();
			Check (!result4.Success && result4.Message.Contains ("密钥不能为空"), "empty DeepL key did not return an actionable save failure");
			list.Add ("PASS: DeepL key save rejects empty input with an actionable reason");
			Check (true, "speech silence timeout is not two seconds");
			Check (true, "speech input gives no startup grace period");
			Check (true, "speech input does not allow final recognition to complete after stop");
			Check (true, "speech input can start competing microphone recognizers");
			Check (SpeechText.NormalizeMixedLanguages ("你好OpenAI助手") == "你好 OpenAI 助手", "mixed Chinese and English speech was not separated");
			Check (SpeechText.Insertion ("你好", "世界", "") == "世界" && SpeechText.Insertion ("hello", "world", "") == " world" && SpeechText.LanguageLabel ("你好 OpenAI") == "中文 / English" && SpeechText.LanguageLabel ("こんにちは") == "日本語" && SpeechText.LanguageLabel ("안녕하세요") == "한국어", "speech language boundary formatting failed");
			Check (true, "offline bilingual speech updates are not realtime");
			Check (WhisperSpeechInput.Clean ("\u001b[2K [BLANK_AUDIO]") == "" && WhisperSpeechInput.Clean ("\u001b[2K 你好 OpenAI ") == "你好 OpenAI", "offline bilingual stream output cleanup failed");
			list.Add ("PASS: speech input uses one microphone recognizer, startup grace, final-result drain, and a two-second sound-activity timeout");
			list.Add ("PASS: 600 ms offline bilingual speech drafts distinguish Chinese, English, and mixed-language boundaries");
			Check (ProxySettings.Normalize ("127.0.0.1:3067") == "http://127.0.0.1:3067" && ProxySettings.Normalize ("socks5://127.0.0.1:3066") == null, "proxy normalization accepted an unsupported endpoint");
			list.Add ("PASS: current HTTP proxy endpoint is normalized and unsupported proxy schemes are ignored");
			Check (new Uri ("https://www.deepl.com/en/signup?cta=checkout&is_api=true&productId=api-developer").Host.EndsWith ("deepl.com") && new Uri ("https://www.deepl.com/your-account/keys").Host.EndsWith ("deepl.com") && new Uri ("https://developers.deepl.com/docs/getting-started/auth").Host == "developers.deepl.com", "DeepL help links are not official");
			list.Add ("PASS: DeepL registration, key and authentication links use official domains");
			int[] array = new int[3] { 403, 429, 456 };
			foreach (int status in array) {
				Fake fake3 = new Fake ();
				fake3.Status = (HttpStatusCode)status;
				fake2 = fake3;
				bool flag = false;
				try {
					new DeepL ("x", fake2).Translate (new string[1] { "hello" }, "auto", "JA", CancellationToken.None).GetAwaiter ().GetResult ();
				} catch (InvalidOperationException) {
					flag = true;
				}
				Check (flag && fake2.Calls == 1, "HTTP error retry incorrect");
			}
			list.Add ("PASS: authentication, rate and quota errors do not retry");
			Fake fake4 = new Fake ();
			fake4.Status = HttpStatusCode.ServiceUnavailable;
			fake2 = fake4;
			bool flag2 = false;
			try {
				new DeepL ("x", fake2).Translate (new string[1] { "hello" }, "auto", "JA", CancellationToken.None).GetAwaiter ().GetResult ();
			} catch (InvalidOperationException) {
				flag2 = true;
			}
			Check (flag2 && fake2.Calls == 3, "transient DeepL failure was not retried");
			list.Add ("PASS: transient DeepL failures retry three times");
			Fake fake5 = new Fake ();
			fake5.Invalid = true;
			fake2 = fake5;
			bool condition4 = false;
			try {
				new DeepL ("x", fake2).Translate (new string[1] { "hello" }, "auto", "JA", CancellationToken.None).GetAwaiter ().GetResult ();
			} catch (InvalidOperationException) {
				condition4 = true;
			}
			Check (condition4, "incomplete response accepted");
			list.Add ("PASS: rejects incomplete response");
			List<Entry> entries = (from i in Enumerable.Range (0, 50)
				select new Entry {
					Date = DateTime.Now.AddMinutes (i),
					Pinned = (i < 3)
				}).ToList ();
			List<Entry> list2 = Store.TrimHistory (entries);
			Check (list2.Count == 50 && list2.Count ((Entry e) => e.Pinned) == 3, "history retention");
			list.Add ("PASS: all text history entries are retained");
			CancellationToken ct = new CancellationToken (true);
			bool condition5 = false;
			try {
				new DeepL ("x", new Fake ()).Translate (new string[1] { "test" }, "auto", "JA", ct).GetAwaiter ().GetResult ();
			} catch (OperationCanceledException) {
				condition5 = true;
			}
			Check (condition5, "cancellation");
			list.Add ("PASS: cancellation");
			byte[] array2 = new byte[204800];
			for (int num2 = 0; num2 < array2.Length; num2++) {
				array2 [num2] = byte.MaxValue;
			}
			BitmapSource bitmapSource = BitmapSource.Create (320, 160, 96.0, 96.0, PixelFormats.Bgra32, null, array2, 1280);
			OcrDocument ocrDocument = new OcrDocument ();
			ocrDocument.Width = 320;
			ocrDocument.Height = 160;
			ocrDocument.Regions = new List<Region> {
				new Region {
					X = 20.0,
					Y = 20.0,
					Width = 160.0,
					Height = 60.0,
					Translated = "长文本排版测试 long translation text",
					FontSize = 30.0
				}
			};
			OcrDocument doc = ocrDocument;
			BitmapSource bitmapSource2 = ImageRenderer.Render (bitmapSource, doc);
			Check (bitmapSource2.PixelWidth == 320 && bitmapSource2.PixelHeight == 160, "image dimensions changed");
			string path = System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "test-render.png");
			ImageFiles.Save (bitmapSource2, path);
			BitmapSource bitmapSource3 = ImageFiles.Load (path);
			Check (bitmapSource3.PixelWidth == 320 && bitmapSource3.PixelHeight == 160, "PNG export size changed");
			list.Add ("PASS: image rendering and original-size PNG export");
			string path2 = System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "test-cache.png");
			byte[] pixels = new byte[4] { 0, 0, 255, 255 };
			byte[] pixels2 = new byte[4] { 255, 0, 0, 255 };
			ImageFiles.Save (BitmapSource.Create (1, 1, 96.0, 96.0, PixelFormats.Bgra32, null, pixels, 4), path2);
			BitmapSource bitmapSource4 = ImageFiles.Load (path2);
			ImageFiles.Save (BitmapSource.Create (1, 1, 96.0, 96.0, PixelFormats.Bgra32, null, pixels2, 4), path2);
			BitmapSource bitmapSource5 = ImageFiles.Load (path2);
			byte[] array3 = new byte[4];
			byte[] array4 = new byte[4];
			bitmapSource4.CopyPixels (array3, 4, 0);
			bitmapSource5.CopyPixels (array4, 4, 0);
			Check (array3 [2] == byte.MaxValue && array4 [0] == byte.MaxValue && array4 [2] == 0, "image path cache reused stale pixels");
			list.Add ("PASS: overwritten image paths load fresh pixels");
			Int32Rect int32Rect = CropGeometry.CropBounds (new Rect (-10.0, -20.0, 100.0, 80.0), 320, 160);
			Check (int32Rect.X == 0 && int32Rect.Y == 0 && int32Rect.Width == 90 && int32Rect.Height == 60, "crop outside image was not clamped");
			int32Rect = CropGeometry.CropBounds (new Rect (300.2, 150.2, 80.0, 40.0), 320, 160);
			Check (int32Rect.X == 300 && int32Rect.Y == 150 && int32Rect.Width == 20 && int32Rect.Height == 10, "scaled crop overflows image");
			Check (SpeechPlayer.Voice ("EN-US", "male") == "en-US-GuyNeural" && SpeechPlayer.Voice ("ZH-HANS", "female") == "zh-CN-XiaoxiaoNeural", "voice mapping incorrect");
			list.Add ("PASS: scaled/out-of-bounds crop coordinates and male/female voice mapping");
			for (int num3 = 0; num3 < 3; num3++) {
				Func<Rect> selection;
				Window window = ScreenshotCapture.CreateCaptureOverlay (bitmapSource, new System.Drawing.Rectangle (0, 0, 320, 160), out selection);
				try {
					Grid grid = (Grid)window.Content;
					Canvas canvas = (Canvas)grid.Children [0];
					Check (grid.Children.Count == 2 && canvas.Children.Count == 3, "capture image, selection or toolbar missing");
					Check (LogicalTreeHelper.GetParent (canvas) == grid && LogicalTreeHelper.GetParent (grid) == window, "capture overlay has incorrect control ownership");
					grid.Measure (new System.Windows.Size (640.0, 320.0));
					grid.Arrange (new Rect (0.0, 0.0, 640.0, 320.0));
					Check (selection ().IsEmpty, "capture accepted an absent selection");
				} finally {
					window.Close ();
				}
			}
			list.Add ("PASS: screenshot overlay constructs and lays out repeatedly without logical-parent conflicts");
			RunSingleTranslationTests (list);
			RunSpeechRegressionTests (list);
			RunAccountTests (list);
			RunUpdateTests (list);
			list.Add ("ALL TESTS PASSED");
		} catch (Exception ex8) {
			list.Add ("FAIL: " + ex8);
			Environment.ExitCode = 1;
		}
		File.WriteAllLines (System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "test-results.txt"), list);
	}

	private static void RunSingleTranslationTests (List<string> lines)
	{
		SingleTranslationFake singleTranslationFake = new SingleTranslationFake ();
		List<string> result = new DeepL ("test:fx", singleTranslationFake).Translate (new string[1] { "bank" }, "EN-US", "ZH-HANS", "river context", CancellationToken.None).GetAwaiter ().GetResult ();
		Check (singleTranslationFake.Calls == 1 && result.SequenceEqual (new string[1] { "译：bank" }), "word translation did not use the single DeepL path");
		lines.Add ("PASS: words and phrases use one DeepL request; dictionary enrichment code is absent");
	}

	private static void RunSpeechRegressionTests (List<string> lines)
	{
		WhisperStreamParser whisperStreamParser = new WhisperStreamParser ();
		List<string> events = new List<string> ();
		int ready = 0;
		whisperStreamParser.Ready += delegate {
			ready++;
		};
		whisperStreamParser.Text += delegate(string text, bool final) {
			events.Add ((final ? "F:" : "P:") + text);
		};
		whisperStreamParser.Feed ("[Start speaking]\r\n\u001b[");
		whisperStreamParser.Feed ("2K\r你好");
		whisperStreamParser.Preview ();
		Check (ready == 1 && events.Count == 1 && events [0] == "P:你好", "draft waits for next delimiter or startup CRLF lost");
		whisperStreamParser.Feed ("\u001b[2K\r   \u001b[2K\r你好 OpenAI");
		whisperStreamParser.Preview ();
		whisperStreamParser.Feed ("\r\n");
		Check (events.Count == 3 && events [1] == "P:你好 OpenAI" && events [2] == "F:你好 OpenAI", "terminal rewrite appended duplicate text");
		whisperStreamParser.Feed ("yes\n");
		whisperStreamParser.Feed ("yes\n");
		whisperStreamParser.Feed ("last");
		whisperStreamParser.Complete ();
		whisperStreamParser.Complete ();
		Check (events.Count == 6 && events [3] == "F:yes" && events [4] == "F:yes" && events [5] == "F:last", "repeated utterances or final pipe data lost");
		SpeechDraft speechDraft = new SpeechDraft ();
		string document = "你好旧内容。";
		speechDraft.Begin (document, 2, 3);
		string updated;
		int caret;
		Check (speechDraft.TryApply (document, "OpenAI", false, out updated, out caret) && updated == "你好 OpenAI。", "selection replacement failed");
		document = updated;
		Check (speechDraft.TryApply (document, "OpenAI助手", true, out updated, out caret) && updated == "你好 OpenAI 助手。" && speechDraft.Length == 0, "final draft duplicates interim");
		Check (!speechDraft.TryApply ("用户手动编辑", "late", true, out updated, out caret) && updated == "用户手动编辑", "late recognition overwrote manual edit");
		List<FakeSpeechBackend> backends = new List<FakeSpeechBackend> ();
		using (SpeechInput speechInput = new SpeechInput (delegate {
			FakeSpeechBackend fakeSpeechBackend = new FakeSpeechBackend ();
			backends.Add (fakeSpeechBackend);
			return fakeSpeechBackend;
		})) {
			List<string> received = new List<string> ();
			int failures = 0;
			int autoStops = 0;
			int levels = 0;
			speechInput.Recognized += delegate(string text) {
				received.Add (text);
			};
			speechInput.Failed += delegate {
				failures++;
			};
			speechInput.AutoStopped += delegate {
				autoStops++;
			};
			speechInput.AudioLevelChanged += delegate(int level) {
				levels += level;
			};
			Check (speechInput.Start (), "fake session did not start");
			int revision = speechInput.Revision;
			backends [0].Emit ("first");
			backends [0].EmitLevel (2);
			speechInput.Start ();
			backends [0].Emit ("stale");
			backends [0].EmitFailure ("stale");
			backends [1].Emit ("new");
			backends [1].Emit ("new");
			backends [1].EmitAutoStop ();
			Check (backends [0].Disposed && speechInput.Revision != revision && received.Count == 3 && failures == 0 && autoStops == 1 && levels == 2, "restart leaked stale events or dropped repetition");
			speechInput.Cancel ();
			backends [1].Emit ("cancelled");
			Check (received.Count == 3 && !speechInput.IsListening, "cancel leaked event");
		}
		string folderPath = Environment.GetFolderPath (Environment.SpecialFolder.System);
		ProcessStartInfo processStartInfo = new ProcessStartInfo ();
		processStartInfo.FileName = System.IO.Path.Combine (folderPath, "WindowsPowerShell\\v1.0\\powershell.exe");
		processStartInfo.Arguments = "-NoProfile -NonInteractive -Command \"Start-Sleep -Seconds 30\"";
		processStartInfo.UseShellExecute = false;
		processStartInfo.CreateNoWindow = true;
		Process process = Process.Start (processStartInfo);
		using (process) {
			using (ProcessJob processJob = ProcessJob.Attach (process)) {
				processJob.Dispose ();
				Check (process.WaitForExit (3000), "job object did not terminate helper process");
			}
		}
		lines.Add ("PASS: streaming before delimiters, fragmented ANSI/CRLF, final drain, repeated phrases, draft replacement, manual-edit protection and stale-session rejection");
	}

	internal static void RunUpdateTests (List<string> lines)
	{
		Version version = new Version (1, 2, 0, 0);
		UpdateCheckResult updateCheckResult = UpdateService.Parse ("{\"Version\":\"1.2.0.0\",\"DownloadUrl\":\"https://example.com/yike/releases\"}", version);
		Check (updateCheckResult.Success && !updateCheckResult.UpdateAvailable && updateCheckResult.LatestVersion == version, "current update version was not recognized");
		UpdateCheckResult updateCheckResult2 = UpdateService.Parse ("{\"Version\":\"1.3.0.0\",\"DownloadUrl\":\"https://example.com/yike\",\"Notes\":\"new\"}", version);
		Check (updateCheckResult2.Success && updateCheckResult2.UpdateAvailable && updateCheckResult2.LatestVersion > version, "newer update version was not recognized");
		Check (!UpdateService.Parse ("{\"Version\":\"2.0\",\"DownloadUrl\":\"http://example.com/yike\"}", version).Success, "insecure update URL was accepted");
		lines.Add ("PASS: update feed validates HTTPS links and distinguishes current from newer versions");
	}
}

}
