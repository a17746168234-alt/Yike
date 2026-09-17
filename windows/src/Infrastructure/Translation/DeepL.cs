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
public class DeepL
{
	private sealed class TranslationCacheItem
	{
		public DateTime Created;

		public List<string> Values;
	}

	private static readonly HttpClient sharedClient = CreateClient (ProxySettings.Current);

	private static readonly HttpClient fallbackClient = CreateClient (null);

	private static readonly Dictionary<string, TranslationCacheItem> translationCache = new Dictionary<string, TranslationCacheItem> ();

	private readonly HttpClient client;

	private readonly string key;

	private readonly bool useCache;

	private readonly bool customTransport;

	private string Endpoint {
		get {
			if (!key.EndsWith (":fx", StringComparison.Ordinal)) {
				return "https://api.deepl.com";
			}
			return "https://api-free.deepl.com";
		}
	}

	private static HttpClient CreateClient (string proxy)
	{
		HttpClient httpClient = new HttpClient (ProxySettings.CreateHandler (proxy));
		httpClient.Timeout = TimeSpan.FromSeconds (12.0);
		return httpClient;
	}

	public DeepL (string key, HttpMessageHandler handler = null)
	{
		this.key = key;
		useCache = handler == null;
		customTransport = handler != null;
		if (handler == null) {
			client = sharedClient;
			return;
		}
		client = new HttpClient (handler);
		client.Timeout = TimeSpan.FromSeconds (12.0);
	}

