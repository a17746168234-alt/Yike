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
private void Settings ()
{
	ShowSettings (false, "appearance");
}


private void SettingsPreview ()
{
	ShowSettings (true, "appearance");
}


private void DeepLHelpPreview ()
{
	ShowSettings (true, "deepl");
}


private void AboutPreview ()
{
	ShowSettings (true, "about");
}


private void ShowSettings (bool preview)
{
	ShowSettings (preview, "appearance");
}


private void ShowSettings (bool preview, string initialPage)
{
	if (!preview && settingsWindow != null) {
		if (settingsWindow.WindowState == WindowState.Minimized) {
			settingsWindow.WindowState = WindowState.Normal;
		}
		settingsWindow.Show ();
		settingsWindow.Activate ();
		return;
	}
	Window dialog = null;
	bool reopenForTheme = false;
	Grid grid = new Grid ();
	grid.ColumnDefinitions.Add (new ColumnDefinition {
		Width = new GridLength (282.0)
	});
	grid.ColumnDefinitions.Add (new ColumnDefinition ());
	Grid grid2 = new Grid ();
	grid2.Background = OverlayBrush ("#D91B282A");
	Grid grid3 = grid2;
	grid3.RowDefinitions.Add (new RowDefinition {
		Height = GridLength.Auto
	});
	grid3.RowDefinitions.Add (new RowDefinition ());
	grid3.RowDefinitions.Add (new RowDefinition {
		Height = GridLength.Auto
	});
	grid.Children.Add (grid3);
	StackPanel stackPanel = new StackPanel ();
	stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
	stackPanel.Margin = new Thickness (30.0, 30.0, 22.0, 24.0);
	StackPanel stackPanel2 = stackPanel;
	Border border = new Border ();
	border.Width = 48.0;
	border.Height = 48.0;
	border.CornerRadius = new CornerRadius (12.0);
	border.Background = OverlayBrush ("#F2F7FA");
	border.Margin = new Thickness (0.0, 0.0, 14.0, 0.0);
	Border border2 = border;
	System.Windows.Controls.Image image = new System.Windows.Controls.Image ();
	image.Source = defaultAppIcon;
	image.Width = 38.0;
	image.Height = 38.0;
	image.Stretch = Stretch.Uniform;
	System.Windows.Controls.Image child = image;
	border2.Child = child;
	stackPanel2.Children.Add (border2);
	StackPanel stackPanel3 = new StackPanel ();
	stackPanel3.VerticalAlignment = VerticalAlignment.Center;
	StackPanel stackPanel4 = stackPanel3;
	TextBlock textBlock = new TextBlock ();
	textBlock.Text = "Yike";
	textBlock.FontSize = 23.0;
	textBlock.FontWeight = FontWeights.SemiBold;
	textBlock.Foreground = OverlayBrush ("#F4F7F8");
	textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
	textBlock.MaxWidth = 150.0;
	TextBlock element = textBlock;
	stackPanel4.Children.Add (element);
	stackPanel4.Children.Add (new TextBlock {
		Text = "设置",
		FontSize = 13.0,
		Margin = new Thickness (0.0, 2.0, 0.0, 0.0),
		Foreground = OverlayBrush ("#8F9A9D")
	});
	stackPanel2.Children.Add (stackPanel4);
	grid3.Children.Add (stackPanel2);
	StackPanel stackPanel5 = new StackPanel ();
	stackPanel5.Margin = new Thickness (18.0, 0.0, 18.0, 12.0);
	StackPanel stackPanel6 = stackPanel5;
	ScrollViewer scrollViewer = new ScrollViewer ();
	scrollViewer.Content = stackPanel6;
	scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
	scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
	ScrollViewer element2 = scrollViewer;
	Grid.SetRow (element2, 1);
	grid3.Children.Add (element2);
	Dictionary<string, System.Windows.Controls.Button> navButtons = new Dictionary<string, System.Windows.Controls.Button> ();
	Dictionary<string, UIElement> pages = new Dictionary<string, UIElement> ();
	Dictionary<string, string[]> titles = new Dictionary<string, string[]> ();
	TextBlock title = new TextBlock {
		FontSize = 27.0,
		FontWeight = FontWeights.SemiBold,
		Foreground = OverlayBrush ("#F4F7F8")
	};
	TextBlock subtitle = new TextBlock {
		FontSize = 14.0,
		Margin = new Thickness (0.0, 7.0, 0.0, 0.0),
		Foreground = OverlayBrush ("#929DA0")
	};
	ContentControl pageHost = new ContentControl ();
	Grid grid4 = new Grid ();
	grid4.Background = OverlayBrush ("#E5171F22");
	Grid grid5 = grid4;
	grid5.RowDefinitions.Add (new RowDefinition {
		Height = new GridLength (126.0)
	});
	grid5.RowDefinitions.Add (new RowDefinition {
		Height = new GridLength (1.0)
	});
	grid5.RowDefinitions.Add (new RowDefinition ());
	Grid.SetColumn (grid5, 1);
	grid.Children.Add (grid5);
	Grid grid6 = new Grid ();
	grid6.Margin = new Thickness (40.0, 25.0, 32.0, 22.0);
	Grid grid7 = grid6;
	StackPanel stackPanel7 = new StackPanel ();
	stackPanel7.VerticalAlignment = VerticalAlignment.Center;
	StackPanel stackPanel8 = stackPanel7;
	stackPanel8.Children.Add (title);
	stackPanel8.Children.Add (subtitle);
	grid7.Children.Add (stackPanel8);
	System.Windows.Controls.Button button = OverlayButton ("完成", delegate {
		dialog.Close ();
	});
	button.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
	button.VerticalAlignment = VerticalAlignment.Top;
	grid7.Children.Add (button);
	grid5.Children.Add (grid7);
	Border border3 = new Border ();
	border3.Background = OverlayBrush ("#344044");
	Border element3 = border3;
	Grid.SetRow (element3, 1);
	grid5.Children.Add (element3);
	Grid.SetRow (pageHost, 2);
	grid5.Children.Add (pageHost);
	pages ["appearance"] = BuildAppearancePage (delegate {
		reopenForTheme = true;
		if (dialog != null) {
			dialog.Close ();
		}
	});
	titles ["appearance"] = new string[2] { "外观", "调整 Yike 的界面显示" };
	pages ["deepl"] = BuildDeepLSettingsPage (preview);
	titles ["deepl"] = new string[2] { "DeepL 密钥与帮助", "管理翻译服务与用量" };
	StackPanel cards = new StackPanel ();
	Action cleanupSpeech = BuildSpeechCard (cards);
	pages ["speech"] = SettingsPage (cards);
	titles ["speech"] = new string[2] { "在线朗读", "选择音色与朗读方式" };
	pages ["shortcuts"] = BuildShortcutPage ();
	titles ["shortcuts"] = new string[2] { "快捷键与语音输入", "查看快捷键与按住空格说话的用法" };
	StackPanel cards2 = new StackPanel ();
	BuildPermissionCard (cards2);
	pages ["permissions"] = SettingsPage (cards2);
	titles ["permissions"] = new string[2] { "权限状态", "检查语音、OCR 与划词翻译权限" };
	StackPanel cards3 = new StackPanel ();
	BuildOcrCard (cards3, delegate {
		dialog.Close ();
	});
	pages ["ocr"] = SettingsPage (cards3);
	titles ["ocr"] = new string[2] { "OCR 识图", "设置图片文字识别" };
	pages ["history"] = BuildHistorySettingsPage (delegate {
		dialog.Close ();
	});
	titles ["history"] = new string[2] { "历史记录", "管理文字与图片的本机记录" };
	pages ["about"] = BuildAboutPage ();
	titles ["about"] = new string[2] { "关于与更新", "认识 Yike，了解本次更新" };
	Action<string> action = delegate(string key) {
		
		pageHost.Content = pages [key];
		title.Text = titles [key] [0];
		subtitle.Text = titles [key] [1];
		foreach (KeyValuePair<string, System.Windows.Controls.Button> item in navButtons) {
			bool flag = item.Key == key;
			item.Value.Background = (flag ? OverlayBrush ("#253C56") : System.Windows.Media.Brushes.Transparent);
			item.Value.Foreground = (flag ? OverlayBrush ("#4A9CFF") : OverlayBrush ("#DCE3E5"));
		}
	};
	AddSettingsNav (stackPanel6, navButtons, "appearance", "\ue790", "外观", action);
	AddSettingsNav (stackPanel6, navButtons, "deepl", "\ue72e", "DeepL 密钥与帮助", action);
	AddSettingsNav (stackPanel6, navButtons, "speech", "\ue767", "在线朗读", action);
	AddSettingsNav (stackPanel6, navButtons, "shortcuts", "\ue765", "快捷键与语音输入", action);
	AddSettingsNav (stackPanel6, navButtons, "permissions", "\ue72e", "权限状态", action);
	AddSettingsNav (stackPanel6, navButtons, "ocr", "\ue91b", "OCR 识图", action);
	AddSettingsNav (stackPanel6, navButtons, "history", "\ue81c", "历史记录", action);
	AddSettingsNav (stackPanel6, navButtons, "about", "\ue946", "关于与更新", action);
	TextBlock textBlock2 = new TextBlock ();
	textBlock2.Text = "Yike · Windows";
	textBlock2.Margin = new Thickness (30.0, 12.0, 20.0, 26.0);
	textBlock2.Foreground = OverlayBrush ("#718083");
	textBlock2.FontSize = 12.0;
	TextBlock element4 = textBlock2;
	Grid.SetRow (element4, 2);
	grid3.Children.Add (element4);
	dialog = OverlayDialog ("设置", 1120.0, 790.0, grid, 32.0);
	dialog.MinWidth = 960.0;
	dialog.MinHeight = 660.0;
	SetOverlayResources (dialog);
	action (pages.ContainsKey (initialPage) ? initialPage : "appearance");
	grid7.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) {
		if (e.LeftButton == MouseButtonState.Pressed) {
			dialog.DragMove ();
		}
	};
	if (preview) {
		dialog.ContentRendered += delegate {
			dialog.UpdateLayout ();
			FrameworkElement frameworkElement = (FrameworkElement)dialog.Content;
			RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap ((int)frameworkElement.ActualWidth, (int)frameworkElement.ActualHeight, 96.0, 96.0, PixelFormats.Pbgra32);
			renderTargetBitmap.Render (frameworkElement);
string path = initialPage == "deepl" ? "deepl-help.png" : initialPage == "about" ? "about-updates.png" : prefs.Appearance == "dark" ? "settings-dark.png" : "settings-light.png";
			ImageFiles.Save (renderTargetBitmap, System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, path));
			dialog.Close ();
		};
	}
	dialog.Closed += delegate {
		cleanupSpeech ();
		if (object.ReferenceEquals (settingsWindow, dialog)) {
			settingsWindow = null;
		}
		if (reopenForTheme && !preview) {
			window.Dispatcher.BeginInvoke ((Action)delegate {
				ShowSettings (false, "appearance");
			});
		}
	};
	if (preview) {
		ShowDimmed (dialog);
		return;
	}
	settingsWindow = dialog;
	dialog.Show ();
	dialog.Activate ();
}


