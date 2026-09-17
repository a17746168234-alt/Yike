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
public static class ScreenshotCapture
{
	public static string Capture ()
	{
		System.Drawing.Rectangle virtualScreen = SystemInformation.VirtualScreen;
		BitmapSource bitmapSource;
		using (Bitmap bitmap = new Bitmap (virtualScreen.Width, virtualScreen.Height)) {
			using (Graphics graphics = Graphics.FromImage (bitmap)) {
				graphics.CopyFromScreen (virtualScreen.Left, virtualScreen.Top, 0, 0, virtualScreen.Size);
			}
			using (MemoryStream memoryStream = new MemoryStream ()) {
				bitmap.Save (memoryStream, ImageFormat.Png);
				memoryStream.Position = 0L;
				bitmapSource = BitmapFrame.Create (memoryStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
			}
		}
		Func<Rect> selection;
		Window window = CreateCaptureOverlay (bitmapSource, virtualScreen, out selection);
		if (window.ShowDialog () != true) {
			return null;
		}
		CroppedBitmap image = new CroppedBitmap (bitmapSource, CropGeometry.CropBounds (selection (), bitmapSource.PixelWidth, bitmapSource.PixelHeight));
		Directory.CreateDirectory (Store.Root);
		string text = System.IO.Path.Combine (Store.Root, "capture-" + Guid.NewGuid ().ToString ("N") + ".png");
		ImageFiles.Save (image, text);
		return text;
	}

	internal static Window CreateCaptureOverlay (BitmapSource source, System.Drawing.Rectangle area, out Func<Rect> selection)
	{
		Canvas canvas = new Canvas {
			Background = System.Windows.Media.Brushes.Transparent
		};
		System.Windows.Controls.Image image = new System.Windows.Controls.Image {
			Source = source,
			Stretch = Stretch.Fill
		};
		canvas.Children.Add (image);
		System.Windows.Shapes.Rectangle shade = new System.Windows.Shapes.Rectangle {
			Fill = new SolidColorBrush (System.Windows.Media.Color.FromArgb (80, 0, 0, 0)),
			IsHitTestVisible = false
		};
		canvas.Children.Add (shade);
		System.Windows.Shapes.Rectangle rectangle = new System.Windows.Shapes.Rectangle {
			Stroke = System.Windows.Media.Brushes.DodgerBlue,
			StrokeThickness = 2.0,
			Fill = new SolidColorBrush (System.Windows.Media.Color.FromArgb (20, byte.MaxValue, byte.MaxValue, byte.MaxValue))
		};
		canvas.Children.Add (rectangle);
		Window overlay = new Window {
			Title = "截图选区 · Yike",
			WindowStyle = WindowStyle.None,
			ResizeMode = ResizeMode.NoResize,
			Left = area.Left,
			Top = area.Top,
			Width = area.Width,
			Height = area.Height,
			Topmost = true,
			ShowInTaskbar = false,
			Cursor = System.Windows.Input.Cursors.Cross
		};
		System.Windows.Point start = default(System.Windows.Point);
		Rect selected = Rect.Empty;
		bool drawing = false;
		Grid grid = new Grid ();
		grid.Children.Add (canvas);
		StackPanel stackPanel = new StackPanel ();
		stackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		stackPanel.VerticalAlignment = VerticalAlignment.Top;
		stackPanel.Margin = new Thickness (16.0);
		stackPanel.Background = System.Windows.Media.Brushes.Black;
		StackPanel stackPanel2 = stackPanel;
		TextBlock info = new TextBlock {
			Text = "拖动框选，可重画 · Enter 确认 · Esc 取消",
			Foreground = System.Windows.Media.Brushes.White,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness (12.0)
		};
		stackPanel2.Children.Add (info);
		System.Windows.Controls.Button button = new System.Windows.Controls.Button ();
		button.Content = "确认选区";
		button.Foreground = System.Windows.Media.Brushes.White;
		button.Padding = new Thickness (12.0);
		System.Windows.Controls.Button button2 = button;
		stackPanel2.Children.Add (button2);
		System.Windows.Controls.Button button3 = new System.Windows.Controls.Button ();
		button3.Content = "取消";
		button3.Foreground = System.Windows.Media.Brushes.White;
		button3.Padding = new Thickness (12.0);
		System.Windows.Controls.Button button4 = button3;
		stackPanel2.Children.Add (button4);
		grid.Children.Add (stackPanel2);
		overlay.Content = grid;
		Action accept = delegate {
			if (!selected.IsEmpty && selected.Width > 3.0 && selected.Height > 3.0) {
				overlay.DialogResult = true;
			}
		};
		button2.Click += delegate {
			accept ();
		};
		button4.Click += delegate {
			overlay.DialogResult = false;
		};
		overlay.SourceInitialized += delegate {
			Native.SetWindowPos (new WindowInteropHelper (overlay).Handle, new IntPtr (-1), area.Left, area.Top, area.Width, area.Height, 64u);
		};
		overlay.Loaded += delegate {
			image.Width = canvas.ActualWidth;
			image.Height = canvas.ActualHeight;
			shade.Width = canvas.ActualWidth;
			shade.Height = canvas.ActualHeight;
		};
		canvas.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) {
			start = e.GetPosition (canvas);
			selected = Rect.Empty;
			System.Windows.Shapes.Rectangle rectangle2 = rectangle;
			double width = (rectangle.Height = 0.0);
			rectangle2.Width = width;
			drawing = true;
			canvas.CaptureMouse ();
		};
		canvas.MouseMove += delegate(object s, System.Windows.Input.MouseEventArgs e) {
			if (drawing) {
				selected = new Rect (start, e.GetPosition (canvas));
				Canvas.SetLeft (rectangle, selected.Left);
				Canvas.SetTop (rectangle, selected.Top);
				rectangle.Width = selected.Width;
				rectangle.Height = selected.Height;
			}
		};
		canvas.MouseLeftButtonUp += delegate {
			drawing = false;
			canvas.ReleaseMouseCapture ();
			if (!selected.IsEmpty) {
				info.Text = "选区已就绪 · 可重画或确认";
			}
		};
		overlay.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e) {
			if (e.Key == Key.Escape) {
				overlay.DialogResult = false;
			}
			if (e.Key == Key.Return) {
				accept ();
			}
		};
		selection = delegate {
			if (selected.IsEmpty) {
				return Rect.Empty;
			}
			double num = (double)source.PixelWidth / canvas.ActualWidth;
			double num2 = (double)source.PixelHeight / canvas.ActualHeight;
			return new Rect (selected.X * num, selected.Y * num2, selected.Width * num, selected.Height * num2);
		};
		return overlay;
	}
}

}
