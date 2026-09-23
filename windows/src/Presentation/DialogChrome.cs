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
		grid2.Margin = new Thickness (0.0);
		Grid grid3 = grid2;
		grid3.ColumnDefinitions.Add (new ColumnDefinition {
			Width = new GridLength (138.0)
		});
		grid3.ColumnDefinitions.Add (new ColumnDefinition ());
		grid3.ColumnDefinitions.Add (new ColumnDefinition {
			Width = new GridLength (138.0)
		});
		grid.Children.Add (grid3);
		StackPanel stackPanel = new StackPanel ();
		stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		StackPanel stackPanel2 = stackPanel;
		Grid.SetColumn (stackPanel2, 2);
		grid3.Children.Add (stackPanel2);
		System.Windows.Controls.Button button = CaptionButton ("\ue921", "最小化", "minimize", false);
		System.Windows.Controls.Button button2 = CaptionButton ("\ue922", "最大化", "maximize", false);
		System.Windows.Controls.Button button3 = CaptionButton ("\ue8bb", "关闭", "close", true);
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
			dialog.WindowState = WindowState.Minimized;
		};
		button2.Click += delegate {
			ToggleMaximize (dialog);
		};
		button3.Click += delegate {
			dialog.Close ();
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
			SetMaximizeGlyph (button2, dialog.WindowState == WindowState.Maximized);
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

	private static System.Windows.Controls.Button CaptionButton (string glyph, string tooltip, string role, bool close)
	{
		System.Windows.Controls.Button button = new System.Windows.Controls.Button {
			Width = 46.0,
			Height = 48.0,
			Padding = new Thickness (0.0),
			Margin = new Thickness (0.0),
			Background = System.Windows.Media.Brushes.Transparent,
			BorderThickness = new Thickness (0.0),
			ToolTip = tooltip,
			Tag = "dialog-caption-" + role,
			Cursor = System.Windows.Input.Cursors.Arrow,
			Content = new TextBlock {
				Text = glyph,
				FontFamily = new System.Windows.Media.FontFamily ("Segoe Fluent Icons"),
				FontSize = 10.0,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
		button.SetResourceReference (System.Windows.Controls.Control.ForegroundProperty, "InkBrush");
		button.Template = (ControlTemplate)XamlReader.Parse ("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='{TemplateBinding Background}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate>");
		button.MouseEnter += delegate {
			button.Background = close ? new SolidColorBrush (System.Windows.Media.Color.FromRgb (232, 17, 35)) : (button.TryFindResource ("ControlBrush") as System.Windows.Media.Brush ?? new SolidColorBrush (System.Windows.Media.Color.FromArgb (24, 128, 140, 160)));
			if (close) button.Foreground = System.Windows.Media.Brushes.White;
		};
		button.MouseLeave += delegate {
			button.Background = System.Windows.Media.Brushes.Transparent;
			button.SetResourceReference (System.Windows.Controls.Control.ForegroundProperty, "InkBrush");
		};
		return button;
	}

	private static void SetMaximizeGlyph (System.Windows.Controls.Button button, bool maximized)
	{
		TextBlock glyph = button.Content as TextBlock;
		if (glyph != null) glyph.Text = maximized ? "\ue923" : "\ue922";
		button.ToolTip = maximized ? "还原" : "最大化";
	}

	private static void ToggleMaximize (Window dialog)
	{
		dialog.WindowState = ((dialog.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}
}

}