private void SetOverlayResources (Window dialog)
{
	dialog.Resources ["WindowBrush"] = OverlayBrush ("#171F22");
	dialog.Resources ["PanelBrush"] = OverlayBrush ("#1B2427");
	dialog.Resources ["InkBrush"] = OverlayBrush ("#F1F5F6");
	dialog.Resources ["MutedBrush"] = OverlayBrush ("#929DA0");
	dialog.Resources ["LineBrush"] = OverlayBrush ("#354145");
	dialog.Resources ["BackdropBrush"] = OverlayBrush ("#171F22");
	dialog.Resources ["SurfaceBrush"] = OverlayBrush ("#8A1B2326");
	dialog.Resources ["ControlBrush"] = OverlayBrush ("#2A3336");
	dialog.Resources ["AccentBrush"] = OverlayBrush ("#1677F2");
	dialog.Resources ["AccentHoverBrush"] = OverlayBrush ("#2B87F5");
	dialog.Resources ["SoftAccentBrush"] = OverlayBrush ("#253C56");
}


private void AddSettingsNav (StackPanel parent, Dictionary<string, System.Windows.Controls.Button> buttons, string key, string glyph, string caption, Action<string> select)
{
	StackPanel stackPanel = IconText (glyph, caption, 18.0);
	stackPanel.Children.OfType<TextBlock> ().Last ().FontSize = 15.0;
	stackPanel.Children.OfType<TextBlock> ().Last ().FontWeight = FontWeights.SemiBold;
	System.Windows.Controls.Button button = new System.Windows.Controls.Button ();
	button.Content = stackPanel;
	button.Height = 54.0;
	button.Margin = new Thickness (0.0, 2.0, 0.0, 2.0);
	button.Padding = new Thickness (18.0, 0.0, 12.0, 0.0);
	button.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
	button.Foreground = OverlayBrush ("#DCE3E5");
	button.Background = System.Windows.Media.Brushes.Transparent;
	button.BorderThickness = new Thickness (0.0);
	System.Windows.Controls.Button button2 = button;
	button2.Click += delegate {
		select (key);
	};
	buttons [key] = button2;
	parent.Children.Add (button2);
}


