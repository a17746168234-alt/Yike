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
internal static class DialogChrome
{
	private static readonly string[] ResourceKeys = new string[11] {
		"WindowBrush", "PanelBrush", "InkBrush", "MutedBrush", "LineBrush", "BackdropBrush", "SurfaceBrush", "ControlBrush", "AccentBrush", "AccentHoverBrush",
		"SoftAccentBrush"
	};

	public static Window Apply (Window dialog, Window owner, string title, UIElement body, double radius = 26.0)
	{
		if (owner != null) {
			foreach (DictionaryEntry resource in owner.Resources) {
				dialog.Resources [resource.Key] = resource.Value;
			}
			string[] resourceKeys = ResourceKeys;
			foreach (string text in resourceKeys) {
				object obj = owner.TryFindResource (text);
				if (obj != null) {
					dialog.Resources [text] = obj;
				}
			}
		}
		dialog.Resources ["DialogChromeOwnerPalette"] = true;
		dialog.Owner = owner;
		dialog.Title = title;
		dialog.WindowStyle = WindowStyle.None;
		dialog.AllowsTransparency = true;
		dialog.Background = System.Windows.Media.Brushes.Transparent;
		dialog.ShowInTaskbar = true;
		dialog.ResizeMode = ResizeMode.CanResizeWithGrip;
		dialog.SetResourceReference (System.Windows.Controls.Control.ForegroundProperty, "InkBrush");
		Grid grid = new Grid ();
		grid.RowDefinitions.Add (new RowDefinition {
			Height = new GridLength (48.0)
		});
		grid.RowDefinitions.Add (new RowDefinition ());
		Grid grid2 = new Grid ();
		grid2.Background = System.Windows.Media.Brushes.Transparent;
		grid2.Margin = new Thickness (17.0, 0.0, 17.0, 0.0);
		Grid grid3 = grid2;
		grid3.ColumnDefinitions.Add (new ColumnDefinition {
			Width = new GridLength (96.0)
		});
		grid3.ColumnDefinitions.Add (new ColumnDefinition ());
		grid3.ColumnDefinitions.Add (new ColumnDefinition {
			Width = new GridLength (96.0)
		});
		grid.Children.Add (grid3);
		StackPanel stackPanel = new StackPanel ();
		stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		grid3.Children.Add (stackPanel2);
		System.Windows.Controls.Button button = TrafficButton ("#FF5F57", "关闭", "close");
		System.Windows.Controls.Button button2 = TrafficButton ("#FFBD2E", "最小化", "minimize");
		System.Windows.Controls.Button button3 = TrafficButton ("#28C840", "最大化 / 还原", "maximize");
		stackPanel2.Children.Add (button);
		stackPanel2.Children.Add (button2);
		stackPanel2.Children.Add (button3);
		TextBlock textBlock = new TextBlock ();
		textBlock.Text = title;
		textBlock.FontSize = 13.0;
		textBlock.FontWeight = FontWeights.SemiBold;
		textBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.Opacity = 0.78;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		TextBlock element = textBlock;
		Grid.SetColumn (element, 1);
		grid3.Children.Add (element);
		ContentControl contentControl = new ContentControl ();
		contentControl.Content = body;
		ContentControl element2 = contentControl;
		Grid.SetRow (element2, 1);
		grid.Children.Add (element2);
		Border frame = new Border {
			CornerRadius = new CornerRadius (radius),
			BorderThickness = new Thickness (1.0),
			Child = grid,
			ClipToBounds = true,
			Effect = new DropShadowEffect {
				BlurRadius = 28.0,
				ShadowDepth = 7.0,
				Opacity = 0.3,
				Color = Colors.Black
			}
		};
		frame.SetResourceReference (Border.BackgroundProperty, "BackdropBrush");
		frame.SetResourceReference (Border.BorderBrushProperty, "LineBrush");
		dialog.Content = frame;
		dialog.Tag = body;
		button.Click += delegate {
			dialog.Close ();
		};
		button2.Click += delegate {
			dialog.WindowState = WindowState.Minimized;
		};
		button3.Click += delegate {
			ToggleMaximize (dialog);
		};
		grid3.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) {
			if (IsInteractiveSource (e.OriginalSource as DependencyObject, grid3)) return;
			if (e.ClickCount == 2) {
				ToggleMaximize (dialog);
				e.Handled = true;
			} else if (e.LeftButton == MouseButtonState.Pressed) {
				dialog.DragMove ();
			}
		};
		dialog.PreviewKeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e) {
			if (e.Key == Key.Escape) {
				e.Handled = true;
				dialog.Close ();
			}
		};
		dialog.StateChanged += delegate {
			frame.CornerRadius = new CornerRadius ((dialog.WindowState == WindowState.Maximized) ? 0.0 : radius);
		};
		return dialog;
	}

	internal static bool IsInteractiveSource (DependencyObject hit, DependencyObject header)
	{
		while (hit != null && hit != header) {
			if (hit is System.Windows.Controls.Primitives.ButtonBase || hit is System.Windows.Controls.Primitives.TextBoxBase || hit is System.Windows.Controls.Primitives.Selector) return true;
			hit = (hit is Visual ? VisualTreeHelper.GetParent (hit) : null) ?? LogicalTreeHelper.GetParent (hit);
		}
		return false;
	}

	public static UIElement Body (Window dialog)
	{
		if (dialog != null) {
			return dialog.Tag as UIElement;
		}
		return null;
	}

	public static void SyncOwnerPalette (Window owner)
	{
		if (owner == null) {
			return;
		}
		foreach (Window ownedWindow in owner.OwnedWindows) {
			if (!object.Equals (ownedWindow.Resources ["DialogChromeOwnerPalette"], true)) {
				continue;
			}
			string[] resourceKeys = ResourceKeys;
			foreach (string text in resourceKeys) {
				object obj = owner.TryFindResource (text);
				if (obj != null) {
					ownedWindow.Resources [text] = obj;
				}
			}
		}
	}

	private static System.Windows.Controls.Button TrafficButton (string color, string tooltip, string role)
	{
		System.Windows.Controls.Button button = new System.Windows.Controls.Button ();
		button.Width = 14.0;
		button.Height = 14.0;
		button.Margin = new Thickness (0.0, 0.0, 8.0, 0.0);
		button.Padding = new Thickness (0.0);
		button.Background = new SolidColorBrush ((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString (color));
		button.BorderBrush = new SolidColorBrush ((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString ("#40FFFFFF"));
		button.BorderThickness = new Thickness (1.0);
		button.ToolTip = tooltip;
		button.Tag = "dialog-traffic-" + role;
		button.Cursor = System.Windows.Input.Cursors.Hand;
		return button;
	}

	private static void ToggleMaximize (Window dialog)
	{
		dialog.WindowState = ((dialog.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}
}

}
