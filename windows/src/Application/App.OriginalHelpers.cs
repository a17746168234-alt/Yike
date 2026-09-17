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
private T Find<T> (string name) where T : FrameworkElement
{
	return (T)window.FindName (name);
}


private void ApplyAccountIdentity ()
{
	if (window != null) {
		AccountProfile current = accounts.Current;
		BitmapSource bitmapSource = AvatarImages.Load (current);
		System.Windows.Controls.Image image = Find<System.Windows.Controls.Image> ("AppIcon");
		image.Source = bitmapSource ?? defaultAppIcon;
		image.Stretch = ((bitmapSource == null) ? Stretch.Uniform : Stretch.UniformToFill);
		image.Clip = ((bitmapSource == null) ? null : new EllipseGeometry (new System.Windows.Point (20.0, 20.0), 20.0, 20.0));
		Find<TextBlock> ("AppDisplayName").Text = ((current == null) ? "Yike" : current.DisplayName);
		window.Icon = bitmapSource ?? defaultAppIcon;
		if (tray != null) {
			string text = ((current == null) ? "Yike" : current.DisplayName);
			tray.Text = ((text.Length > 63) ? text.Substring (0, 63) : text);
		}
	}
}


private void BuildDeepLHelp (StackPanel cards)
{
	StackPanel stackPanel = Card (cards, "\ue946", "快速获取 DeepL 密钥");
	Border border = new Border ();
	border.CornerRadius = new CornerRadius (13.0);
	border.Background = OverlayBrush ("#172B3D");
	border.Padding = new Thickness (15.0, 12.0, 15.0, 12.0);
	border.Margin = new Thickness (0.0, 0.0, 0.0, 20.0);
	border.Child = new TextBlock {
		Text = "DeepL 目前将新用户引导至免费的 API Developer 注册页；旧 API Free 已停止新购。已有 API Free 用户仍可使用原密钥，通常以 :fx 结尾。",
		Foreground = OverlayBrush ("#AFCBE5"),
		TextWrapping = TextWrapping.Wrap,
		FontSize = 13.0
	};
	Border element = border;
	stackPanel.Children.Add (element);
	AddDeepLStep (stackPanel, "1", "注册免费的 DeepL API", "打开 DeepL 官方注册页，按提示创建或登录账户并选择可用的免费开发者 API。", "注册 / 获取免费 API", "https://www.deepl.com/en/signup?cta=checkout&is_api=true&productId=api-developer", "\ue77b");
	AddDeepLStep (stackPanel, "2", "登录账户并找到密钥", "进入账户的 API Keys 页面，创建或复制完整密钥。请勿向其他人公开密钥。", "登录账户 / 打开我的密钥", "https://www.deepl.com/your-account/keys", "\ue72e");
	AddDeepLStep (stackPanel, "3", "回到 Yike 填写", "把完整密钥粘贴到上方输入框，点击“保存密钥”，再点击“查询额度”确认连接成功。", null, null, null);
	stackPanel.Children.Add (DeepLLinkCard ("官方密钥说明与常见问题", "https://support.deepl.com/hc/en-us/articles/360020695820-API-key-for-DeepL-API", "\ue946"));
	stackPanel.Children.Add (DeepLLinkCard ("DeepL API 认证文档", "https://developers.deepl.com/docs/getting-started/auth", "\ue946"));
}