private ScrollViewer SettingsPage (StackPanel cards)
{
	cards.Margin = new Thickness (38.0, 32.0, 38.0, 28.0);
	ScrollViewer scrollViewer = new ScrollViewer ();
	scrollViewer.Content = cards;
	scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
	scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
	return scrollViewer;
}


private UIElement BuildAppearancePage (Action refresh)
{
	StackPanel stackPanel = new StackPanel ();
	StackPanel stackPanel2 = Card (stackPanel, "\ue790", "界面外观");
	Row (stackPanel2, "主题").Children.Add (Segments (new string[3] { "跟随系统", "浅色", "深色" }, (prefs.Appearance == "light") ? 1 : ((prefs.Appearance == "dark") ? 2 : 0), delegate(int i) {
		prefs.Appearance = (new string[3] { "system", "light", "dark" }) [i];
		ApplyAppearance ();
		SavePreferences ();
		refresh ();
	}));
	TextBlock textBlock = Label ("主题会同步应用到主界面、设置、历史记录与 DeepL 窗口，主界面背景图保持不变。", true);
	textBlock.Margin = new Thickness (31.0, 12.0, 0.0, 6.0);
	stackPanel2.Children.Add (textBlock);
	return SettingsPage (stackPanel);
}


private void OpenWeb (string url)
{
	try {
		ProcessStartInfo processStartInfo = new ProcessStartInfo (url);
		processStartInfo.UseShellExecute = true;
		Process.Start (processStartInfo);
	} catch (Exception ex) {
		Status ("无法打开网页：" + ex.Message);
	}
}


