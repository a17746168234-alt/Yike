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
public partial class App {
private void EngineMenu ()
{
	EngineMenu (false);
}


private void EnginePreview ()
{
	EngineMenu (true);
}


private void EngineMenu (bool preview)
{
	Grid grid = new Grid ();
	grid.Margin = new Thickness (32.0);
	Grid grid2 = grid;
	grid2.RowDefinitions.Add (new RowDefinition {
		Height = GridLength.Auto
	});
	grid2.RowDefinitions.Add (new RowDefinition ());
	grid2.RowDefinitions.Add (new RowDefinition {
		Height = GridLength.Auto
	});
	Grid grid3 = new Grid ();
	StackPanel stackPanel = new StackPanel ();
	stackPanel.Children.Add (new TextBlock {
		Text = "设置 DeepL API Free",
		FontSize = 25.0,
		FontWeight = FontWeights.SemiBold,
		Foreground = OverlayBrush ("#F4F7F8")
	});
	stackPanel.Children.Add (new TextBlock {
		Text = "密钥只保存在这台电脑的 Windows 加密存储中。",
		FontSize = 13.0,
		Margin = new Thickness (0.0, 7.0, 0.0, 0.0),
		Foreground = OverlayBrush ("#9DA7AA")
	});
	grid3.Children.Add (stackPanel);
	Window dialog = null;
	System.Windows.Controls.Button button = OverlayButton ("取消", delegate {
		dialog.Close ();
	});
	button.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
	button.VerticalAlignment = VerticalAlignment.Top;
	grid3.Children.Add (button);
	grid2.Children.Add (grid3);
	StackPanel stackPanel2 = new StackPanel ();
	stackPanel2.Margin = new Thickness (0.0, 28.0, 0.0, 24.0);
	StackPanel stackPanel3 = stackPanel2;
	Grid.SetRow (stackPanel3, 1);
	grid2.Children.Add (stackPanel3);
	PasswordBox password = new PasswordBox {
		Password = Store.Key,
		Height = 46.0,
		Padding = new Thickness (13.0, 9.0, 13.0, 9.0),
		FontSize = 16.0,
		Foreground = OverlayBrush ("#F4F7F8"),
		Background = OverlayBrush ("#20282B"),
		BorderBrush = OverlayBrush ("#2579B8"),
		BorderThickness = new Thickness (2.0)
	};
	stackPanel3.Children.Add (password);
	CredentialFeedback feedback = new CredentialFeedback ();
	stackPanel3.Children.Add (feedback.View);
	TextBlock usageValue = new TextBlock {
		Text = "点击查询当前用量",
		HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
		Foreground = OverlayBrush ("#9DA7AA"),
		FontSize = 13.0
	};
	Border border = new Border ();
	border.CornerRadius = new CornerRadius (16.0);
	border.BorderBrush = OverlayBrush ("#354044");
	border.BorderThickness = new Thickness (1.0);
	border.Padding = new Thickness (18.0);
	border.Margin = new Thickness (0.0, 22.0, 0.0, 0.0);
	border.Background = OverlayBrush ("#8A1B2326");
	Border border2 = border;
	StackPanel stackPanel4 = (StackPanel)(border2.Child = new StackPanel ());
	stackPanel3.Children.Add (border2);
	Grid grid4 = new Grid ();
	grid4.Children.Add (new TextBlock {
		Text = "本月用量",
		FontSize = 15.0,
		FontWeight = FontWeights.SemiBold,
		Foreground = OverlayBrush ("#E7ECEE")
	});
	grid4.Children.Add (usageValue);
	stackPanel4.Children.Add (grid4);
	System.Windows.Controls.ProgressBar progress = new System.Windows.Controls.ProgressBar {
		Height = 8.0,
		Minimum = 0.0,
		Maximum = 100.0,
		Value = 0.0,
		Margin = new Thickness (0.0, 18.0, 0.0, 12.0),
		Foreground = OverlayBrush ("#1677F2"),
		Background = OverlayBrush ("#303A3D"),
		BorderThickness = new Thickness (0.0)
	};
	stackPanel4.Children.Add (progress);
	TextBlock usageHint = new TextBlock {
		Text = "具体额度以 DeepL 账户显示为准",
		Foreground = OverlayBrush ("#8E999D")
	};
	stackPanel4.Children.Add (usageHint);
	System.Windows.Controls.Button query = null;
	query = OverlayButton ("查询额度", async delegate {
		query.IsEnabled = false;
		usageValue.Text = "正在查询…";
		try {
			int num = default(int);
			int num2 = num;
			int num3 = 0;
			try {
				Dictionary<string, object> dictionary = Store.Json.Deserialize<Dictionary<string, object>> (await new DeepL (password.Password).Request ("/v2/usage", null, CancellationToken.None));
				Dictionary<string, object> data = dictionary;
				long limit = Convert.ToInt64 (data ["character_limit"]);
				long used = Convert.ToInt64 (data ["character_count"]);
				progress.Maximum = Math.Max (1L, limit);
				progress.Value = used;
				usageValue.Text = used.ToString ("N0") + " / " + limit.ToString ("N0") + " 字符";
				usageHint.Text = "已使用 " + Math.Round ((double)used * 100.0 / (double)Math.Max (1L, limit), 1) + "%";
			} catch (Exception ex) {
				usageValue.Text = ex.Message;
			}
		} finally {
			query.IsEnabled = true;
		}
	});
	Grid grid5 = new Grid ();
	grid5.Margin = new Thickness (0.0, 20.0, 0.0, 0.0);
	Grid grid6 = grid5;
	Grid.SetRow (grid6, 2);
	grid2.Children.Add (grid6);
	StackPanel stackPanel5 = new StackPanel ();
	stackPanel5.Orientation = System.Windows.Controls.Orientation.Horizontal;
	StackPanel stackPanel6 = stackPanel5;
	System.Windows.Controls.Button button2 = OverlayButton ("移除密钥", delegate {
		DeepLCredentialResult deepLCredentialResult = DeepLCredentials.Remove ();
		if (deepLCredentialResult.Success) {
			password.Clear ();
		}
		feedback.Show (deepLCredentialResult.Success, deepLCredentialResult.Message);
		usageValue.Text = (deepLCredentialResult.Success ? "尚未保存密钥" : deepLCredentialResult.Message);
	});
	button2.Foreground = OverlayBrush ("#FF5A5F");
	button2.Margin = new Thickness (0.0);
	stackPanel6.Children.Add (button2);
	stackPanel6.Children.Add (query);
	System.Windows.Controls.Button element = OverlayButton ("注册 / 获取密钥", delegate {
		dialog.Close ();
		ShowSettings (false, "deepl");
	});
	stackPanel6.Children.Add (element);
	grid6.Children.Add (stackPanel6);
	System.Windows.Controls.Button save = null;
	save = OverlayButton ("保存并使用 DeepL", async delegate {
		save.IsEnabled = false;
		password.IsEnabled = false;
		feedback.Working ("正在验证密钥并安全保存…");
		try {
			DeepLCredentialResult result = await DeepLCredentials.ValidateAndSave (password.Password, CancellationToken.None);
			feedback.Show (result.Success, result.Message);
			if (result.Success) {
				progress.Maximum = Math.Max (1L, result.Limit);
				progress.Value = result.Used;
				usageValue.Text = result.Used.ToString ("N0") + " / " + result.Limit.ToString ("N0") + " 字符";
				usageHint.Text = "已使用 " + Math.Round ((double)result.Used * 100.0 / (double)Math.Max (1L, result.Limit), 1) + "%";
				Status ("DeepL 密钥已保存并验证");
			}
		} finally {
			save.IsEnabled = true;
			password.IsEnabled = true;
		}
	}, true);
	save.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
	grid6.Children.Add (save);
	if (preview) {
		feedback.Show (true, "保存成功，密钥已验证并启用。");
	}
	dialog = OverlayDialog ("DeepL 密钥与帮助", 680.0, 560.0, grid2);
	grid3.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) {
		if (!DialogChrome.IsInteractiveSource (e.OriginalSource as DependencyObject, grid3) && e.LeftButton == MouseButtonState.Pressed) {
			dialog.DragMove ();
		}
	};
	if (preview) {
		dialog.ContentRendered += delegate {
			dialog.UpdateLayout ();
			FrameworkElement frameworkElement = (FrameworkElement)dialog.Content;
			RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap ((int)frameworkElement.ActualWidth, (int)frameworkElement.ActualHeight, 96.0, 96.0, PixelFormats.Pbgra32);
			renderTargetBitmap.Render (frameworkElement);
			ImageFiles.Save (renderTargetBitmap, System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "deepl-dialog.png"));
			dialog.Close ();
		};
	}
	ShowDimmed (dialog);
}