	public async Task<string> Request (string path, object body, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace (key)) {
			throw new InvalidOperationException ("请先在设置中填写 DeepL API 密钥。");
		}
		for (int attempt = 0; attempt < 3; attempt++) {
			try {
				using (HttpRequestMessage request = new HttpRequestMessage ((body == null) ? HttpMethod.Get : HttpMethod.Post, Endpoint + path)) {
					request.Headers.TryAddWithoutValidation ("Authorization", "DeepL-Auth-Key " + key);
					if (body != null) {
						request.Content = new StringContent (Store.Json.Serialize (body), Encoding.UTF8, "application/json");
					}
					HttpClient transport = ((customTransport || attempt < 2) ? client : fallbackClient);
					using (HttpResponseMessage response = await transport.SendAsync (request, ct)) {
						int status = (int)response.StatusCode;
						switch (status) {
						case 401:
						case 403:
							throw new InvalidOperationException ("DeepL 密钥无效或无权限，请在设置中更新。");
						case 456:
							throw new InvalidOperationException ("DeepL 本月字符额度已用完。");
						case 429:
							throw new InvalidOperationException ("请求过于频繁，请稍后再试。");
						}
						if (!response.IsSuccessStatusCode) {
							if ((status != 408 && status != 425 && status != 500 && status != 502 && status != 503 && status != 504) || attempt >= 2) {
								throw new InvalidOperationException ("DeepL 服务返回错误（" + status + "）。");
							}
							await Task.Delay (200 * (attempt + 1), ct);
							continue;
						}
						return await response.Content.ReadAsStringAsync ();
					}
				}
			} catch (TaskCanceledException) {
				ct.ThrowIfCancellationRequested ();
				if (attempt == 2) {
					throw new TimeoutException ("请求超时，已自动重试。请检查当前代理或网络。");
				}
				goto IL_0495;
			} catch (HttpRequestException) {
				if (attempt == 2) {
					throw new InvalidOperationException ("无法连接 DeepL，请检查当前代理或网络。");
				}
				goto IL_0495;
			}
			IL_0495:
			await Task.Delay (200 * (attempt + 1), ct);
		}
		throw new TimeoutException ();
	}

	public Task<List<string>> Translate (IList<string> texts, string source, string target, CancellationToken ct)
	{
		return Translate (texts, source, target, null, ct);
	}

	public async Task<List<string>> Translate (IList<string> texts, string source, string target, string context, CancellationToken ct)
	{
		List<string> output = new List<string> ();
		int i = 0;
		while (i < texts.Count) {
			List<string> batch = new List<string> ();
			int bytes = 0;
			while (i < texts.Count && batch.Count < 40) {
				int byteCount = Encoding.UTF8.GetByteCount (texts [i]);
				if (batch.Count > 0 && bytes + byteCount > 90000) {
					break;
				}
				batch.Add (texts [i++]);
				bytes += byteCount;
			}
			string cacheKey = CacheKey (batch, source, target, context);
			TranslationCacheItem cached = null;
			if (useCache) {
				lock (translationCache) {
					translationCache.TryGetValue (cacheKey, out cached);
				}
			}
			if (cached != null && DateTime.UtcNow - cached.Created < TimeSpan.FromMinutes (20.0)) {
				output.AddRange (cached.Values);
				continue;
			}
			Dictionary<string, object> body = new Dictionary<string, object> {
				{ "text", batch },
				{ "target_lang", target },
				{ "preserve_formatting", true },
				{ "split_sentences", "1" },
				{ "model_type", "latency_optimized" }
			};
			if (!string.IsNullOrWhiteSpace (context)) {
				body ["context"] = ((context.Length > 8000) ? context.Substring (0, 8000) : context);
			}
			if (source != "auto") {
				body ["source_lang"] = ((source == "ZH-HANS") ? "ZH" : ((source == "EN-US") ? "EN" : source));
			}
			Dictionary<string, object> dictionary = Store.Json.Deserialize<Dictionary<string, object>> (await Request ("/v2/translate", body, ct));
			Dictionary<string, object> data = dictionary;
			ArrayList rows = (ArrayList)data ["translations"];
			if (rows.Count != batch.Count) {
				throw new InvalidOperationException ("翻译返回结果不完整。");
			}
			List<string> translatedBatch = new List<string> ();
			foreach (Dictionary<string, object> item in rows) {
				string value = item ["text"] as string;
				if (string.IsNullOrWhiteSpace (value)) {
					throw new InvalidOperationException ("翻译返回了空结果。");
				}
				translatedBatch.Add (ChinesePinyin.Remove (value, target));
			}
			if (useCache) {
				lock (translationCache) {
					if (translationCache.Count >= 160) {
						translationCache.Remove (translationCache.OrderBy ((KeyValuePair<string, TranslationCacheItem> pair) => pair.Value.Created).First ().Key);
					}
					translationCache [cacheKey] = new TranslationCacheItem {
						Created = DateTime.UtcNow,
						Values = new List<string> (translatedBatch)
					};
				}
			}
			output.AddRange (translatedBatch);
		}
		return output;
	}

	private static string CacheKey (IList<string> texts, string source, string target, string context)
	{
		string s = source + "\n" + target + "\n" + (context ?? "") + "\n" + string.Join ("\u001f", texts);
		using (SHA256 sHA = SHA256.Create ()) {
			return Convert.ToBase64String (sHA.ComputeHash (Encoding.UTF8.GetBytes (s)));
		}
	}

	public async Task<List<string>> TranslateProgressive (IList<string> texts, string source, string target, string context, Action<int, string> progress, CancellationToken ct)
	{
		if (texts == null || texts.Count == 0) {
			return new List<string> ();
		}
		if (texts.Count == 1) {
			List<string> one = await Translate (texts, source, target, context, ct);
			if (progress != null) {
				progress (0, one [0]);
			}
			return one;
		}
		int batchSize = 6;
		int batchCount = (texts.Count + batchSize - 1) / batchSize;
		int workers = Math.Min (4, batchCount);
		SemaphoreSlim gate = new SemaphoreSlim (workers, workers);
		string[] values = new string[texts.Count];
		Task<List<string>>[] tasks = Enumerable.Range (0, batchCount).Select (async delegate(int num2) {
			await gate.WaitAsync (ct);
			try {
				int start2 = num2 * batchSize;
				int count = Math.Min (batchSize, texts.Count - start2);
				List<string> part = new List<string> ();
				for (int i = 0; i < count; i++) {
					part.Add (texts [start2 + i]);
				}
				return await Translate (part, source, target, context, ct);
			} finally {
				gate.Release ();
			}
		}).ToArray ();
		for (int batchNumber = 0; batchNumber < tasks.Length; batchNumber++) {
			List<string> translated = await tasks [batchNumber];
			int start = batchNumber * batchSize;
			for (int num = 0; num < translated.Count; num++) {
				values [start + num] = translated [num];
				if (progress != null) {
					progress (start + num, translated [num]);
				}
			}
		}
		gate.Dispose ();
		return values.ToList ();
	}
}

}
