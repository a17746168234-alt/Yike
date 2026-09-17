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
public static class ImageWindows
{
	public static Window PreviewImage (Window owner, BitmapSource bitmap, string title)
	{
		Window window = CreateImagePreview (owner, bitmap, title);
		window.Show ();
		return window;
	}

	internal static Window CreateImagePreview (Window owner, BitmapSource bitmap, string title)
	{
		Canvas canvas = new Canvas ();
		canvas.Width = bitmap.PixelWidth;
		canvas.Height = bitmap.PixelHeight;
		canvas.Background = System.Windows.Media.Brushes.Transparent;
		Canvas canvas2 = canvas;
		canvas2.Children.Add (new System.Windows.Controls.Image {
			Source = bitmap,
			Width = bitmap.PixelWidth,
			Height = bitmap.PixelHeight,
			Stretch = Stretch.Fill
		});
		FrameworkElement frameworkElement = ViewZoom.Image (canvas2);
		frameworkElement.Margin = new Thickness (16.0);
		Rect workArea = SystemParameters.WorkArea;
		double num = Math.Max (480.0, Math.Min (860.0, (workArea.Width - 96.0) / 2.0));
		double height = Math.Min (720.0, workArea.Height - 96.0);
		Window window = new Window ();
		window.Width = num;
		window.Height = height;
		window.MinWidth = 420.0;
		window.MinHeight = 320.0;
		window.MaxWidth = Math.Max (480.0, workArea.Width - 16.0);
		window.MaxHeight = Math.Max (320.0, workArea.Height - 16.0);
		window.WindowStartupLocation = WindowStartupLocation.Manual;
		Window window2 = window;
		DialogChrome.Apply (window2, owner, title, frameworkElement);
		window2.Left = ((title.IndexOf ("原文", StringComparison.Ordinal) >= 0) ? (workArea.Left + 32.0) : (workArea.Right - num - 32.0));
		window2.Top = workArea.Top + 48.0;
		window2.SetResourceReference (System.Windows.Controls.Control.BackgroundProperty, "BackdropBrush");
		window2.SetResourceReference (System.Windows.Controls.Control.ForegroundProperty, "InkBrush");
		return window2;
	}

	public static void Compare (Window owner, BitmapSource original, BitmapSource translated)
	{
		DockPanel dockPanel = new DockPanel ();
		dockPanel.Margin = new Thickness (16.0);
		DockPanel dockPanel2 = dockPanel;
		Slider slider = new Slider {
			Minimum = 0.0,
			Maximum = 1.0,
			Value = 0.5,
			Margin = new Thickness (0.0, 12.0, 0.0, 0.0)
		};
		DockPanel.SetDock (slider, Dock.Bottom);
		dockPanel2.Children.Add (slider);
		Grid grid = new Grid ();
		System.Windows.Controls.Image image = new System.Windows.Controls.Image ();
		image.Source = original;
		image.Stretch = Stretch.Uniform;
		System.Windows.Controls.Image element = image;
		System.Windows.Controls.Image b = new System.Windows.Controls.Image {
			Source = translated,
			Stretch = Stretch.Uniform
		};
		grid.Children.Add (element);
		grid.Children.Add (b);
		dockPanel2.Children.Add (grid);
		Action clip = delegate {
			b.Clip = new RectangleGeometry (new Rect (0.0, 0.0, grid.ActualWidth * slider.Value, grid.ActualHeight));
		};
		slider.ValueChanged += delegate {
			clip ();
		};
		grid.SizeChanged += delegate {
			clip ();
		};
		Window window = new Window ();
		window.Width = 900.0;
		window.Height = 650.0;
		window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Window window2 = window;
		DialogChrome.Apply (window2, owner, "原图 / 译图 · 拖动滑杆对比", dockPanel2);
		window2.ShowDialog ();
	}

	public static void PreviewText (Window owner, string text, string title)
	{
		CreateTextPreview (owner, text, title).ShowDialog ();
	}

