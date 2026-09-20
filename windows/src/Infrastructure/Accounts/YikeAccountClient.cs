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
public sealed class YikeApiException : InvalidOperationException
{
	public int StatusCode { get; private set; }

	public string ErrorCode { get; private set; }

	public YikeApiException (int statusCode, string errorCode, string message) : base (message)
	{
		StatusCode = statusCode;
		ErrorCode = errorCode;
	}
}


public sealed class YikeRegistrationChallenge
{
	public string Id { get; set; }

	public string Message { get; set; }
}


public sealed class YikeAccountClient : IDisposable
{
	public const string BaseUrl = "https://n5v1b.cn/yike-api/";

	private readonly HttpClient client;

	private readonly bool ownsClient;

	public YikeAccountClient (HttpMessageHandler handler = null)
	{
		ownsClient = true;
		client = new HttpClient (handler ?? ProxySettings.CreateHandler (ProxySettings.Current));
		client.BaseAddress = new Uri (BaseUrl);
		client.Timeout = TimeSpan.FromSeconds (25.0);
	}

	public async Task<RemoteAccountSession> Login (string email, string password, CancellationToken ct)
	{
		Dictionary<string, object> result = await Request (HttpMethod.Post, "v2/login", new Dictionary<string, object> {
			{ "email", NormalizeEmail (email) }, { "password", ValidatePassword (password) }
		}, null, ct);
		return ReadSession (result);
	}

	public async Task<YikeRegistrationChallenge> SendRegistration (string email, string password, CancellationToken ct)
	{
		Dictionary<string, object> result = await Request (HttpMethod.Post, "v2/register/send", new Dictionary<string, object> {
			{ "email", NormalizeEmail (email) }, { "password", ValidatePassword (password) }
		}, null, ct);
		string id = ReadString (result, "challenge_id");
		if (string.IsNullOrWhiteSpace (id)) throw new InvalidOperationException ("注册服务返回无效，请稍后重试。");
		return new YikeRegistrationChallenge { Id = id, Message = ReadString (result, "message") };
	}

	public async Task<RemoteAccountSession> VerifyRegistration (string email, string challengeId, string code, CancellationToken ct)
	{
		if (!Regex.IsMatch (code ?? "", "^\\d{6}$")) throw new InvalidOperationException ("请输入邮件中的 6 位验证码。");
		Dictionary<string, object> result = await Request (HttpMethod.Post, "v2/register/verify", new Dictionary<string, object> {
			{ "email", NormalizeEmail (email) }, { "challenge_id", challengeId ?? "" }, { "code", code }
		}, null, ct);
		return ReadSession (result);
	}

	public async Task<RemoteAccountSession> Refresh (RemoteAccountSession session, CancellationToken ct)
	{
		RequireSession (session);
		Dictionary<string, object> result = await Request (HttpMethod.Get, "v1/me", null, session.Token, ct);
		UpdateAccount (session, ReadDictionary (result, "account"));
		return session;
	}

	public async Task Logout (RemoteAccountSession session, CancellationToken ct)
	{
		if (session == null || string.IsNullOrWhiteSpace (session.Token)) return;
		await Request (HttpMethod.Post, "v1/logout", new Dictionary<string, object> (), session.Token, ct);
	}

	public async Task<List<string>> TranslateProgressive (RemoteAccountSession session, IList<string> texts, string source, string target, Action<int, string> progress, CancellationToken ct)
	{
		RequireSession (session);
		string sourceCode = PublicLanguage (source, true);
		string targetCode = PublicLanguage (target, false);
		if (sourceCode == targetCode) throw new InvalidOperationException ("原文和目标语言不能相同。");
		List<string> output = new List<string> ();
		int offset = 0;
		while (offset < texts.Count) {
			List<string> batch = new List<string> ();
			int chars = 0;
			while (offset + batch.Count < texts.Count && batch.Count < 40) {
				string value = texts [offset + batch.Count] ?? "";
				if (string.IsNullOrWhiteSpace (value)) throw new InvalidOperationException ("公共翻译不接受空白段落。");
				if (batch.Count > 0 && chars + value.Length > 5000) break;
				batch.Add (value);
				chars += value.Length;
			}
			Dictionary<string, object> result = await Request (HttpMethod.Post, "v1/translate", new Dictionary<string, object> {
				{ "text", batch }, { "source", sourceCode }, { "target", targetCode }, { "request_id", Guid.NewGuid ().ToString () }
			}, session.Token, ct);
			ArrayList rows = result.ContainsKey ("translations") ? result ["translations"] as ArrayList : null;
			if (rows == null || rows.Count != batch.Count) throw new InvalidOperationException ("公共翻译返回结果不完整。");
			for (int i = 0; i < rows.Count; i++) {
				string translated = rows [i] as string;
				if (string.IsNullOrWhiteSpace (translated)) throw new InvalidOperationException ("公共翻译返回了空结果。");
				translated = ChinesePinyin.Remove (translated, target);
				output.Add (translated);
				if (progress != null) progress (offset + i, translated);
			}
			Dictionary<string, object> account = ReadDictionary (result, "account");
			if (account != null) UpdateAccount (session, account);
			offset += batch.Count;
		}
		return output;
	}

