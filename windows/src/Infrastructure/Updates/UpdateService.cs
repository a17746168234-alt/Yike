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
internal sealed class UpdateService
{
	private const int MaximumFeedBytes = 65536;

	private readonly string baseDirectory;

	private readonly Version currentVersion;

	public UpdateService (string baseDirectory = null, Version currentVersion = null)
	{
		this.baseDirectory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
		this.currentVersion = currentVersion ?? typeof(App).Assembly.GetName ().Version;
	}

	public async Task<UpdateCheckResult> CheckAsync (CancellationToken token)
	{
		try {
			string sourcePath = System.IO.Path.Combine (baseDirectory, "update-source.txt");
			string json;
			if (File.Exists (sourcePath)) {
				string source = (File.ReadAllText (sourcePath) ?? "").Trim ();
				Uri uri;
				if (!Uri.TryCreate (source, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps) {
					return UpdateCheckResult.Failed (currentVersion, "更新源必须使用 HTTPS 地址");
				}
				using (HttpClient client = new HttpClient ()) {
					client.Timeout = TimeSpan.FromSeconds (15.0);
					client.MaxResponseContentBufferSize = 65536L;
					using (HttpResponseMessage response = await client.GetAsync (uri, HttpCompletionOption.ResponseContentRead, token)) {
						if (response.RequestMessage == null || response.RequestMessage.RequestUri == null || response.RequestMessage.RequestUri.Scheme != Uri.UriSchemeHttps) {
							return UpdateCheckResult.Failed (currentVersion, "更新服务重定向到了不安全地址");
						}
						response.EnsureSuccessStatusCode ();
						json = await response.Content.ReadAsStringAsync ();
						if (json.Length > 65536) {
							return UpdateCheckResult.Failed (currentVersion, "更新信息超过允许大小");
						}
					}
				}
			} else {
				string text = System.IO.Path.Combine (baseDirectory, "update-feed.json");
				if (!File.Exists (text)) {
					return UpdateCheckResult.Failed (currentVersion, "未找到更新信息，请重新安装 Yike");
				}
				FileInfo fileInfo = new FileInfo (text);
				if (fileInfo.Length <= 0 || fileInfo.Length > 65536) {
					return UpdateCheckResult.Failed (currentVersion, "更新信息文件无效");
				}
				json = File.ReadAllText (text);
			}
			token.ThrowIfCancellationRequested ();
			return Parse (json, currentVersion);
		} catch (OperationCanceledException) {
			return UpdateCheckResult.Failed (currentVersion, "检查更新已取消");
		} catch (Exception ex2) {
			return UpdateCheckResult.Failed (currentVersion, "无法连接更新服务：" + ex2.Message);
		}
	}

	internal static UpdateCheckResult Parse (string json, Version currentVersion)
	{
		try {
			UpdateManifest updateManifest = Store.Json.Deserialize<UpdateManifest> (json);
			Version result;
			if (updateManifest == null || !Version.TryParse (updateManifest.Version, out result) || result.Major < 0) {
				return UpdateCheckResult.Failed (currentVersion, "更新版本信息无效");
			}
			Uri result2;
			if (!Uri.TryCreate (updateManifest.DownloadUrl, UriKind.Absolute, out result2) || (result2.Scheme != Uri.UriSchemeHttps && result2.Scheme != "ms-windows-store")) {
				return UpdateCheckResult.Failed (currentVersion, "更新下载地址无效");
			}
			return UpdateCheckResult.Found (currentVersion, result, result2.AbsoluteUri, updateManifest.Notes);
		} catch (Exception ex) {
			return UpdateCheckResult.Failed (currentVersion, "更新信息无法解析：" + ex.Message);
		}
	}
}

}
