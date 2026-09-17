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
internal TextBlock Icon (string glyph, double size = 16.0)
{
	TextBlock textBlock = new TextBlock ();
	textBlock.Text = glyph;
	textBlock.FontFamily = new System.Windows.Media.FontFamily ("Segoe Fluent Icons");
	textBlock.FontSize = size;
	textBlock.Width = size + 5.0;
	textBlock.Height = size + 7.0;
	textBlock.LineHeight = size + 5.0;
	textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
	textBlock.Padding = new Thickness (0.0, 1.0, 0.0, 0.0);
	textBlock.VerticalAlignment = VerticalAlignment.Center;
	textBlock.TextAlignment = TextAlignment.Center;
	textBlock.ClipToBounds = false;
	return textBlock;
}


internal StackPanel IconText (string glyph, string text, double size = 16.0)
{
	StackPanel stackPanel = new StackPanel ();
	stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
	stackPanel.VerticalAlignment = VerticalAlignment.Center;
	StackPanel stackPanel2 = stackPanel;
	stackPanel2.Children.Add (Icon (glyph, size));
	TextBlock textBlock = new TextBlock ();
	textBlock.Text = text;
	textBlock.Margin = new Thickness (7.0, 0.0, 0.0, 0.0);
	textBlock.Height = size + 7.0;
	textBlock.LineHeight = size + 5.0;
	textBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
	textBlock.Padding = new Thickness (0.0, 1.0, 0.0, 0.0);
	textBlock.VerticalAlignment = VerticalAlignment.Center;
	TextBlock element = textBlock;
	stackPanel2.Children.Add (element);
	return stackPanel2;
}


internal TextBlock ButtonIconText (string glyph, string text, double size = 15.0)
{
	TextBlock textBlock = new TextBlock ();
	textBlock.VerticalAlignment = VerticalAlignment.Center;
	textBlock.TextAlignment = TextAlignment.Center;
	textBlock.LineStackingStrategy = LineStackingStrategy.MaxHeight;
	textBlock.SnapsToDevicePixels = true;
	TextBlock textBlock2 = textBlock;
	textBlock2.Inlines.Add (new Run (glyph) {
		FontFamily = new System.Windows.Media.FontFamily ("Segoe Fluent Icons"),
		FontSize = size,
		BaselineAlignment = BaselineAlignment.Center
	});
	textBlock2.Inlines.Add (new Run ("\u2002"));
	textBlock2.Inlines.Add (new Run (text) {
		FontFamily = new System.Windows.Media.FontFamily ("Microsoft YaHei UI"),
		FontSize = 13.0,
		BaselineAlignment = BaselineAlignment.Center
	});
	return textBlock2;
}


internal void SetButtonIcon (string name, string glyph, string text)
{
	System.Windows.Controls.Button button = Find<System.Windows.Controls.Button> (name);
	button.Content = ButtonIconText (glyph, text);
	button.VerticalContentAlignment = VerticalAlignment.Center;
}


internal void SetButtonIconOnly (string name, string glyph, string tooltip, double size = 18.0)
{
	System.Windows.Controls.Button button = Find<System.Windows.Controls.Button> (name);
	button.Content = Icon (glyph, size);
	button.ToolTip = tooltip;
	button.Padding = new Thickness (0.0);
	button.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
	button.VerticalContentAlignment = VerticalAlignment.Center;
}


private Window Dialog (string title, double width, double height, UIElement content)
{
	Window window = new Window ();
	window.Width = width;
	window.Height = height;
	window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
	window.FontFamily = this.window.FontFamily;
	Window window2 = window;
	DialogChrome.Apply (window2, this.window, title, content);
	return window2;
}


private System.Windows.Media.Brush OverlayBrush (string color)
{
	return ThemeBrush (IsDarkAppearance () ? color : LightOverlayColor (color));
}


private Window OverlayDialog (string title, double width, double height, UIElement content, double radius = 28.0)
{
	Window window = new Window ();
	window.Width = width;
	window.Height = height;
	window.MinWidth = Math.Min (520.0, width);
	window.MinHeight = Math.Min (360.0, height);
	window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
	window.Foreground = OverlayBrush ("#F4F7F8");
	window.FontFamily = this.window.FontFamily;
	Window window2 = window;
	DialogChrome.Apply (window2, this.window, title, content, radius);
	return window2;
}


