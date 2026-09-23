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
	string previous = prefs.TranslationEngine;
	prefs.TranslationEngine = TranslationEngines.Public;
	EngineMenu (true);
	prefs.TranslationEngine = previous;
	ApplyAccountIdentity ();
}


private void EngineMenu (bool preview)
{
	if (enginePopup != null) enginePopup.IsOpen = false;
	string selected = SelectedTranslationEngine ();
	TextBlock usageValue;
	TextBlock usageHint;
	System.Windows.Controls.ProgressBar progress;
	Border card = BuildEngineMenuCard (selected, out usageValue, out usageHint, out progress);
	Popup popup = new Popup {
		PlacementTarget = Find<System.Windows.Controls.Button> ("EngineButton"),
		Placement = PlacementMode.Bottom,
		HorizontalOffset = -238.0,
		VerticalOffset = 8.0,
		AllowsTransparency = true,
		PopupAnimation = PopupAnimation.Fade,
		StaysOpen = false,
		Child = card
	};
	enginePopup = popup;
	popup.Closed += delegate { if (object.ReferenceEquals (enginePopup, popup)) enginePopup = null; };
	popup.IsOpen = true;
	if (selected == TranslationEngines.Personal && !string.IsNullOrWhiteSpace (Store.Key) && !preview) {
		RefreshPersonalEngineUsage (usageValue, usageHint, progress);
	}
	if (preview) {
		card.Measure (new System.Windows.Size (370.0, double.PositiveInfinity));
		card.Arrange (new Rect (0.0, 0.0, card.DesiredSize.Width, card.DesiredSize.Height));
		card.UpdateLayout ();
		RenderTargetBitmap bitmap = new RenderTargetBitmap ((int)Math.Ceiling (card.ActualWidth), (int)Math.Ceiling (card.ActualHeight), 96.0, 96.0, PixelFormats.Pbgra32);
		bitmap.Render (card);
		ImageFiles.Save (bitmap, System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "engine-menu.png"));
		popup.IsOpen = false;
	}
}