private UIElement BuildShortcutPage ()
{
	StackPanel stackPanel = new StackPanel ();
	StackPanel stackPanel2 = Card (stackPanel, "\ue765", "划词翻译快捷键");
	DockPanel dockPanel = new DockPanel ();
	System.Windows.Controls.Button button = OverlayButton ("Ctrl + Shift + F", delegate {
	});
	button.IsHitTestVisible = false;
	DockPanel.SetDock (button, Dock.Right);
	dockPanel.Children.Add (button);
	dockPanel.Children.Add (Label ("在其他应用中选中文字，按快捷键打开翻译悬浮窗。"));
	stackPanel2.Children.Add (dockPanel);
	StackPanel stackPanel3 = Card (stackPanel, "\ue720", "按住空格语音输入");
	stackPanel3.Children.Add (new TextBlock {
		Text = "按住空格 0.3 秒开始语音输入，松开空格立即停止。",
		FontSize = 15.0,
		Foreground = OverlayBrush ("#EDF2F3"),
		TextWrapping = TextWrapping.Wrap
	});
	TextBlock textBlock = Label ("也可以点击主界面的“语音输入”。正常说话后若连续 2 秒没有检测到声音，会自动停止。输入框为空且未处于编辑状态时快捷键生效。", true);
	textBlock.Margin = new Thickness (0.0, 13.0, 0.0, 0.0);
	stackPanel3.Children.Add (textBlock);
	return SettingsPage (stackPanel);
}


