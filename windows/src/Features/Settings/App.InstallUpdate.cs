using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace WindowsTranslator {
public partial class App {
	private async Task InstallUpdateAsync (UpdateCheckResult release) {
		using (CancellationTokenSource cancel = new CancellationTokenSource ()) {
			StackPanel body = new StackPanel { Margin = new Thickness (28) };
			body.Children.Add (Label ("正在下载 Yike Windows " + DisplayVersion (release.LatestVersion)));
			ProgressBar progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 10, Margin = new Thickness (0, 20, 0, 12) };
			body.Children.Add (progress);
			TextBlock message = Label ("正在连接更新服务；下载完成后核对大小与 SHA-256，再运行安装包。", true);
			body.Children.Add (message);
			Window dialog = OverlayDialog ("安装更新", 570, 320, body);
			System.Windows.Controls.Button cancelButton = OverlayButton ("取消下载", delegate { dialog.Close (); });
			cancelButton.Margin = new Thickness (0, 20, 0, 0);
			body.Children.Add (cancelButton);
			EventHandler closed = delegate { cancel.Cancel (); };
			dialog.Closed += closed;
			dialog.Show ();
			try {
				string path = await new UpdateService ().DownloadInstallerAsync (release, new Progress<int> (delegate(int percent) {
					progress.Value = percent;
					message.Text = percent == 100 ? "下载完成，正在校验安装包…" : "已下载 " + percent + "% · 可以取消，原安装保持不变。";
				}), cancel.Token);
				cancel.Token.ThrowIfCancellationRequested ();
				Process process = Process.Start (new ProcessStartInfo (path) { Arguments = "--launch", UseShellExecute = false, CreateNoWindow = true });
				if (process == null) throw new InvalidOperationException ("无法启动安装包，请重试。");
				process.Dispose ();
				exiting = true;
				window.Close ();
			} catch (OperationCanceledException) {
				if (dialog.IsVisible) { message.Text = "下载超时，请检查网络后重试。"; cancelButton.Content = "关闭"; }
			} catch (Exception ex) {
				if (dialog.IsVisible) {
					message.Text = "更新未安装：" + ex.Message;
					cancelButton.Content = "关闭";
					body.Children.Add (OverlayButton ("查看发布页", delegate { OpenWeb (release.ReleaseUrl); }));
				}
			} finally {
				dialog.Closed -= closed;
			}
		}
	}
}
}
