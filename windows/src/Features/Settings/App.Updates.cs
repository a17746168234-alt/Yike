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
private async Task CheckForUpdatesAsync (System.Windows.Controls.Button button, TextBlock statusText)
{
	if (button == null || !button.IsEnabled) {
		return;
	}
	button.IsEnabled = false;
	button.Content = "正在检查…";
	statusText.Text = "正在连接更新服务…";
	try {
		UpdateCheckResult result = await new UpdateService ().CheckAsync (CancellationToken.None);
		if (!result.Success) {
			statusText.Text = result.Message;
			ShowUpdateDialog ("无法检查更新", result.Message, null, null);
			return;
		}
		if (!result.UpdateAvailable) {
			statusText.Text = "当前为最新版";
			ShowUpdateDialog ("当前为最新版", string.Concat ("已安装 Yike ", result.CurrentVersion, "，无需更新。"), null, null);
			return;
		}
		statusText.Text = "发现新版本 " + result.LatestVersion;
		ShowUpdateDialog ("是否要更新为最新版", string.Concat ("Yike ", result.LatestVersion, " 已可用。", string.IsNullOrWhiteSpace (result.Notes) ? "" : ("\n\n" + result.Notes)), "立即更新", delegate {
			OpenWeb (result.DownloadUrl);
		});
	} finally {
		button.Content = "检查更新";
		button.IsEnabled = true;
	}
}


private void ShowUpdateDialog (string heading, string message, string primaryCaption, Action primaryAction)
{
	Window dialog = null;
	Grid grid = new Grid ();
	grid.Margin = new Thickness (30.0, 20.0, 30.0, 28.0);
	Grid grid2 = grid;
	grid2.RowDefinitions.Add (new RowDefinition ());
	grid2.RowDefinitions.Add (new RowDefinition {
		Height = GridLength.Auto
	});
	StackPanel stackPanel = new StackPanel ();
	stackPanel.VerticalAlignment = VerticalAlignment.Center;
	stackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
	stackPanel.MaxWidth = 470.0;
	StackPanel stackPanel2 = stackPanel;
	Border border = new Border ();
	border.Width = 58.0;
	border.Height = 58.0;
	border.CornerRadius = new CornerRadius (20.0);
	border.Background = OverlayBrush ("#253C56");
	border.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
	border.Child = new TextBlock {
		Text = ((primaryAction == null) ? "\ue73e" : "\ue895"),
		FontFamily = new System.Windows.Media.FontFamily ("Segoe Fluent Icons"),
		FontSize = 25.0,
		Foreground = OverlayBrush ("#4A9CFF"),
		HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
		VerticalAlignment = VerticalAlignment.Center
	};
	Border element = border;
	stackPanel2.Children.Add (element);
	stackPanel2.Children.Add (new TextBlock {
		Text = heading,
		Foreground = OverlayBrush ("#F4F7F8"),
		FontSize = 22.0,
		FontWeight = FontWeights.SemiBold,
		TextAlignment = TextAlignment.Center,
		Margin = new Thickness (0.0, 18.0, 0.0, 0.0)
	});
	stackPanel2.Children.Add (new TextBlock {
		Text = (message ?? ""),
		Foreground = OverlayBrush ("#929DA0"),
		FontSize = 13.0,
		TextWrapping = TextWrapping.Wrap,
		TextAlignment = TextAlignment.Center,
		Margin = new Thickness (0.0, 10.0, 0.0, 0.0)
	});
	grid2.Children.Add (stackPanel2);
	StackPanel stackPanel3 = new StackPanel ();
	stackPanel3.Orientation = System.Windows.Controls.Orientation.Horizontal;
	stackPanel3.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
	stackPanel3.Margin = new Thickness (0.0, 22.0, 0.0, 0.0);
	StackPanel stackPanel4 = stackPanel3;
	Grid.SetRow (stackPanel4, 1);
	grid2.Children.Add (stackPanel4);
	if (primaryAction != null) {
		System.Windows.Controls.Button button = OverlayButton ("稍后", delegate {
			dialog.Close ();
		});
		button.Margin = new Thickness (0.0);
		stackPanel4.Children.Add (button);
		System.Windows.Controls.Button button2 = OverlayButton (primaryCaption, delegate {
			dialog.Close ();
			primaryAction ();
		}, true);
		button2.Margin = new Thickness (10.0, 0.0, 0.0, 0.0);
		stackPanel4.Children.Add (button2);
	} else {
		System.Windows.Controls.Button button3 = OverlayButton ("确定", delegate {
			dialog.Close ();
		}, true);
		button3.Margin = new Thickness (0.0);
		stackPanel4.Children.Add (button3);
	}
	Window owner = settingsWindow ?? window;
	dialog = new Window {
		Width = 570.0,
		Height = 370.0,
		MinWidth = 520.0,
		MinHeight = 340.0,
		WindowStartupLocation = WindowStartupLocation.CenterOwner,
		FontFamily = window.FontFamily
	};
	DialogChrome.Apply (dialog, owner, "Yike 更新", grid2);
	SetOverlayResources (dialog);
	dialog.ShowDialog ();
}

}
}