private UIElement BuildHistorySettingsPage (Action close)
{
	StackPanel stackPanel = new StackPanel ();
	StackPanel stackPanel2 = Card (stackPanel, "\ue81c", "本机历史记录");
	stackPanel2.Children.Add (Label ("按需保留翻译记录，方便之后查看与复用。", true));
	Toggle (stackPanel2, "保存全部文字翻译历史", prefs.History, delegate(bool value) {
		prefs.History = value;
		SavePreferences ();
	});
	Toggle (stackPanel2, "保存最近 10 张图片翻译", prefs.ImageHistory, delegate(bool value) {
		prefs.ImageHistory = value;
		SavePreferences ();
	});
	StackPanel stackPanel3 = new StackPanel ();
	stackPanel3.Orientation = System.Windows.Controls.Orientation.Horizontal;
	stackPanel3.Margin = new Thickness (0.0, 14.0, 0.0, 0.0);
	StackPanel stackPanel4 = stackPanel3;
	System.Windows.Controls.Button button = OverlayButton ("查看文字记录", delegate {
		close ();
		TextHistoryDialog ();
	});
	button.Margin = new Thickness (0.0, 0.0, 8.0, 0.0);
	stackPanel4.Children.Add (button);
	System.Windows.Controls.Button button2 = OverlayButton ("查看图片记录", delegate {
		close ();
		ImageHistory ();
	});
	button2.Margin = new Thickness (0.0);
	stackPanel4.Children.Add (button2);
	stackPanel2.Children.Add (stackPanel4);
	return SettingsPage (stackPanel);
}


private UIElement BuildAboutPage ()
{
	StackPanel stackPanel = new StackPanel ();
	StackPanel stackPanel2 = Card (stackPanel, "\ue946", "关于与更新");
	stackPanel2.Children.Add (new TextBlock {
		Text = "当前版本  Yike for Windows · " + typeof(App).Assembly.GetName ().Version.ToString (),
		FontSize = 16.0,
		Foreground = OverlayBrush ("#EDF2F3"),
		Margin = new Thickness (0.0, 0.0, 0.0, 10.0)
	});
	stackPanel2.Children.Add (new TextBlock {
		Text = "开发者  本工具由null团队打造",
		FontSize = 16.0,
		Foreground = OverlayBrush ("#EDF2F3"),
		Margin = new Thickness (0.0, 0.0, 0.0, 12.0)
	});
	stackPanel2.Children.Add (Label ("一款专注于文字、截图与图片翻译的桌面工具。界面已统一为更清晰的深色圆角布局。", true));
	StackPanel stackPanel3 = Card (stackPanel, "\ue895", "检查更新");
	DockPanel dockPanel = new DockPanel ();
	TextBlock updateStatus = Label ("检查是否有新的稳定版本。", true);
	System.Windows.Controls.Button check = null;
	check = OverlayButton ("检查更新", async delegate {
		await CheckForUpdatesAsync (check, updateStatus);
	}, true);
	check.Margin = new Thickness (14.0, 0.0, 0.0, 0.0);
	DockPanel.SetDock (check, Dock.Right);
	dockPanel.Children.Add (check);
	dockPanel.Children.Add (updateStatus);
	stackPanel3.Children.Add (dockPanel);
	StackPanel stackPanel4 = Card (stackPanel, "\ue7e8", "退出应用");
	DockPanel dockPanel2 = new DockPanel ();
	System.Windows.Controls.Button element = OverlayButton ("退出 Yike", delegate {
		exiting = true;
		window.Close ();
	});
	DockPanel.SetDock (element, Dock.Right);
	dockPanel2.Children.Add (element);
	dockPanel2.Children.Add (Label ("关闭所有 Yike 窗口，停止菜单栏与全局快捷键。", true));
	stackPanel4.Children.Add (dockPanel2);
	return SettingsPage (stackPanel);
}