private UIElement BuildDeepLSettingsPage (bool preview)
{
	StackPanel stackPanel = new StackPanel ();
	StackPanel stackPanel2 = Card (stackPanel, "\ue72e", "DeepL API 密钥");
	stackPanel2.Children.Add (Label ("密钥只保存在这台电脑的 Windows 加密存储中。", true));
	PasswordBox password = new PasswordBox {
		Password = (preview ? "" : Store.Key),
		Height = 44.0,
		Padding = new Thickness (12.0, 9.0, 12.0, 9.0),
		FontSize = 15.0,
		Margin = new Thickness (0.0, 16.0, 0.0, 8.0)
	};
	stackPanel2.Children.Add (password);
	CredentialFeedback feedback = new CredentialFeedback ();
	stackPanel2.Children.Add (feedback.View);
	TextBlock usage = Label (preview ? "本月用量 · 点击查询" : "尚未查询本月用量", true);
	usage.Margin = new Thickness (0.0, 8.0, 0.0, 8.0);
	stackPanel2.Children.Add (usage);
	StackPanel stackPanel3 = new StackPanel ();
	stackPanel3.Orientation = System.Windows.Controls.Orientation.Horizontal;
	StackPanel stackPanel4 = stackPanel3;
	System.Windows.Controls.Button query = null;
	query = OverlayButton ("查询额度", async delegate {
		query.IsEnabled = false;
		usage.Text = "正在查询…";
		try {
			int num = default(int);
			int num2 = num;
			int num3 = 0;
			try {
				Dictionary<string, object> dictionary = Store.Json.Deserialize<Dictionary<string, object>> (await new DeepL (password.Password).Request ("/v2/usage", null, CancellationToken.None));
				Dictionary<string, object> data = dictionary;
				long limit = Convert.ToInt64 (data ["character_limit"]);
				long used = Convert.ToInt64 (data ["character_count"]);
				usage.Text = "本月已使用 " + used.ToString ("N0") + " / " + limit.ToString ("N0") + " 字符";
			} catch (Exception ex) {
				usage.Text = ex.Message;
			}
		} finally {
			query.IsEnabled = true;
		}
	});
	query.Margin = new Thickness (0.0, 6.0, 8.0, 0.0);
	stackPanel4.Children.Add (query);
	System.Windows.Controls.Button save = null;
	save = OverlayButton ("保存密钥", async delegate {
		save.IsEnabled = false;
		password.IsEnabled = false;
		feedback.Working ("正在验证密钥并安全保存…");
		try {
			DeepLCredentialResult result = await DeepLCredentials.ValidateAndSave (password.Password, CancellationToken.None);
			feedback.Show (result.Success, result.Message);
			usage.Text = (result.Success ? ("本月已使用 " + result.Used.ToString ("N0") + " / " + result.Limit.ToString ("N0") + " 字符") : result.Message);
			if (result.Success) {
				Status ("DeepL 密钥已保存并验证");
			}
		} finally {
			save.IsEnabled = true;
			password.IsEnabled = true;
		}
	}, true);
	save.Margin = new Thickness (0.0, 6.0, 8.0, 0.0);
	stackPanel4.Children.Add (save);
	System.Windows.Controls.Button button = OverlayButton ("移除密钥", delegate {
		DeepLCredentialResult deepLCredentialResult = DeepLCredentials.Remove ();
		if (deepLCredentialResult.Success) {
			password.Clear ();
		}
		feedback.Show (deepLCredentialResult.Success, deepLCredentialResult.Message);
		usage.Text = deepLCredentialResult.Message;
	});
	button.Foreground = OverlayBrush ("#FF656A");
	button.Margin = new Thickness (0.0, 6.0, 0.0, 0.0);
	stackPanel4.Children.Add (button);
	stackPanel2.Children.Add (stackPanel4);
	BuildDeepLHelp (stackPanel);
	return SettingsPage (stackPanel);
}

}
}
