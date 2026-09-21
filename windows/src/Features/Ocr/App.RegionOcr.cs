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
private void OpenCurrentOcr ()
{
	if (original == null) {
		System.Windows.Controls.ContextMenu contextMenu = BuildOcrMenu ();
		contextMenu.IsOpen = true;
	} else {
		RegionOcr (original);
	}
}


private System.Windows.Controls.ContextMenu BuildOcrMenu ()
{
	System.Windows.Controls.Button anchor = Find<System.Windows.Controls.Button> ("OcrButton");
	System.Windows.Controls.ContextMenu contextMenu = new System.Windows.Controls.ContextMenu {
		PlacementTarget = anchor,
		Placement = PlacementMode.Bottom,
		VerticalOffset = 6.0,
		MinWidth = Math.Max (220.0, anchor.ActualWidth)
	};
	contextMenu.Style = (Style)window.FindResource ("ToolbarContextMenu");
	Style itemStyle = (Style)window.FindResource ("ToolbarMenuItem");
	System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem ();
	menuItem.Header = IconText ("\ue91b", "选择图片识别", 15.0);
	menuItem.Style = itemStyle;
	System.Windows.Controls.MenuItem menuItem2 = menuItem;
	menuItem2.Click += delegate {
		OpenOcr ();
	};
	System.Windows.Controls.MenuItem menuItem3 = new System.Windows.Controls.MenuItem ();
	menuItem3.Header = IconText ("\ue7b3", "截图框选识别", 15.0);
	menuItem3.Style = itemStyle;
	System.Windows.Controls.MenuItem menuItem4 = menuItem3;
	menuItem4.Click += delegate {
		Capture (true);
	};
	contextMenu.Items.Add (menuItem2);
	contextMenu.Items.Add (menuItem4);
	return contextMenu;
}


