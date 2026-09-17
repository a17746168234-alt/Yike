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
public sealed class CredentialFeedback
{
	private readonly Border border;

	private readonly TextBlock icon;

	private readonly TextBlock message;

	public FrameworkElement View {
		get {
			return border;
		}
	}

	public CredentialFeedback ()
	{
		icon = new TextBlock {
			FontFamily = new System.Windows.Media.FontFamily ("Segoe Fluent Icons"),
			FontSize = 14.0,
			Margin = new Thickness (0.0, 0.0, 9.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		message = new TextBlock {
			FontSize = 13.0,
			TextWrapping = TextWrapping.Wrap,
			VerticalAlignment = VerticalAlignment.Center
		};
		StackPanel stackPanel = new StackPanel {
			Orientation = System.Windows.Controls.Orientation.Horizontal
		};
		stackPanel.Children.Add (icon);
		stackPanel.Children.Add (message);
		border = new Border {
			CornerRadius = new CornerRadius (11.0),
			Padding = new Thickness (13.0, 10.0, 13.0, 10.0),
			Margin = new Thickness (0.0, 10.0, 0.0, 0.0),
			Visibility = Visibility.Collapsed,
			Child = stackPanel
		};
	}

	public void Show (bool success, string text)
	{
		icon.Text = (success ? "\ue73e" : "\uea39");
		message.Text = text;
		SolidColorBrush solidColorBrush = new SolidColorBrush ((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString (success ? "#B9F6D0" : "#FFD0D2"));
		TextBlock textBlock = icon;
		System.Windows.Media.Brush foreground = (message.Foreground = solidColorBrush);
		textBlock.Foreground = foreground;
		border.Background = new SolidColorBrush ((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString (success ? "#2539A768" : "#2BFF5A5F"));
		border.BorderBrush = new SolidColorBrush ((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString (success ? "#6045C77C" : "#60FF7478"));
		border.BorderThickness = new Thickness (1.0);
		border.Visibility = Visibility.Visible;
	}

	public void Working (string text)
	{
		icon.Text = "\ue895";
		message.Text = text;
		TextBlock textBlock = icon;
		System.Windows.Media.Brush foreground = (message.Foreground = new SolidColorBrush ((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString ("#B9D9FF")));
		textBlock.Foreground = foreground;
		border.Background = new SolidColorBrush ((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString ("#253477B3"));
		border.BorderBrush = new SolidColorBrush ((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString ("#605D9BD3"));
		border.BorderThickness = new Thickness (1.0);
		border.Visibility = Visibility.Visible;
	}
}

}