private Border BuildEngineMenuCard (string selected, out TextBlock usageValue, out TextBlock usageHint, out System.Windows.Controls.ProgressBar progress)
{
	StackPanel body = new StackPanel ();
	body.Children.Add (EngineChoiceButton (TranslationEngines.Public, "\ue734", "DeepL 高质量翻译", "使用注册送的体验额度", selected));
	body.Children.Add (EngineChoiceButton (TranslationEngines.Personal, "\ue72e", "DeepL（个人接入）", "使用自己的 API 密钥与额度", selected));
	body.Children.Add (new Border { Height = 1.0, Margin = new Thickness (9.0, 7.0, 9.0, 13.0), Background = OverlayBrush ("#354044") });
	Grid quotaHeader = new Grid { Margin = new Thickness (14.0, 0.0, 14.0, 0.0) };
	quotaHeader.ColumnDefinitions.Add (new ColumnDefinition ());
	quotaHeader.ColumnDefinitions.Add (new ColumnDefinition { Width = GridLength.Auto });
	TextBlock quotaTitle = new TextBlock { Text = selected == TranslationEngines.Personal ? "个人额度" : "体验额度", FontSize = 12.0, FontWeight = FontWeights.SemiBold, Foreground = OverlayBrush ("#E7ECEE") };
	usageValue = new TextBlock { FontSize = 12.0, Foreground = OverlayBrush ("#929DA0"), HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
	Grid.SetColumn (usageValue, 1);
	quotaHeader.Children.Add (quotaTitle);
	quotaHeader.Children.Add (usageValue);
	body.Children.Add (quotaHeader);
	progress = new System.Windows.Controls.ProgressBar {
		Height = 7.0, Minimum = 0.0, Maximum = 100.0, Value = 0.0,
		Margin = new Thickness (14.0, 13.0, 14.0, 8.0),
		Foreground = OverlayBrush ("#1677F2"), Background = OverlayBrush ("#303A3D"), BorderThickness = new Thickness (0.0)
	};
	body.Children.Add (progress);
	Grid footer = new Grid { Margin = new Thickness (14.0, 0.0, 9.0, 4.0) };
	footer.ColumnDefinitions.Add (new ColumnDefinition ());
	footer.ColumnDefinitions.Add (new ColumnDefinition { Width = GridLength.Auto });
	usageHint = new TextBlock { FontSize = 11.0, Foreground = OverlayBrush ("#8E999D"), VerticalAlignment = VerticalAlignment.Center };
	footer.Children.Add (usageHint);
	string actionText = selected == TranslationEngines.Personal ? "更新 DeepL 密钥" : "账号与安全";
	System.Windows.Controls.Button action = new System.Windows.Controls.Button {
		Content = actionText, Padding = new Thickness (9.0, 6.0, 9.0, 6.0), Margin = new Thickness (8.0, 0.0, 0.0, 0.0),
		Foreground = OverlayBrush ("#2789F7"), Background = System.Windows.Media.Brushes.Transparent,
		FontSize = 12.0, FontWeight = FontWeights.SemiBold
	};
	action.Click += delegate {
		if (enginePopup != null) enginePopup.IsOpen = false;
		ShowSettings (false, selected == TranslationEngines.Public ? "account" : "deepl");
	};
	Grid.SetColumn (action, 1);
	footer.Children.Add (action);
	body.Children.Add (footer);
	if (selected == TranslationEngines.Public && remoteSession != null) {
		long granted = Math.Max (1L, remoteSession.Granted);
		long used = Math.Max (0L, granted - remoteSession.Remaining);
		progress.Maximum = granted;
		progress.Value = Math.Min (granted, used);
		usageValue.Text = used.ToString ("N0") + " / " + remoteSession.Granted.ToString ("N0") + " 字符";
		usageHint.Text = "已用 " + Math.Round ((double)used * 100.0 / granted, 1) + "% · 剩余 " + remoteSession.Remaining.ToString ("N0");
	} else if (selected == TranslationEngines.Personal && !string.IsNullOrWhiteSpace (Store.Key)) {
		usageValue.Text = "正在查询…";
		usageHint.Text = "额度由你的 DeepL 账户提供";
	} else if (selected == TranslationEngines.Personal) {
		usageValue.Text = "尚未配置";
		usageHint.Text = "请先添加自己的 DeepL 密钥";
	} else if (selected == TranslationEngines.Public) {
		usageValue.Text = "尚未登录";
		usageHint.Text = "登录后可使用赠送额度";
	} else {
		quotaTitle.Text = "选择翻译引擎";
		usageValue.Text = "尚未选择";
		usageHint.Text = "选择后会记住，不会自动切换额度";
		progress.Visibility = Visibility.Collapsed;
		action.Content = "了解与帮助";
	}
	Border card = new Border {
		Width = 370.0, CornerRadius = new CornerRadius (18.0), Padding = new Thickness (9.0, 9.0, 9.0, 10.0),
		Background = OverlayBrush ("#F1171F22"), BorderBrush = OverlayBrush ("#354044"), BorderThickness = new Thickness (1.0),
		Child = body,
		Effect = new DropShadowEffect { BlurRadius = 24.0, ShadowDepth = 7.0, Direction = 270.0, Opacity = 0.28, Color = Colors.Black }
	};
	return card;
}


private System.Windows.Controls.Button EngineChoiceButton (string engine, string glyph, string title, string detail, string selected)
{
	bool active = engine == selected;
	Grid grid = new Grid ();
	grid.ColumnDefinitions.Add (new ColumnDefinition { Width = new GridLength (38.0) });
	grid.ColumnDefinitions.Add (new ColumnDefinition ());
	grid.ColumnDefinitions.Add (new ColumnDefinition { Width = new GridLength (28.0) });
	TextBlock icon = Icon (glyph, 17.0);
	icon.Foreground = active ? OverlayBrush ("#2789F7") : OverlayBrush ("#929DA0");
	icon.VerticalAlignment = VerticalAlignment.Center;
	grid.Children.Add (icon);
	StackPanel labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
	labels.Children.Add (new TextBlock { Text = title, FontSize = 14.0, FontWeight = FontWeights.SemiBold, Foreground = OverlayBrush ("#EDF2F3") });
	labels.Children.Add (new TextBlock { Text = detail, FontSize = 10.5, Margin = new Thickness (0.0, 3.0, 0.0, 0.0), Foreground = OverlayBrush ("#929DA0") });
	Grid.SetColumn (labels, 1);
	grid.Children.Add (labels);
	TextBlock check = Icon ("\ue73e", 15.0);
	check.Foreground = OverlayBrush ("#2789F7");
	check.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
	check.VerticalAlignment = VerticalAlignment.Center;
	check.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
	Grid.SetColumn (check, 2);
	grid.Children.Add (check);
	System.Windows.Controls.Button button = new System.Windows.Controls.Button {
		Tag = "engine-choice-" + engine, Content = grid, Height = 62.0, Padding = new Thickness (10.0, 5.0, 8.0, 5.0), Margin = new Thickness (0.0, 1.0, 0.0, 1.0),
		HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
		Background = active ? OverlayBrush ("#253C56") : System.Windows.Media.Brushes.Transparent,
		BorderThickness = new Thickness (0.0)
	};
	button.Click += delegate { SelectTranslationEngine (engine); };
	return button;
}


private async void RefreshPersonalEngineUsage (TextBlock value, TextBlock hint, System.Windows.Controls.ProgressBar progress)
{
	try {
		Dictionary<string, object> data = Store.Json.Deserialize<Dictionary<string, object>> (await new DeepL (Store.Key).Request ("/v2/usage", null, CancellationToken.None));
		long limit = Convert.ToInt64 (data ["character_limit"]);
		long used = Convert.ToInt64 (data ["character_count"]);
		progress.Maximum = Math.Max (1L, limit);
		progress.Value = Math.Min (limit, used);
		value.Text = used.ToString ("N0") + " / " + limit.ToString ("N0") + " 字符";
		hint.Text = "已用 " + Math.Round ((double)used * 100.0 / Math.Max (1L, limit), 1) + "%";
	} catch (Exception ex) {
		value.Text = "暂时无法查询";
		hint.Text = ex.Message;
	}
}


private string SelectedTranslationEngine ()
{
	string selected = TranslationEngines.Normalize (prefs == null ? null : prefs.TranslationEngine);
	if (!string.IsNullOrWhiteSpace (selected)) return selected;
	bool publicReady = remoteSession != null;
	bool personalReady = !string.IsNullOrWhiteSpace (Store.Key);
	if (publicReady != personalReady) return publicReady ? TranslationEngines.Public : TranslationEngines.Personal;
	return "";
}


private void SelectTranslationEngine (string engine)
{
	engine = TranslationEngines.Normalize (engine);
	if (string.IsNullOrWhiteSpace (engine)) return;
	prefs.TranslationEngine = engine;
	SavePreferences ();
	if (enginePopup != null) enginePopup.IsOpen = false;
	ApplyAccountIdentity ();
	Status (engine == TranslationEngines.Public ? "已选择 DeepL 高质量翻译 · 使用赠送额度" : "已选择 DeepL（个人接入）· 使用自己的密钥");
}


private void UpdateEngineButtonContent ()
{
	if (window == null) return;
	System.Windows.Controls.Button button = Find<System.Windows.Controls.Button> ("EngineButton");
	TextBlock text = new TextBlock { Height = 22.0, LineHeight = 18.0, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, SnapsToDevicePixels = true };
	text.Inlines.Add (new Run ("●") { FontFamily = new System.Windows.Media.FontFamily ("Microsoft YaHei UI"), Foreground = new SolidColorBrush (System.Windows.Media.Color.FromRgb (34, 197, 94)), FontSize = 10.0, BaselineAlignment = BaselineAlignment.Center });
	text.Inlines.Add (new Run ("  DeepL  ") { FontFamily = new System.Windows.Media.FontFamily ("Microsoft YaHei UI"), FontSize = 13.0, FontWeight = FontWeights.SemiBold, BaselineAlignment = BaselineAlignment.Center });
	text.Inlines.Add (new Run ("\ue70d") { FontFamily = new System.Windows.Media.FontFamily ("Segoe Fluent Icons"), Foreground = OverlayBrush ("#929DA0"), FontSize = 10.0, BaselineAlignment = BaselineAlignment.Center });
	button.Content = text;
	button.ToolTip = "选择翻译引擎与额度";
	button.VerticalContentAlignment = VerticalAlignment.Center;
}


private UIElement BuildDeepLSettingsPage (bool preview)
{
	StackPanel stackPanel = new StackPanel ();
	StackPanel engines = Card (stackPanel, "\ue774", "两种引擎如何工作");
	TextBlock publicTitle = Label ("DeepL 高质量翻译");
	publicTitle.FontSize = 16.0;
	publicTitle.FontWeight = FontWeights.SemiBold;
	engines.Children.Add (publicTitle);
	TextBlock publicDescription = Label ("登录 Yike 账号后使用，无需填写个人密钥。待翻译文字通过 Yike 服务交由 DeepL 处理，消耗账号中的赠送额度，需要联网。", true);
	publicDescription.Margin = new Thickness (0.0, 7.0, 0.0, 16.0);
	engines.Children.Add (publicDescription);
	engines.Children.Add (new Border { Height = 1.0, Margin = new Thickness (0.0, 0.0, 0.0, 16.0), Background = OverlayBrush ("#354044") });
	TextBlock personalTitle = Label ("DeepL（个人接入）");
	personalTitle.FontSize = 16.0;
	personalTitle.FontWeight = FontWeights.SemiBold;
	engines.Children.Add (personalTitle);
	TextBlock personalDescription = Label ("使用你自己的 DeepL API 密钥，文字由本机直接发送到 DeepL 在线翻译，消耗个人账户额度。密钥由 Windows 数据保护加密并只保存在本机。", true);
	personalDescription.Margin = new Thickness (0.0, 7.0, 0.0, 10.0);
	engines.Children.Add (personalDescription);
	engines.Children.Add (Label ("两种引擎由你在主界面明确选择；选择会被记住，失败时不会自动改用另一种额度。", true));
	StackPanel stackPanel2 = Card (stackPanel, "\ue72e", "个人 DeepL 接入");
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
				ApplyAccountIdentity ();
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
		ApplyAccountIdentity ();
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