	private async Task<Dictionary<string, object>> Request (HttpMethod method, string path, object body, string token, CancellationToken ct)
	{
		try {
			using (HttpRequestMessage request = new HttpRequestMessage (method, path)) {
				if (!string.IsNullOrWhiteSpace (token)) request.Headers.TryAddWithoutValidation ("Authorization", "Bearer " + token);
				if (body != null) request.Content = new StringContent (Store.Json.Serialize (body), Encoding.UTF8, "application/json");
				using (HttpResponseMessage response = await client.SendAsync (request, ct)) {
					string json = await response.Content.ReadAsStringAsync ();
					Dictionary<string, object> data = null;
					try { data = Store.Json.Deserialize<Dictionary<string, object>> (json); } catch { }
					if (!response.IsSuccessStatusCode) {
						string message = ReadString (data, "message");
						if (string.IsNullOrWhiteSpace (message)) message = "Yike 服务返回错误（" + (int)response.StatusCode + "）。";
						throw new YikeApiException ((int)response.StatusCode, ReadString (data, "code"), message);
					}
					if (data == null) throw new InvalidOperationException ("Yike 服务返回了无效数据。");
					return data;
				}
			}
		} catch (TaskCanceledException) {
			ct.ThrowIfCancellationRequested ();
			throw new TimeoutException ("连接 Yike 服务超时，请稍后重试。");
		} catch (HttpRequestException) {
			throw new InvalidOperationException ("无法连接 Yike 账号服务，请检查网络或代理。");
		}
	}

	private static RemoteAccountSession ReadSession (Dictionary<string, object> data)
	{
		string token = ReadString (data, "token");
		Dictionary<string, object> account = ReadDictionary (data, "account");
		if (string.IsNullOrWhiteSpace (token) || account == null) throw new InvalidOperationException ("登录结果无效，请稍后重试。");
		RemoteAccountSession session = new RemoteAccountSession { Token = token };
		UpdateAccount (session, account);
		return session;
	}

	private static void UpdateAccount (RemoteAccountSession session, Dictionary<string, object> account)
	{
		if (session == null || account == null) return;
		session.Email = ReadString (account, "email");
		session.Username = ReadString (account, "username");
		session.Granted = ReadLong (account, "granted");
		session.Used = ReadLong (account, "used");
		session.Remaining = ReadLong (account, "remaining");
	}

	private static Dictionary<string, object> ReadDictionary (Dictionary<string, object> data, string key)
	{
		if (data == null || !data.ContainsKey (key)) return null;
		return data [key] as Dictionary<string, object>;
	}

	private static string ReadString (Dictionary<string, object> data, string key)
	{
		if (data == null || !data.ContainsKey (key) || data [key] == null) return null;
		return Convert.ToString (data [key], CultureInfo.InvariantCulture);
	}

	private static long ReadLong (Dictionary<string, object> data, string key)
	{
		if (data == null || !data.ContainsKey (key) || data [key] == null) return 0;
		try { return Convert.ToInt64 (data [key], CultureInfo.InvariantCulture); } catch { return 0; }
	}

	private static string NormalizeEmail (string value)
	{
		string email = (value ?? "").Trim ().ToLowerInvariant ();
		try { if (new MailAddress (email).Address != email) throw new FormatException (); }
		catch { throw new InvalidOperationException ("请输入有效的邮箱地址。"); }
		if (email.Length > 254) throw new InvalidOperationException ("邮箱地址过长。");
		return email;
	}

	private static string ValidatePassword (string value)
	{
		if (value == null || value.Length < 10 || value.Length > 128) throw new InvalidOperationException ("密码需为 10 至 128 个字符。");
		return value;
	}

	private static void RequireSession (RemoteAccountSession session)
	{
		if (session == null || string.IsNullOrWhiteSpace (session.Token)) throw new InvalidOperationException ("请先登录 Yike 账号。");
	}

	internal static string PublicLanguage (string code, bool source)
	{
		switch ((code ?? "").ToUpperInvariant ()) {
		case "EN": case "EN-US": return "en";
		case "ZH": case "ZH-HANS": return "zh-CN";
		case "JA": return "ja";
		case "KO": return "ko";
		default:
			throw new InvalidOperationException (source ? "公共翻译的原文仅支持中文、英语、日语和韩语；其他语言请使用自己的 DeepL 密钥。" : "公共翻译的目标语言仅支持中文、英语、日语和韩语；其他语言请使用自己的 DeepL 密钥。");
		}
	}

	public void Dispose ()
	{
		if (ownsClient) client.Dispose ();
	}
}
}