private StackPanel Card (StackPanel parent, string glyph, string title)
{
	StackPanel stackPanel = new StackPanel ();
	StackPanel stackPanel2 = IconText (glyph, title, 19.0);
	stackPanel2.Margin = new Thickness (0.0, 0.0, 0.0, 18.0);
	foreach (TextBlock item in stackPanel2.Children.OfType<TextBlock> ()) {
		item.Foreground = OverlayBrush ("#EEF3F4");
	}
	stackPanel2.Children.OfType<TextBlock> ().Last ().FontSize = 18.0;
	stackPanel2.Children.OfType<TextBlock> ().Last ().FontWeight = FontWeights.SemiBold;
	stackPanel.Children.Add (stackPanel2);
	Border border = new Border ();
	border.CornerRadius = new CornerRadius (20.0);
	border.Padding = new Thickness (25.0);
	border.Margin = new Thickness (0.0, 0.0, 0.0, 20.0);
	border.BorderThickness = new Thickness (1.0);
	border.Child = stackPanel;
	border.Background = OverlayBrush ("#781B2326");
	border.BorderBrush = OverlayBrush ("#354145");
	border.Effect = new DropShadowEffect {
		BlurRadius = 18.0,
		ShadowDepth = 3.0,
		Opacity = 0.12,
		Color = Colors.Black
	};
	Border element = border;
	parent.Children.Add (element);
	return stackPanel;
}


