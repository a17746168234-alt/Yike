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
public static class DeepLCredentials
{
	public static async Task<DeepLCredentialResult> ValidateAndSave (string key, CancellationToken cancellationToken)
	{
		string candidate = (key ?? "").Trim ();
		if (candidate.Length == 0) {
			return Failure ("密钥不能为空。");
		}
		try {
			Dictionary<string, object> dictionary = Store.Json.Deserialize<Dictionary<string, object>> (await new DeepL (candidate).Request ("/v2/usage", null, cancellationToken));
			Dictionary<string, object> data = dictionary;
			long used = Convert.ToInt64 (data ["character_count"]);
			long limit = Convert.ToInt64 (data ["character_limit"]);
			Store.Key = candidate;
			return new DeepLCredentialResult {
				Success = true,
				Message = "保存成功，密钥已验证并启用。",
				Used = used,
				Limit = limit
			};
		} catch (OperationCanceledException) {
			throw;
		} catch (Exception ex2) {
			return Failure (ex2.Message);
		}
	}

	public static DeepLCredentialResult Remove ()
	{
		try {
			Store.Key = "";
			DeepLCredentialResult deepLCredentialResult = new DeepLCredentialResult ();
			deepLCredentialResult.Success = true;
			deepLCredentialResult.Message = "密钥已移除。";
			return deepLCredentialResult;
		} catch (Exception ex) {
			return Failure (ex.Message);
		}
	}

	private static DeepLCredentialResult Failure (string reason)
	{
		DeepLCredentialResult deepLCredentialResult = new DeepLCredentialResult ();
		deepLCredentialResult.Success = false;
		deepLCredentialResult.Message = "保存失败：" + reason;
		return deepLCredentialResult;
	}
}

}