	internal static Window CreateTextPreview (Window owner, string text, string title)
	{
		DockPanel dockPanel = new DockPanel ();
		dockPanel.Margin = new Thickness (24.0);
		DockPanel dockPanel2 = dockPanel;
		dockPanel2.SetResourceReference (System.Windows.Controls.Panel.BackgroundProperty, "BackdropBrush");
		TextBlock textBlock = new TextBlock ();
		textBlock.Text = title + "文字";
		textBlock.FontSize = 22.0;
		textBlock.FontWeight = FontWeights.SemiBold;
		textBlock.Margin = new Thickness (2.0, 0.0, 0.0, 14.0);
		TextBlock textBlock2 = textBlock;
		textBlock2.SetResourceReference (TextBlock.ForegroundProperty, "InkBrush");
		DockPanel.SetDock (textBlock2, Dock.Top);
		dockPanel2.Children.Add (textBlock2);
		System.Windows.Controls.TextBox textBox = new System.Windows.Controls.TextBox ();
		textBox.Text = text ?? "";
		textBox.IsReadOnly = true;
		textBox.AcceptsReturn = true;
		textBox.TextWrapping = TextWrapping.Wrap;
		textBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
		textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
		textBox.VerticalContentAlignment = VerticalAlignment.Top;
		textBox.Padding = new Thickness (22.0);
		textBox.FontSize = 18.0;
		textBox.BorderThickness = new Thickness (0.0);
		System.Windows.Controls.TextBox textBox2 = textBox;
		textBox2.SetResourceReference (System.Windows.Controls.Control.ForegroundProperty, "InkBrush");
		textBox2.SetResourceReference (System.Windows.Controls.Control.BackgroundProperty, "SurfaceBrush");
		FrameworkElement frameworkElement = ViewZoom.Text (textBox2);
		frameworkElement.Margin = new Thickness (0.0, 0.0, 0.0, 12.0);
		DockPanel.SetDock (frameworkElement, Dock.Top);
		dockPanel2.Children.Add (frameworkElement);
		dockPanel2.Children.Add (textBox2);
		ScrollViewer scroll = new ScrollViewer {
			Content = textBox2,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			CanContentScroll = false
		};
		scroll.SetResourceReference (System.Windows.Controls.Control.BackgroundProperty, "SurfaceBrush");
		scroll.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e) {
			if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) {
				scroll.ScrollToVerticalOffset (Math.Max (0.0, scroll.VerticalOffset - (double)e.Delta));
				e.Handled = true;
			}
		};
		Border border = new Border ();
		border.Child = scroll;
		border.CornerRadius = new CornerRadius (18.0);
		border.BorderThickness = new Thickness (1.0);
		border.ClipToBounds = true;
		Border border2 = border;
		border2.SetResourceReference (Border.BackgroundProperty, "SurfaceBrush");
		border2.SetResourceReference (Border.BorderBrushProperty, "LineBrush");
		dockPanel2.Children.Remove (textBox2);
		dockPanel2.Children.Add (border2);
		Rect workArea = SystemParameters.WorkArea;
		Window window = new Window ();
		window.Width = Math.Min (900.0, workArea.Width - 48.0);
		window.Height = Math.Min (720.0, workArea.Height - 96.0);
		window.MinWidth = 460.0;
		window.MinHeight = 320.0;
		window.MaxWidth = Math.Max (460.0, workArea.Width - 16.0);
		window.MaxHeight = Math.Max (320.0, workArea.Height - 16.0);
		window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Window window2 = window;
		DialogChrome.Apply (window2, owner, title + " · 文字阅读", dockPanel2);
		window2.SetResourceReference (System.Windows.Controls.Control.ForegroundProperty, "InkBrush");
		return window2;
	}

	public static void Edit (Window owner, BitmapSource original, OcrDocument doc, bool translated)
	{
		DockPanel dockPanel = new DockPanel ();
		dockPanel.Margin = new Thickness (16.0);
		DockPanel dockPanel2 = dockPanel;
		StackPanel stackPanel = new StackPanel ();
		stackPanel.Width = 230.0;
		stackPanel.Margin = new Thickness (16.0, 0.0, 0.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		DockPanel.SetDock (stackPanel2, Dock.Right);
		dockPanel2.Children.Add (stackPanel2);
		stackPanel2.Children.Add (new TextBlock {
			Text = "点击图片中的文字框\n拖动文字框调整位置",
			Margin = new Thickness (0.0, 0.0, 0.0, 12.0)
		});
		System.Windows.Controls.TextBox text = new System.Windows.Controls.TextBox {
			AcceptsReturn = true,
			TextWrapping = TextWrapping.Wrap,
			Height = 130.0
		};
		stackPanel2.Children.Add (text);
		Dictionary<string, System.Windows.Controls.TextBox> fields = new Dictionary<string, System.Windows.Controls.TextBox> ();
		string[] array = new string[6] { "X", "Y", "宽度", "高度", "字号", "颜色" };
		foreach (string text2 in array) {
			stackPanel2.Children.Add (new TextBlock {
				Text = text2,
				Margin = new Thickness (0.0, 8.0, 0.0, 2.0)
			});
			System.Windows.Controls.TextBox textBox = new System.Windows.Controls.TextBox ();
			fields [text2] = textBox;
			stackPanel2.Children.Add (textBox);
		}
		Canvas canvas = new Canvas {
			Width = original.PixelWidth,
			Height = original.PixelHeight
		};
		Viewbox viewbox = new Viewbox ();
		viewbox.Child = canvas;
		viewbox.Stretch = Stretch.Uniform;
		Viewbox element = viewbox;
		dockPanel2.Children.Add (element);
		Region selected = null;
		bool filling = false;
		Action fill = delegate {
			if (selected != null) {
				filling = true;
				text.Text = (translated ? selected.Translated : selected.Text);
				fields ["X"].Text = selected.X.ToString ("0.##");
				fields ["Y"].Text = selected.Y.ToString ("0.##");
				fields ["宽度"].Text = selected.Width.ToString ("0.##");
				fields ["高度"].Text = selected.Height.ToString ("0.##");
				fields ["字号"].Text = selected.FontSize.ToString ("0.##");
				fields ["颜色"].Text = selected.Color;
				filling = false;
			}
		};
		Action redraw = null;
		redraw = delegate {
			canvas.Children.Clear ();
			canvas.Children.Add (new System.Windows.Controls.Image {
				Source = (translated ? ImageRenderer.Render (original, doc) : original),
				Width = original.PixelWidth,
				Height = original.PixelHeight
			});
			foreach (Region region in doc.Regions) {
				Border border = new Border {
					Width = Math.Max (4.0, region.Width),
					Height = Math.Max (4.0, region.Height),
					BorderBrush = ((region == selected) ? System.Windows.Media.Brushes.OrangeRed : System.Windows.Media.Brushes.DodgerBlue),
					BorderThickness = new Thickness (Math.Max (1.0, (double)original.PixelWidth / 650.0)),
					Background = new SolidColorBrush (System.Windows.Media.Color.FromArgb (10, 0, 100, byte.MaxValue)),
					Cursor = System.Windows.Input.Cursors.SizeAll
				};
				Canvas.SetLeft (border, region.X);
				Canvas.SetTop (border, region.Y);
				canvas.Children.Add (border);
				System.Windows.Point last = default(System.Windows.Point);
				bool drag = false;
				border.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) {
					selected = region;
					fill ();
					last = e.GetPosition (canvas);
					drag = true;
					border.CaptureMouse ();
					e.Handled = true;
				};
				border.MouseMove += delegate(object s, System.Windows.Input.MouseEventArgs e) {
					if (drag) {
						System.Windows.Point position = e.GetPosition (canvas);
						region.X = Math.Max (0.0, Math.Min ((double)original.PixelWidth - region.Width, region.X + position.X - last.X));
						region.Y = Math.Max (0.0, Math.Min ((double)original.PixelHeight - region.Height, region.Y + position.Y - last.Y));
						last = position;
						Canvas.SetLeft (border, region.X);
						Canvas.SetTop (border, region.Y);
						fill ();
					}
				};
				border.MouseLeftButtonUp += delegate {
					drag = false;
					border.ReleaseMouseCapture ();
					redraw ();
				};
			}
		};
		System.Windows.Controls.Button button = new System.Windows.Controls.Button ();
		button.Content = "应用到选中区域";
		button.Margin = new Thickness (0.0, 12.0, 0.0, 4.0);
		System.Windows.Controls.Button button2 = button;
		button2.Click += delegate {
			if (selected != null && !filling) {
				double result;
				double result2;
				double result3;
				double result4;
				double result5;
				if (!double.TryParse (fields ["X"].Text, out result) || !double.TryParse (fields ["Y"].Text, out result2) || !double.TryParse (fields ["宽度"].Text, out result3) || !double.TryParse (fields ["高度"].Text, out result4) || !double.TryParse (fields ["字号"].Text, out result5) || result3 <= 0.0 || result4 <= 0.0 || result5 <= 0.0) {
					System.Windows.MessageBox.Show ("请输入有效坐标和正数尺寸。");
				} else {
					if (translated) {
						selected.Translated = text.Text;
					} else {
						selected.Text = text.Text;
					}
					selected.X = Math.Max (0.0, result);
					selected.Y = Math.Max (0.0, result2);
					selected.Width = result3;
					selected.Height = result4;
					selected.FontSize = result5;
					selected.Color = fields ["颜色"].Text;
					redraw ();
				}
			}
		};
		stackPanel2.Children.Add (button2);
		System.Windows.Controls.Button button3 = new System.Windows.Controls.Button ();
		button3.Content = "补充文字框";
		button3.Margin = new Thickness (0.0, 4.0, 0.0, 4.0);
		System.Windows.Controls.Button button4 = button3;
		button4.Click += delegate {
			selected = new Region {
				Text = "新文字",
				Translated = "新译文",
				X = 10.0,
				Y = 10.0,
				Width = Math.Min (200, original.PixelWidth),
				Height = 40.0,
				FontSize = 22.0
			};
			doc.Regions.Add (selected);
			fill ();
			redraw ();
		};
		stackPanel2.Children.Add (button4);
		System.Windows.Controls.Button button5 = new System.Windows.Controls.Button ();
		button5.Content = "删除选中区域";
		button5.Margin = new Thickness (0.0, 4.0, 0.0, 4.0);
		System.Windows.Controls.Button button6 = button5;
		button6.Click += delegate {
			if (selected != null) {
				doc.Regions.Remove (selected);
				selected = null;
				text.Clear ();
				redraw ();
			}
		};
		stackPanel2.Children.Add (button6);
		redraw ();
		Window window = new Window ();
		window.Width = 1050.0;
		window.Height = 760.0;
		window.MinHeight = 700.0;
		window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Window window2 = window;
		DialogChrome.Apply (window2, owner, translated ? "编辑译文框" : "编辑 OCR 原文", dockPanel2);
		window2.ShowDialog ();
	}
}

}