private Action BuildSpeechCard (StackPanel cards)
{
	StackPanel stackPanel = Card (cards, "\ue767", "朗读");
	Grid grid = new Grid ();
	grid.Margin = new Thickness (0.0, 0.0, 0.0, 12.0);
	Grid grid2 = grid;
	grid2.ColumnDefinitions.Add (new ColumnDefinition {
		Width = new GridLength (64.0)
	});
	grid2.ColumnDefinitions.Add (new ColumnDefinition ());
	grid2.ColumnDefinitions.Add (new ColumnDefinition {
		Width = new GridLength (68.0)
	});
	grid2.Children.Add (Label ("语速"));
	TextBlock speedLabel = Label ((prefs.Rate == 0) ? "标准" : prefs.Rate.ToString ("+0;-0"));
	speedLabel.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
	Grid.SetColumn (speedLabel, 2);
	grid2.Children.Add (speedLabel);
	Slider speed = new Slider {
		Minimum = -10.0,
		Maximum = 10.0,
		Value = Math.Max (-10, Math.Min (10, prefs.Rate)),
		TickFrequency = 1.0,
		IsSnapToTickEnabled = true,
		VerticalAlignment = VerticalAlignment.Center,
		Margin = new Thickness (8.0, 0.0, 12.0, 0.0)
	};
	Grid.SetColumn (speed, 1);
	grid2.Children.Add (speed);
	speed.ValueChanged += delegate {
		prefs.Rate = (int)speed.Value;
		speedLabel.Text = ((prefs.Rate == 0) ? "标准" : prefs.Rate.ToString ("+0;-0"));
		SavePreferences ();
	};
	stackPanel.Children.Add (grid2);
	Row (stackPanel, "音色").Children.Add (Segments (new string[2] { "女声", "男声" }, (prefs.VoiceGender == "male") ? 1 : 0, delegate(int i) {
		prefs.VoiceGender = ((i == 1) ? "male" : "female");
		SavePreferences ();
	}));
	Toggle (stackPanel, "在线自然语音（推荐，无需安装语音包）", prefs.OnlineSpeech, delegate(bool value) {
		speech.Stop ();
		prefs.OnlineSpeech = value;
		SavePreferences ();
	});
	TextBlock textBlock = Label ("在线自然语音：中文云希男声 / 晓晓女声，各语言均有男女声。", true);
	textBlock.Margin = new Thickness (0.0, 10.0, 0.0, 8.0);
	stackPanel.Children.Add (textBlock);
	StackPanel stackPanel2 = Row (stackPanel, "");
	stackPanel2.Children.Clear ();
	stackPanel2.Children.Add (OverlayButton ("试听音色", delegate {
		SpeakText ("你好，这是当前选择的朗读声音。", "ZH-HANS", true);
	}));
	System.Windows.Controls.Button pause = OverlayButton ("暂停 / 继续", delegate {
		speech.TogglePause ();
	});
	stackPanel2.Children.Add (pause);
	Action refresh = delegate {
		pause.Content = ((speech.State == "paused") ? "继续" : "暂停");
		pause.IsEnabled = speech.State == "playing" || speech.State == "paused";
	};
	speech.Changed += refresh;
	refresh ();
	stackPanel2.Children.Add (OverlayButton ("停止", delegate {
		speech.Stop ();
	}));
	stackPanel2.Children.Add (OverlayButton ("管理语音包", delegate {
		OpenSystem ("ms-settings:speech");
	}));
	return delegate {
		speech.Changed -= refresh;
	};
}


private void BuildOcrCard (StackPanel cards, Action close)
{
	StackPanel stackPanel = Card (cards, "\ue91b", "OCR 识图");
	stackPanel.Children.Add (Label ("本机识别图片文字，可编辑、复制或继续翻译。无需 DeepL 密钥。", true));
	System.Windows.Controls.ComboBox language = new System.Windows.Controls.ComboBox {
		MinWidth = 200.0
	};
	for (int i = 0; i < ocrCodes.Length; i++) {
		language.Items.Add (new ComboBoxItem {
			Content = ocrNames [i],
			Tag = ocrCodes [i]
		});
	}
	Select (language, prefs.OcrLanguage);
	language.SelectionChanged += delegate {
		prefs.OcrLanguage = Code (language);
		SavePreferences ();
	};
	Row (stackPanel, "识别语言").Children.Add (language);
	StackPanel stackPanel2 = Row (stackPanel, "");
	stackPanel2.Children.Clear ();
	stackPanel2.Children.Add (OverlayButton ("选择图片识别", delegate {
		close ();
		OpenOcr ();
	}));
	stackPanel2.Children.Add (OverlayButton ("截图识别", delegate {
		close ();
		Capture (true);
	}));
	stackPanel2.Children.Add (OverlayButton ("安装语言包", delegate {
		OpenSystem ("ms-settings:regionlanguage");
	}));
}


private void BuildPermissionCard (StackPanel cards)
{
	StackPanel parent = Card (cards, "\ue72e", "系统与权限");
	PermissionRow (parent, "划词翻译", "Ctrl+Shift+F · 松键后取词", null);
	PermissionRow (parent, "语音输入 / 麦克风", "需允许桌面应用访问", "ms-settings:privacy-microphone");
	PermissionRow (parent, "系统语音输入", "手动使用 Win+H 时需要", "ms-settings:privacy-speech");
	PermissionRow (parent, "OCR 语言组件", "使用已安装的系统语言", "ms-settings:regionlanguage");
}


