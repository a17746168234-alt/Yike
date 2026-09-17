using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsTranslator {
internal static class UpdateTests {
	private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
	private static object Release(string tag, bool draft = false, bool prerelease = false, string state = "uploaded", string digest = null) {
		return new { tag_name = tag, draft = draft, prerelease = prerelease, body = "release notes", assets = new[] {
			new { name = "Yike-Setup.exe", state = state, size = 3, digest = digest ?? "sha256:" + new string('a', 64),
				browser_download_url = UpdateService.ReleasesUrl + "/download/" + tag + "/Yike-Setup.exe" }
		} };
	}
	public static void Run(List<string> lines) {
		Version old = new Version(1, 2, 0, 0);
		string json = Store.Json.Serialize(new[] { Release("v2.0-build71"), Release("windows-v1.2.1"), Release("windows-v1.2.3"),
			Release("windows-v9.0.0", true), Release("windows-v8.0.0", false, true), Release("windows-v7.0.0", false, false, "starter") });
		UpdateCheckResult result = UpdateService.ParseGitHubReleases(json, old);
		Check(result.Success && result.LatestVersion == new Version(1,2,3,0) && result.UpdateAvailable && result.CanInstall, "Windows release filtering/max version failed");
		Check(!UpdateService.ParseGitHubReleases(json, new Version(1,2,3)).UpdateAvailable, "3/4 component versions differ");
		Check(!UpdateService.ParseGitHubReleases("{}", old).Success && !UpdateService.ParseGitHubReleases("[]", old).Success, "invalid/empty releases accepted");
		Check(!UpdateService.ParseGitHubReleases(json.Replace("https://github.com/", "https://example.com/"), old).Success, "foreign installer URL accepted");
		Check(!UpdateService.ParseGitHubReleases(json.Replace("sha256:", "other:"), old).CanInstall, "unverified installer can be executed");
		string root = Path.Combine(Path.GetTempPath(), "Yike-update-test-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try {
			File.WriteAllText(Path.Combine(root, "update-feed.json"), "{\"Version\":\"1.2.0.0\",\"DownloadUrl\":\"https://example.com\"}");
			int requests = 0;
			UpdateService online = new UpdateService(root, old, delegate(Uri uri, CancellationToken token) {
				requests++; Check(uri.Host == "api.github.com", "default source does not read GitHub"); return Task.FromResult(json);
			});
			Check(online.CheckAsync(CancellationToken.None).GetAwaiter().GetResult().UpdateAvailable && requests == 1, "stale bundled feed masks newer release");
			UpdateService offline = new UpdateService(root, old, delegate { throw new HttpRequestException(); });
			Check(!offline.CheckAsync(CancellationToken.None).GetAwaiter().GetResult().Success, "offline check falsely reports latest");
			using (CancellationTokenSource cancel = new CancellationTokenSource()) {
				cancel.Cancel(); Check(!online.CheckAsync(cancel.Token).GetAwaiter().GetResult().Success, "cancelled check succeeded");
			}
			File.WriteAllText(Path.Combine(root, "update-source.txt"), "http://example.com");
			Check(!online.CheckAsync(CancellationToken.None).GetAwaiter().GetResult().Success, "HTTP custom feed accepted");
			File.WriteAllText(Path.Combine(root, "update-source.txt"), "https://example.com");
			UpdateService custom = new UpdateService(root, old, delegate { return Task.FromResult("{\"Version\":\"1.2.4\",\"DownloadUrl\":\"https://example.com\"}"); });
			Check(custom.CheckAsync(CancellationToken.None).GetAwaiter().GetResult().LatestVersion == new Version(1,2,4,0), "custom HTTPS source stopped working");
			object[] many = Enumerable.Range(0,100).Select(i=>Release("v2.0-build"+i)).ToArray();
			UpdateService pages = new UpdateService(root + "-missing", old, delegate(Uri uri, CancellationToken token) { return Task.FromResult(uri.Query.Contains("page=2") ? json : Store.Json.Serialize(many)); });
			Check(pages.CheckAsync(CancellationToken.None).GetAwaiter().GetResult().UpdateAvailable, "Windows release on second page was lost");
			DownloadTests(old);
		} finally { Directory.Delete(root, true); }
		lines.Add("PASS: GitHub Windows-only stable releases; latest semantic version; pagination; stale local feed ignored; offline/cancel never report latest; installer SHA-256 and size; failed/cancelled download cleanup; HTTPS custom feed retained");
	}
	private static void DownloadTests(Version old) {
		byte[] bytes = { 1, 2, 3 }; string digest;
		using (SHA256 sha = SHA256.Create()) digest = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
		UpdateCheckResult good = UpdateCheckResult.Found(old, new Version(1,2,3,0), UpdateService.ReleasesUrl + "/download/windows-v1.2.3/Yike-Setup.exe", "", UpdateService.ReleasesUrl, digest, bytes.Length);
		UpdateService service = new UpdateService(handlerFactory: delegate { return new BytesHandler(bytes); });
		string path = service.DownloadInstallerAsync(good, null, CancellationToken.None).GetAwaiter().GetResult();
		try { Check(File.ReadAllBytes(path).SequenceEqual(bytes), "verified download corrupted"); }
		finally { File.Delete(path); Directory.Delete(Path.GetDirectoryName(path)); }
		string[] before = Directory.GetDirectories(Path.GetTempPath(), "Yike-update-*");
		foreach (UpdateCheckResult bad in new[] {
			UpdateCheckResult.Found(old, new Version(2,0,0,0), good.DownloadUrl, "", good.ReleaseUrl, new string('0',64),3),
			UpdateCheckResult.Found(old, new Version(2,0,0,0), good.DownloadUrl, "", good.ReleaseUrl,digest,2),
			UpdateCheckResult.Found(old, new Version(2,0,0,0), good.DownloadUrl, "", good.ReleaseUrl,digest,4) }) {
			bool failed = false; try { service.DownloadInstallerAsync(bad,null,CancellationToken.None).GetAwaiter().GetResult(); } catch(InvalidDataException) { failed=true; }
			Check(failed, "invalid installer download accepted");
		}
		using (CancellationTokenSource cancel = new CancellationTokenSource()) {
			cancel.Cancel(); bool stopped=false;
			try { service.DownloadInstallerAsync(good,null,cancel.Token).GetAwaiter().GetResult(); } catch(OperationCanceledException) { stopped=true; }
			Check(stopped, "cancelled download completed");
		}
		Check(Directory.GetDirectories(Path.GetTempPath(), "Yike-update-*").Except(before).Count()==0, "failed download left temporary installer");
	}
	private sealed class BytesHandler : HttpMessageHandler {
		private readonly byte[] data;
		public BytesHandler(byte[] data) { this.data=data; }
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
			token.ThrowIfCancellationRequested();
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage=request, Content=new ByteArrayContent(data) });
		}
	}
	public static void Live(bool download) {
		string report = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"update-live-results.txt");
		try {
			UpdateService service = new UpdateService(currentVersion: new Version(1,2,0,0));
			UpdateCheckResult result = service.CheckAsync(CancellationToken.None).GetAwaiter().GetResult();
			Check(result.Success && result.LatestVersion >= new Version(1,2,2,0) && result.CanInstall, "live update check: " + result.Message);
			File.WriteAllText(report,"PASS: live GitHub Windows update check " + result.LatestVersion + "\nURL: " + result.DownloadUrl + "\nSHA256: " + result.Sha256 + "\nInstaller download: " + (download ? "in progress" : "not requested"));
			int previous = -1; object gate = new object();
			IProgress<int> progress = new Progress<int>(delegate(int percent) { lock(gate) { if (percent > previous) { previous=percent; File.AppendAllText(report,"\nDownloaded " + percent + "%"); } } });
			string path = download ? service.DownloadInstallerAsync(result,progress,CancellationToken.None).GetAwaiter().GetResult() : "not requested";
			File.WriteAllText(report,"PASS: latest Windows " + result.LatestVersion + "\nURL: " + result.DownloadUrl + "\nSHA256: " + result.Sha256 + "\nDownloaded: " + path);
		} catch(Exception ex) { File.WriteAllText(report,"FAIL: " + ex); Environment.ExitCode=1; }
	}
}
}
