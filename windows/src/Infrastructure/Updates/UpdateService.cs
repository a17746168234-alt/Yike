using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsTranslator {
internal sealed class UpdateService {
	internal const string ReleasesUrl = "https://github.com/a17746168234-alt/Yike/releases";
	internal const string ApiUrl = "https://api.github.com/repos/a17746168234-alt/Yike/releases?per_page=100";
	internal const string ServerManifestUrl = "https://n5v1b.cn/yike-api/v1/update/windows";
	internal const string RepositoryManifestUrl = "https://raw.githubusercontent.com/a17746168234-alt/Yike/main/server/update-windows.json";
	private const int MaximumFeedBytes = 2097152;
	private readonly string baseDirectory;
	private readonly Version currentVersion;
	private readonly Func<Uri, CancellationToken, Task<string>> fetch;
	private readonly Func<string, HttpMessageHandler> handlerFactory;

	public UpdateService(string baseDirectory = null, Version currentVersion = null,
		Func<Uri, CancellationToken, Task<string>> fetch = null, Func<string, HttpMessageHandler> handlerFactory = null) {
		this.baseDirectory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
		this.currentVersion = currentVersion ?? typeof(App).Assembly.GetName().Version;
		this.fetch = fetch ?? FetchAsync;
		this.handlerFactory = handlerFactory ?? delegate(string proxy) {
			HttpClientHandler handler = ProxySettings.CreateHandler(proxy);
			if (proxy == null) handler.UseProxy = false;
			return handler;
		};
	}

	public async Task<UpdateCheckResult> CheckAsync(CancellationToken token) {
		try {
			string customSource = Path.Combine(baseDirectory, "update-source.txt");
			if (File.Exists(customSource)) {
				Uri uri;
				if (!Uri.TryCreate(File.ReadAllText(customSource).Trim(), UriKind.Absolute, out uri) || uri.Scheme != "https")
					return UpdateCheckResult.Failed(currentVersion, "更新源必须使用 HTTPS 地址");
				string manifest = await fetch(uri, token).ConfigureAwait(false);
				token.ThrowIfCancellationRequested();
				return Parse(manifest, currentVersion);
			}
			// The first-party manifest carries an installer hash and size, making the
			// in-app button independent from GitHub API availability and rate limits.
			foreach (string manifestUrl in new[] { ServerManifestUrl, RepositoryManifestUrl }) {
				try {
					string serverJson = await fetch(new Uri(manifestUrl), token).ConfigureAwait(false);
					token.ThrowIfCancellationRequested();
					UpdateCheckResult serverResult = ParseServerManifest(serverJson, currentVersion);
					if (serverResult.Success) return serverResult;
				} catch (OperationCanceledException) {
					throw;
				} catch (Exception) {
					// Try the repository-hosted manifest, then the Releases API.
				}
			}
			// Bundled metadata describes this installer, never the latest online release.
			UpdateCheckResult newest = null;
			for (int page = 1; page <= 5; page++) {
				string json = await fetch(new Uri(ApiUrl + "&page=" + page), token).ConfigureAwait(false);
				token.ThrowIfCancellationRequested();
				UpdateCheckResult candidate = ParseGitHubReleases(json, currentVersion);
				if (candidate.Success && (newest == null || candidate.LatestVersion > newest.LatestVersion)) newest = candidate;
				GitHubRelease[] releases = Store.Json.Deserialize<GitHubRelease[]>(json);
				if (releases.Length < 100) break;
			}
			return newest ?? UpdateCheckResult.Failed(currentVersion, "更新服务器和 GitHub 均未找到可用的 Windows 稳定版。");
		} catch (OperationCanceledException) {
			return UpdateCheckResult.Failed(currentVersion, token.IsCancellationRequested ? "检查更新已取消" : "连接更新服务超时，请检查网络后重试。");
		} catch (Exception) {
			return UpdateCheckResult.Failed(currentVersion, "无法连接更新服务器或 GitHub，请检查网络或代理后重试；此次未确认是否为最新版。");
		}
	}

	private async Task<string> FetchAsync(Uri uri, CancellationToken token) {
		string proxy = ProxySettings.Current;
		try { return await FetchViaAsync(uri, proxy, token).ConfigureAwait(false); }
		catch (Exception) {
			token.ThrowIfCancellationRequested();
			if (proxy == null) throw;
		}
		return await FetchViaAsync(uri, null, token).ConfigureAwait(false);
	}

	private async Task<string> FetchViaAsync(Uri uri, string proxy, CancellationToken token) {
		using (HttpClient client = NewClient(proxy)) {
			client.MaxResponseContentBufferSize = MaximumFeedBytes;
			using (HttpResponseMessage response = await client.GetAsync(uri, token).ConfigureAwait(false)) {
				if (response.RequestMessage.RequestUri.Scheme != "https") throw new InvalidDataException();
				response.EnsureSuccessStatusCode();
				string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
				if (json.Length > MaximumFeedBytes) throw new InvalidDataException();
				return json;
			}
		}
	}

	private HttpClient NewClient(string proxy) {
		HttpClient client = new HttpClient(handlerFactory(proxy)) { Timeout = TimeSpan.FromSeconds(10) };
		client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Yike-Windows/" + typeof(App).Assembly.GetName().Version);
		client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/vnd.github+json");
		client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
		return client;
	}

	internal static UpdateCheckResult ParseGitHubReleases(string json, Version current) {
		try {
			if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumFeedBytes) throw new InvalidDataException();
			GitHubRelease[] releases = Store.Json.Deserialize<GitHubRelease[]>(json);
			UpdateCheckResult newest = null;
			foreach (GitHubRelease release in releases ?? new GitHubRelease[0]) {
				Version version;
				if (release == null || release.draft || release.prerelease || release.tag_name == null ||
					!Regex.IsMatch(release.tag_name, @"^windows-v\d+\.\d+(\.\d+){0,2}$") ||
					!Version.TryParse(release.tag_name.Substring(9), out version)) continue;
				version = NormalizeVersion(version);
				string pageUrl = ReleasesUrl + "/tag/" + release.tag_name;
				GitHubAsset asset = (release.assets ?? new GitHubAsset[0]).FirstOrDefault(a => a != null &&
					a.name == "Yike-Setup.exe" && a.state == "uploaded" && a.size > 0 && a.size <= 2147483648L &&
					a.browser_download_url == ReleasesUrl + "/download/" + release.tag_name + "/Yike-Setup.exe");
				if (asset == null) continue;
				string digest = asset.digest ?? "";
				string sha = Regex.IsMatch(digest, @"^sha256:[a-fA-F0-9]{64}$") ? digest.Substring(7).ToLowerInvariant() : null;
				UpdateCheckResult candidate = UpdateCheckResult.Found(NormalizeVersion(current), version,
					asset.browser_download_url, release.body, pageUrl, sha, asset.size);
				if (newest == null || version > newest.LatestVersion) newest = candidate;
			}
			return newest ?? UpdateCheckResult.Failed(current, "未找到可用的 Windows 稳定版安装包。");
		} catch (Exception) { return UpdateCheckResult.Failed(current, "GitHub 发布信息无法解析，请打开发布页查看。"); }
	}

	internal static Version NormalizeVersion(Version version) {
		return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
	}

	internal static UpdateCheckResult ParseServerManifest(string json, Version current) {
		try {
			if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumFeedBytes) throw new InvalidDataException();
			ServerUpdateManifest manifest = Store.Json.Deserialize<ServerUpdateManifest>(json);
			Version version; Uri download;
			if (manifest == null || !Version.TryParse(manifest.version, out version) || manifest.build < 1 ||
				string.IsNullOrWhiteSpace(manifest.title) || string.IsNullOrWhiteSpace(manifest.notes) ||
				!Uri.TryCreate(manifest.download_url, UriKind.Absolute, out download) || download.Scheme != "https" ||
				!Regex.IsMatch(manifest.sha256 ?? "", @"^[a-fA-F0-9]{64}$") || manifest.size <= 0 || manifest.size > 2147483648L)
				return UpdateCheckResult.Failed(current, "服务器版本清单无效。");
			string tag = "windows-v" + manifest.version;
			string expected = ReleasesUrl + "/download/" + tag + "/Yike-Setup.exe";
			if (!string.Equals(download.AbsoluteUri, expected, StringComparison.Ordinal))
				return UpdateCheckResult.Failed(current, "服务器安装包地址不受信任。");
			return UpdateCheckResult.Found(NormalizeVersion(current), NormalizeVersion(version), download.AbsoluteUri,
				manifest.notes, ReleasesUrl + "/tag/" + tag, manifest.sha256.ToLowerInvariant(), manifest.size);
		} catch (Exception) {
			return UpdateCheckResult.Failed(current, "服务器版本清单无法解析。");
		}
	}

	internal static UpdateCheckResult Parse(string json, Version currentVersion) {
		try {
			UpdateManifest manifest = Store.Json.Deserialize<UpdateManifest>(json);
			Version version; Uri url;
			if (manifest == null || !Version.TryParse(manifest.Version, out version))
				return UpdateCheckResult.Failed(currentVersion, "更新版本信息无效");
			if (!Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out url) || (url.Scheme != "https" && url.Scheme != "ms-windows-store"))
				return UpdateCheckResult.Failed(currentVersion, "更新下载地址无效");
			return UpdateCheckResult.Found(NormalizeVersion(currentVersion), NormalizeVersion(version), url.AbsoluteUri, manifest.Notes);
		} catch (Exception) { return UpdateCheckResult.Failed(currentVersion, "更新信息无法解析，请检查更新源。"); }
	}

	public async Task<string> DownloadInstallerAsync(UpdateCheckResult release, IProgress<int> progress, CancellationToken token) {
		if (!release.CanInstall) throw new InvalidOperationException("此更新没有可核对的安装包，请打开发布页下载。");
		string root = Path.Combine(Path.GetTempPath(), "Yike-update-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		string path = Path.Combine(root, "Yike-Setup.exe");
		try {
			using (HttpClient client = NewClient(ProxySettings.Current))
			using (CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token)) {
				client.Timeout = TimeSpan.FromMinutes(15);
				deadline.CancelAfter(TimeSpan.FromMinutes(15));
				client.DefaultRequestHeaders.Remove("Accept");
				client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/octet-stream");
				using (CancellationTokenSource activity = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token)) {
				activity.CancelAfter(TimeSpan.FromSeconds(60));
				using (HttpResponseMessage response = await client.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, activity.Token).ConfigureAwait(false)) {
					response.EnsureSuccessStatusCode();
					if (response.RequestMessage.RequestUri.Scheme != "https") throw new InvalidDataException("安装包重定向到了不安全地址。");
					using (Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
					using (FileStream output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
					using (SHA256 hash = SHA256.Create()) {
						byte[] buffer = new byte[81920]; long received = 0; int count;
						while ((count = await input.ReadAsync(buffer, 0, buffer.Length, activity.Token).ConfigureAwait(false)) > 0) {
							activity.CancelAfter(TimeSpan.FromSeconds(60));
							received += count;
							if (received > release.Size) throw new InvalidDataException("安装包大小与发布信息不一致。");
							await output.WriteAsync(buffer, 0, count, deadline.Token).ConfigureAwait(false);
							hash.TransformBlock(buffer, 0, count, buffer, 0);
							if (progress != null) progress.Report((int)(received * 100 / release.Size));
						}
						hash.TransformFinalBlock(new byte[0], 0, 0);
						string actual = BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
						if (received != release.Size || actual != release.Sha256) throw new InvalidDataException("安装包校验失败，已停止安装，请重试。");
					}
				}
				}
			}
			token.ThrowIfCancellationRequested();
			return path;
		} catch {
			if (File.Exists(path)) File.Delete(path);
			if (Directory.Exists(root)) Directory.Delete(root);
			throw;
		}
	}

	internal sealed class GitHubRelease {
		public string tag_name { get; set; }
		public bool draft { get; set; }
		public bool prerelease { get; set; }
		public string body { get; set; }
		public GitHubAsset[] assets { get; set; }
	}
	internal sealed class GitHubAsset {
		public string name { get; set; }
		public string state { get; set; }
		public long size { get; set; }
		public string digest { get; set; }
		public string browser_download_url { get; set; }
	}
}
}
