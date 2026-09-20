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

	public async Task<TranslationExecutionResult> TranslateProgressive (RemoteAccountSession session, IList<string> texts, string publicSource, string deepLSource, string target, string context, Action<int, string> progress, CancellationToken ct)
	{
		bool personalAvailable = !string.IsNullOrWhiteSpace (personalKey);
		bool publicSupported = session != null && SupportsPublicPair (publicSource, target);
		bool rejectedSession = false;
		if (publicSupported) {
			int publicDelivered = 0;
			Action<int, string> publicProgress = delegate(int index, string value) {
				publicDelivered++;
				if (progress != null) progress (index, value);
			};
			try {
				List<string> values;
				using (YikeAccountClient client = publicClientFactory ()) values = await client.TranslateProgressive (session, texts, publicSource, target, publicProgress, ct);
				return new TranslationExecutionResult {
					Values = values, EngineName = "Yike 公共 DeepL", UsedPublicQuota = true
				};
			} catch (YikeApiException ex) {
				rejectedSession = ex.ErrorCode == "login_required";
				if (publicDelivered > 0 || !personalAvailable || !CanSafelyFallback (ex.ErrorCode)) throw;
			}
		}
		if (personalAvailable) {
			List<string> values = await personalClientFactory (personalKey).TranslateProgressive (texts, deepLSource, target, context, progress, ct);
			return new TranslationExecutionResult {
				Values = values,
				EngineName = publicSupported ? "DeepL（个人密钥后备）" : "DeepL",
				UsedPersonalFallback = publicSupported,
				PublicSessionRejected = rejectedSession
			};
		}
		if (session == null) throw new InvalidOperationException ("请先注册或登录 Yike 账号，或配置个人 DeepL 密钥。");
		throw new InvalidOperationException ("公共体验额度仅支持中文、英语、日语和韩语；该语言请配置个人 DeepL 密钥。");
	}

	internal static bool SupportsPublicPair (string source, string target)
	{
		try {
			return YikeAccountClient.PublicLanguage (source, true) != YikeAccountClient.PublicLanguage (target, false);
		} catch {
			return false;
		}
	}

	internal static bool CanSafelyFallback (string errorCode)
	{
		switch (errorCode ?? "") {
		case "login_required":
		case "email_required":
		case "trial_empty":
		case "pool_empty":
		case "shared_unavailable":
		case "usage_unavailable":
		case "busy":
			return true;
		default:
			return false;
		}
	}
}
}