private void ShowDimmed (Window dialog)
{
	double opacity = window.Opacity;
	window.Opacity = 0.34;
	try {
		dialog.ShowDialog ();
	} finally {
		window.Opacity = opacity;
	}
}


private System.Windows.Controls.Button OverlayButton (string text, Action action, bool primary = false)
{
	System.Windows.Controls.Button button = new System.Windows.Controls.Button ();
	button.Content = text;
	button.Padding = new Thickness (17.0, 9.0, 17.0, 9.0);
	button.Margin = new Thickness (8.0, 0.0, 0.0, 0.0);
	button.MinHeight = 38.0;
	button.Foreground = (primary ? System.Windows.Media.Brushes.White : OverlayBrush ("#E2E8EA"));
	button.Background = OverlayBrush (primary ? "#1677F2" : "#2A3336");
	button.BorderThickness = new Thickness (0.0);
	button.FontSize = 14.0;
	System.Windows.Controls.Button button2 = button;
	button2.Click += delegate {
		action ();
	};
	return button2;
}


private System.Windows.Controls.Button ActionButton (string text, Action action)
{
	System.Windows.Controls.Button button = new System.Windows.Controls.Button ();
	button.Content = text;
	button.Padding = new Thickness (13.0, 7.0, 13.0, 7.0);
	button.Margin = new Thickness (0.0, 4.0, 8.0, 4.0);
	button.MinHeight = 34.0;
	button.BorderThickness = new Thickness (1.0);
	System.Windows.Controls.Button button2 = button;
	button2.SetResourceReference (System.Windows.Controls.Control.BackgroundProperty, "ControlBrush");
	button2.SetResourceReference (System.Windows.Controls.Control.BorderBrushProperty, "LineBrush");
	button2.Click += delegate {
		action ();
	};
	return button2;
}


internal void AttachTextContextMenu (System.Windows.Controls.TextBox box, bool readOnly = false)
{
	System.Windows.Controls.ContextMenu menu = new System.Windows.Controls.ContextMenu ();
	menu.SetResourceReference (System.Windows.Controls.Control.BackgroundProperty, "SurfaceBrush");
	menu.SetResourceReference (System.Windows.Controls.Control.ForegroundProperty, "InkBrush");
	Action<string, string, Action> action = delegate(string glyph, string text, Action action2) {
		System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem {
			Header = IconText (glyph, text, 14.0),
			Padding = new Thickness (9.0, 7.0, 18.0, 7.0)
		};
		menuItem.Click += delegate {
			action2 ();
		};
		menu.Items.Add (menuItem);
	};
	action ("\ue8c8", "复制", delegate {
		if (box.SelectionLength > 0) {
			System.Windows.Clipboard.SetText (box.SelectedText);
		}
	});
	if (!readOnly) {
		action ("\ue77f", "粘贴", delegate {
			if (System.Windows.Clipboard.ContainsText ()) {
				box.SelectedText = System.Windows.Clipboard.GetText ();
			}
		});
	}
	if (!readOnly) {
		action ("\ue8c6", "剪切", delegate {
			if (box.SelectionLength > 0) {
				System.Windows.Clipboard.SetText (box.SelectedText);
				box.SelectedText = "";
			}
		});
	}
	action ("\ue8b3", "全选", delegate {
		box.SelectAll ();
	});
	menu.Opened += delegate {
		foreach (System.Windows.Controls.MenuItem item in (IEnumerable)menu.Items) {
			int isEnabled;
			switch (((StackPanel)item.Header).Children.OfType<TextBlock> ().Last ().Text) {
			default:
				isEnabled = 1;
				break;
			case "粘贴":
				isEnabled = ((!readOnly && System.Windows.Clipboard.ContainsText ()) ? 1 : 0);
				break;
			case "剪切":
				isEnabled = ((!readOnly && box.SelectionLength > 0) ? 1 : 0);
				break;
			case "复制":
				isEnabled = ((box.SelectionLength > 0) ? 1 : 0);
				break;
			}
			item.IsEnabled = (byte)isEnabled != 0;
		}
	};
	box.ContextMenu = menu;
}

}
}
