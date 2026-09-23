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
public sealed class TranslationExecutionResult
{
	public List<string> Values { get; set; }

	public string EngineName { get; set; }

	public bool UsedPublicQuota { get; set; }

	public bool UsedPersonalFallback { get; set; }

	public bool PublicSessionRejected { get; set; }
}


public static class TranslationEngines
{
	public const string Public = "public";

	public const string Personal = "personal";

	public static string Normalize (string value)
	{
		if (string.Equals (value, Public, StringComparison.OrdinalIgnoreCase)) return Public;
		if (string.Equals (value, Personal, StringComparison.OrdinalIgnoreCase)) return Personal;
		return "";
	}
}


public sealed class TranslationRouter
{
	private readonly string personalKey;

	private readonly Func<YikeAccountClient> publicClientFactory;

	private readonly Func<string, DeepL> personalClientFactory;

	public TranslationRouter (string personalKey, Func<YikeAccountClient> publicClientFactory = null, Func<string, DeepL> personalClientFactory = null)
	{
		this.personalKey = (personalKey ?? "").Trim ();
		this.publicClientFactory = publicClientFactory ?? (() => new YikeAccountClient ());
		this.personalClientFactory = personalClientFactory ?? (key => new DeepL (key));
	}

	public async Task<TranslationExecutionResult> TranslateProgressive (string engine, RemoteAccountSession session, IList<string> texts, string publicSource, string deepLSource, string target, string context, Action<int, string> progress, CancellationToken ct)
	{
		engine = TranslationEngines.Normalize (engine);
		if (engine == TranslationEngines.Public) {
			if (session == null) throw new InvalidOperationException ("当前选择的是赠送额度，请先注册或登录 Yike 账号。");
			if (!SupportsPublicPair (publicSource, target)) throw new InvalidOperationException ("DeepL 高质量翻译的赠送额度目前仅支持中文、英语、日语和韩语，请在顶部切换为 DeepL（个人接入）。");
			List<string> values;
			using (YikeAccountClient client = publicClientFactory ()) values = await client.TranslateProgressive (session, texts, publicSource, target, progress, ct);
			return new TranslationExecutionResult {
				Values = values, EngineName = "DeepL 高质量翻译", UsedPublicQuota = true
			};
		}
		if (engine == TranslationEngines.Personal) {
			if (string.IsNullOrWhiteSpace (personalKey)) throw new InvalidOperationException ("当前选择的是个人接入，请先在“了解与帮助”中填写自己的 DeepL 密钥。");
			List<string> values = await personalClientFactory (personalKey).TranslateProgressive (texts, deepLSource, target, context, progress, ct);
			return new TranslationExecutionResult {
				Values = values,
				EngineName = "DeepL（个人接入）"
			};
		}
		throw new InvalidOperationException ("请先在主界面顶部的 DeepL 菜单中选择使用赠送额度或自己的密钥。");
	}

	internal static bool SupportsPublicPair (string source, string target)
	{
		try {
			return YikeAccountClient.PublicLanguage (source, true) != YikeAccountClient.PublicLanguage (target, false);
		} catch {
			return false;
		}
}

}
}