private void AddDeepLStep (StackPanel parent, string number, string title, string description, string linkTitle, string url, string glyph)
{
	Grid grid = new Grid ();
	grid.Margin = new Thickness (0.0, 0.0, 0.0, 20.0);
	Grid grid2 = grid;
	grid2.ColumnDefinitions.Add (new ColumnDefinition {
		Width = new GridLength (54.0)
	});
	grid2.ColumnDefinitions.Add (new ColumnDefinition ());
	Border border = new Border ();
	border.Width = 38.0;
	border.Height = 38.0;
	border.CornerRadius = new CornerRadius (19.0);
	border.Background = OverlayBrush ("#183758");
	border.VerticalAlignment = VerticalAlignment.Top;
	border.Child = new TextBlock {
		Text = number,
		Foreground = OverlayBrush ("#2388FA"),
		FontSize = 17.0,
		FontWeight = FontWeights.SemiBold,
		HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
		VerticalAlignment = VerticalAlignment.Center
	};
	Border element = border;
	grid2.Children.Add (element);
	StackPanel stackPanel = new StackPanel ();
	Grid.SetColumn (stackPanel, 1);
	grid2.Children.Add (stackPanel);
	stackPanel.Children.Add (new TextBlock {
		Text = title,
		Foreground = OverlayBrush ("#EDF2F3"),
		FontSize = 16.0,
		FontWeight = FontWeights.SemiBold
	});
	stackPanel.Children.Add (new TextBlock {
		Text = description,
		Foreground = OverlayBrush ("#9AA6A9"),
		FontSize = 13.0,
		TextWrapping = TextWrapping.Wrap,
		Margin = new Thickness (0.0, 7.0, 0.0, 0.0)
	});
	if (!string.IsNullOrWhiteSpace (url)) {
		System.Windows.Controls.Button button = DeepLLinkCard (linkTitle, url, glyph);
		button.Margin = new Thickness (0.0, 13.0, 0.0, 0.0);
		stackPanel.Children.Add (button);
	}
	parent.Children.Add (grid2);
}


private System.Windows.Controls.Button DeepLLinkCard (string title, string url, string glyph)
{
	Grid grid = new Grid ();
	grid.ColumnDefinitions.Add (new ColumnDefinition {
		Width = new GridLength (42.0)
	});
	grid.ColumnDefinitions.Add (new ColumnDefinition ());
	grid.ColumnDefinitions.Add (new ColumnDefinition {
		Width = new GridLength (28.0)
	});
	TextBlock textBlock = Icon (glyph, 20.0);
	textBlock.Foreground = OverlayBrush ("#2489FA");
	textBlock.VerticalAlignment = VerticalAlignment.Center;
	grid.Children.Add (textBlock);
	StackPanel stackPanel = new StackPanel ();
	stackPanel.VerticalAlignment = VerticalAlignment.Center;
	StackPanel stackPanel2 = stackPanel;
	stackPanel2.Children.Add (new TextBlock {
		Text = title,
		Foreground = OverlayBrush ("#2789F7"),
		FontSize = 14.0,
		FontWeight = FontWeights.SemiBold
	});
	stackPanel2.Children.Add (new TextBlock {
		Text = url,
		Foreground = OverlayBrush ("#3475B3"),
		FontSize = 11.0,
		Margin = new Thickness (0.0, 3.0, 0.0, 0.0),
		TextTrimming = TextTrimming.CharacterEllipsis
	});
	Grid.SetColumn (stackPanel2, 1);
	grid.Children.Add (stackPanel2);
	TextBlock textBlock2 = Icon ("\ue72a", 15.0);
	textBlock2.Foreground = OverlayBrush ("#2789F7");
	textBlock2.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
	Grid.SetColumn (textBlock2, 2);
	grid.Children.Add (textBlock2);
	System.Windows.Controls.Button button = new System.Windows.Controls.Button ();
	button.Content = grid;
	button.Height = 66.0;
	button.Padding = new Thickness (14.0, 8.0, 12.0, 8.0);
	button.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch;
	button.Background = OverlayBrush ("#142A3C");
	button.BorderBrush = OverlayBrush ("#26445D");
	button.BorderThickness = new Thickness (1.0);
	button.ToolTip = "在浏览器中打开 " + url;
	System.Windows.Controls.Button button2 = button;
	button2.Click += delegate {
		OpenWeb (url);
	};
	return button2;
}

}
}