private TextBlock Label (string text, bool muted = false)
{
	TextBlock textBlock = new TextBlock ();
	textBlock.Text = text;
	textBlock.TextWrapping = TextWrapping.Wrap;
	textBlock.VerticalAlignment = VerticalAlignment.Center;
	textBlock.Foreground = OverlayBrush (muted ? "#929DA0" : "#EDF2F3");
	return textBlock;
}


private StackPanel Row (StackPanel parent, string caption)
{
	StackPanel stackPanel = new StackPanel ();
	stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
	stackPanel.Margin = new Thickness (0.0, 6.0, 0.0, 6.0);
	StackPanel stackPanel2 = stackPanel;
	TextBlock textBlock = Label (caption);
	textBlock.MinWidth = 64.0;
	textBlock.Margin = new Thickness (0.0, 0.0, 12.0, 0.0);
	stackPanel2.Children.Add (textBlock);
	parent.Children.Add (stackPanel2);
	return stackPanel2;
}


private FrameworkElement Segments (string[] labels, int selected, Action<int> changed)
{
	StackPanel stackPanel = new StackPanel ();
	stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
	StackPanel stackPanel2 = stackPanel;
	List<System.Windows.Controls.Button> buttons = new List<System.Windows.Controls.Button> ();
	Action<int> paint = delegate(int num2) {
		for (int i = 0; i < buttons.Count; i++) {
			buttons [i].Background = OverlayBrush ((i == num2) ? "#1677F2" : "#2A3336");
			buttons [i].Foreground = ((i == num2) ? System.Windows.Media.Brushes.White : OverlayBrush ("#E2E8EA"));
		}
	};
	for (int num = 0; num < labels.Length; num++) {
		int index = num;
		System.Windows.Controls.Button button = new System.Windows.Controls.Button ();
		button.Content = labels [num];
		button.MinWidth = 76.0;
		button.Padding = new Thickness (15.0, 8.0, 15.0, 8.0);
		button.Margin = new Thickness (1.0);
		button.FontSize = 14.0;
		System.Windows.Controls.Button button2 = button;
		button2.Click += delegate {
			paint (index);
			changed (index);
		};
		buttons.Add (button2);
		stackPanel2.Children.Add (button2);
	}
	paint (selected);
	Border border = new Border ();
	border.CornerRadius = new CornerRadius (12.0);
	border.Padding = new Thickness (2.0);
	border.Child = stackPanel2;
	border.Background = OverlayBrush ("#2A3336");
	return border;
}


private void OpenSystem (string uri)
{
	try {
		ProcessStartInfo processStartInfo = new ProcessStartInfo (uri);
		processStartInfo.UseShellExecute = true;
		Process.Start (processStartInfo);
	} catch (Exception ex) {
		Status ("无法打开系统设置：" + ex.Message);
	}
}


private void PermissionRow (StackPanel parent, string title, string state, string uri)
{
	DockPanel dockPanel = new DockPanel ();
	dockPanel.Margin = new Thickness (0.0, 7.0, 0.0, 7.0);
	DockPanel dockPanel2 = dockPanel;
	if (uri != null) {
		System.Windows.Controls.Button element = OverlayButton ("系统设置", delegate {
			OpenSystem (uri);
		});
		DockPanel.SetDock (element, Dock.Right);
		dockPanel2.Children.Add (element);
	}
	TextBlock textBlock = Label (state, true);
	textBlock.Margin = new Thickness (12.0, 0.0, 12.0, 0.0);
	DockPanel.SetDock (textBlock, Dock.Right);
	dockPanel2.Children.Add (textBlock);
	dockPanel2.Children.Add (Label (title));
	parent.Children.Add (dockPanel2);
}

}
}