private void RegionOcr (BitmapSource bitmap, bool preview = false)
{
	DockPanel root = new DockPanel {
		Margin = new Thickness (16.0)
	};
	root.SetResourceReference (System.Windows.Controls.Panel.BackgroundProperty, "BackdropBrush");
	TextBlock hint = Label ("在图片上拖动框选，可反复重画；不框选则识别整张图片。", true);
	hint.Margin = new Thickness (0.0, 0.0, 0.0, 12.0);
	DockPanel.SetDock (hint, Dock.Top);
	root.Children.Add (hint);
	WrapPanel wrapPanel = new WrapPanel ();
	wrapPanel.Margin = new Thickness (0.0, 12.0, 0.0, 0.0);
	WrapPanel wrapPanel2 = wrapPanel;
	DockPanel.SetDock (wrapPanel2, Dock.Bottom);
	root.Children.Add (wrapPanel2);
	System.Windows.Controls.TextBox result = new System.Windows.Controls.TextBox {
		Padding = new Thickness (12.0),
		AcceptsReturn = true,
		TextWrapping = TextWrapping.Wrap,
		VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
		FontSize = 16.0
	};
	DockPanel resultPane = new DockPanel {
		Width = 320.0,
		Margin = new Thickness (14.0, 0.0, 0.0, 0.0)
	};
	FrameworkElement element = ViewZoom.Text (result);
	DockPanel.SetDock (element, Dock.Top);
	resultPane.Children.Add (element);
	resultPane.Children.Add (result);
	DockPanel.SetDock (resultPane, Dock.Right);
	root.Children.Add (resultPane);
	root.SizeChanged += delegate {
		resultPane.Width = Math.Max (240.0, root.ActualWidth * 0.34);
	};
	Canvas canvas = new Canvas {
		Width = bitmap.PixelWidth,
		Height = bitmap.PixelHeight,
		Background = System.Windows.Media.Brushes.Transparent,
		ClipToBounds = true,
		Cursor = System.Windows.Input.Cursors.Cross
	};
	canvas.Children.Add (new System.Windows.Controls.Image {
		Source = bitmap,
		Width = bitmap.PixelWidth,
		Height = bitmap.PixelHeight
	});
	System.Windows.Shapes.Rectangle rectangle = new System.Windows.Shapes.Rectangle {
		Stroke = System.Windows.Media.Brushes.DodgerBlue,
		StrokeThickness = Math.Max (2.0, (double)bitmap.PixelWidth / 450.0),
		Fill = new SolidColorBrush (System.Windows.Media.Color.FromArgb (35, 10, 100, byte.MaxValue)),
		IsHitTestVisible = false
	};
	canvas.Children.Add (rectangle);
	root.Children.Add (ViewZoom.Image (canvas));
	Window dialog = Dialog ("OCR · 自主框选翻译", 1040.0, 700.0, root);
	dialog.MinWidth = 780.0;
	dialog.MinHeight = 480.0;
	System.Windows.Point start = default(System.Windows.Point);
	Rect selected = Rect.Empty;
	bool dragging = false;
	int version = 0;
	bool closed = false;
	CancellationTokenSource pendingOcr = null;
	Action invalidate = delegate {
		version++;
		if (pendingOcr != null) {
			pendingOcr.Cancel ();
		}
	};
	Func<System.Windows.Point, System.Windows.Point> clamp = (System.Windows.Point p) => new System.Windows.Point (Math.Max (0.0, Math.Min (bitmap.PixelWidth, p.X)), Math.Max (0.0, Math.Min (bitmap.PixelHeight, p.Y)));
	canvas.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) {
		invalidate ();
		start = clamp (e.GetPosition (canvas));
		selected = Rect.Empty;
		System.Windows.Shapes.Rectangle rectangle2 = rectangle;
		double width = (rectangle.Height = 0.0);
		rectangle2.Width = width;
		dragging = true;
		canvas.CaptureMouse ();
	};
	canvas.MouseMove += delegate(object s, System.Windows.Input.MouseEventArgs e) {
		if (dragging) {
			selected = new Rect (start, clamp (e.GetPosition (canvas)));
			Canvas.SetLeft (rectangle, selected.X);
			Canvas.SetTop (rectangle, selected.Y);
			rectangle.Width = selected.Width;
			rectangle.Height = selected.Height;
		}
	};
	canvas.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e) {
		if (dragging) {
			selected = new Rect (start, clamp (e.GetPosition (canvas)));
			dragging = false;
			canvas.ReleaseMouseCapture ();
			hint.Text = "选区 " + Math.Round (selected.Width) + " × " + Math.Round (selected.Height) + " 像素 · 点击识别或翻译";
		}
	};
	Action<bool> run = async delegate(bool translate) {
		invalidate ();
		int ticket = version;
		CancellationTokenSource cancel = (pendingOcr = new CancellationTokenSource ());
		string path = System.IO.Path.Combine (System.IO.Path.GetTempPath (), "translator-region-" + Guid.NewGuid ().ToString ("N") + ".png");
		try {
			Int32Rect bounds = (selected.IsEmpty ? new Int32Rect (0, 0, bitmap.PixelWidth, bitmap.PixelHeight) : CropGeometry.CropBounds (selected, bitmap.PixelWidth, bitmap.PixelHeight));
			if (bounds.Width < 3 || bounds.Height < 3) {
				hint.Text = "选区太小，请重新框选。";
			} else {
				ImageFiles.Save (new CroppedBitmap (bitmap, bounds), path);
				hint.Text = "正在识别选区…";
				OcrDocument doc = await OcrService.Ocr (path, prefs.OcrLanguage);
				if (!closed && ticket == version) {
					string text = string.Join (Environment.NewLine, doc.Regions.Select ((Region r) => r.Text));
					result.Text = text;
					if (string.IsNullOrWhiteSpace (text)) {
						hint.Text = "选区内未识别到文字，请重新框选或更换 OCR 语言。";
					} else {
						if (translate) {
							hint.Text = "识别完成，正在翻译选区…";
							string prefix = text + Environment.NewLine + Environment.NewLine + "—— 译文 ——" + Environment.NewLine;
							List<string> translated = await new DeepL (Store.Key).Translate (new string[1] { text }, LanguageDetector.ResolveForDeepL (Code (source), text), Code (target), null, cancel.Token);
							if (closed || ticket != version) {
								return;
							}
							result.Text = prefix + translated [0];
						}
						hint.Text = (translate ? "选区翻译完成 · 可重新框选其他区域" : "识别完成 · 文字可编辑、复制");
					}
				}
			}
		} catch (OperationCanceledException) {
		} catch (Exception ex2) {
			if (!closed && ticket == version) {
				hint.Text = "处理失败：" + ex2.Message;
			}
		} finally {
			if (pendingOcr == cancel) {
				pendingOcr = null;
			}
			cancel.Dispose ();
			try {
				File.Delete (path);
			} catch {
			}
		}
	};
	wrapPanel2.Children.Add (ActionButton ("识别选区文字", delegate {
		run (false);
	}));
	wrapPanel2.Children.Add (ActionButton ("翻译选区", delegate {
		run (true);
	}));
	wrapPanel2.Children.Add (ActionButton ("整图 / 清除选框", delegate {
		invalidate ();
		selected = Rect.Empty;
		System.Windows.Shapes.Rectangle rectangle2 = rectangle;
		double width = (rectangle.Height = 0.0);
		rectangle2.Width = width;
		hint.Text = "已选择整张图片，也可重新拖动框选。";
	}));
	wrapPanel2.Children.Add (ActionButton ("复制结果", delegate {
		if (!string.IsNullOrWhiteSpace (result.Text)) {
			System.Windows.Clipboard.SetText (result.Text);
			hint.Text = "已复制结果";
		}
	}));
	wrapPanel2.Children.Add (ActionButton ("最大化 / 还原", delegate {
		dialog.WindowState = ((dialog.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}));
	wrapPanel2.Children.Add (ActionButton ("关闭", delegate {
		dialog.Close ();
	}));
	dialog.Closed += delegate {
		closed = true;
		invalidate ();
	};
	if (preview) {
		dialog.ContentRendered += delegate {
			dialog.UpdateLayout ();
			FrameworkElement frameworkElement = (FrameworkElement)dialog.Content;
			RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap ((int)frameworkElement.ActualWidth, (int)frameworkElement.ActualHeight, 96.0, 96.0, PixelFormats.Pbgra32);
			DrawingVisual drawingVisual = new DrawingVisual ();
			using (DrawingContext drawingContext = drawingVisual.RenderOpen ()) {
				drawingContext.DrawRectangle (new VisualBrush (frameworkElement), null, new Rect (0.0, 0.0, frameworkElement.ActualWidth, frameworkElement.ActualHeight));
			}
			renderTargetBitmap.Render (drawingVisual);
			ImageFiles.Save (renderTargetBitmap, System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "region-preview.png"));
			dialog.Close ();
		};
		dialog.ShowDialog ();
	} else {
		dialog.Show ();
	}
}

}
}
