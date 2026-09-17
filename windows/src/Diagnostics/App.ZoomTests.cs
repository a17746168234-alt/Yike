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
private void VerifyZoom ()
{
	SelectionPopup selectionPopup = CreateSelectionPopup ("Long text remains selectable while the window is enlarged.", "ZH-HANS");
	Window window = new Window ();
	window.Width = 640.0;
	window.Height = 480.0;
	window.ShowInTaskbar = false;
	Window window2 = window;
	Window window3 = null;
	Window window4 = null;
	Window window5 = null;
	try {
		selectionPopup.Complete (string.Join ("\n", Enumerable.Repeat ("放大后依然可以阅读、选择和复制译文。", 20)), true);
		selectionPopup.Window.Show ();
		selectionPopup.Window.UpdateLayout ();
		double fontSize = selectionPopup.Result.FontSize;
		System.Windows.Controls.Panel panel = (System.Windows.Controls.Panel)((ContentControl)selectionPopup.Window.FindName ("TextZoom")).Content;
		Action<System.Windows.Controls.Button> action = delegate(System.Windows.Controls.Button button) {
			button.RaiseEvent (new RoutedEventArgs (System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
		};
		action ((System.Windows.Controls.Button)panel.Children [2]);
		if (selectionPopup.Result.FontSize <= fontSize) {
			throw new Exception ("Text zoom did not enlarge translation");
		}
		for (int num = 0; num < 20; num++) {
			action ((System.Windows.Controls.Button)panel.Children [2]);
		}
		if (selectionPopup.Result.FontSize != fontSize * 3.0) {
			throw new Exception ("Text zoom upper bound failed");
		}
		action ((System.Windows.Controls.Button)panel.Children [1]);
		if (selectionPopup.Result.FontSize != fontSize) {
			throw new Exception ("Text zoom reset failed");
		}
		System.Windows.Controls.Button obj = (System.Windows.Controls.Button)selectionPopup.Window.FindName ("MaximizePopup");
		action (obj);
		selectionPopup.Window.UpdateLayout ();
		if (selectionPopup.Window.WindowState != WindowState.Maximized) {
			throw new Exception ("Popup maximize failed");
		}
		action (obj);
		selectionPopup.Window.UpdateLayout ();
		if (selectionPopup.Window.WindowState != WindowState.Normal) {
			throw new Exception ("Popup restore failed");
		}
		selectionPopup.Window.Width = 900.0;
		selectionPopup.Window.Height = 650.0;
		selectionPopup.Window.UpdateLayout ();
		FrameworkElement frameworkElement = (FrameworkElement)selectionPopup.Window.Content;
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap ((int)frameworkElement.ActualWidth, (int)frameworkElement.ActualHeight, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render (frameworkElement);
		ImageFiles.Save (renderTargetBitmap, System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "zoom-preview.png"));
		window3 = ImageWindows.CreateTextPreview (this.window, string.Join ("\n", Enumerable.Repeat ("较长的 OCR 文本需要在窗口内滚动阅读。", 80)), "译文");
		DockPanel dockPanel = (DockPanel)DialogChrome.Body (window3);
		Border border = (Border)dockPanel.Children [2];
		ScrollViewer scrollViewer = (ScrollViewer)border.Child;
		System.Windows.Controls.TextBox textBox = (System.Windows.Controls.TextBox)scrollViewer.Content;
		if (window3.MaxWidth > SystemParameters.WorkArea.Width || window3.MaxHeight > SystemParameters.WorkArea.Height) {
			throw new Exception ("Text preview can exceed the screen");
		}
		if (scrollViewer.VerticalScrollBarVisibility != ScrollBarVisibility.Auto || scrollViewer.CanContentScroll || textBox.TextWrapping != TextWrapping.Wrap) {
			throw new Exception ("Text preview cannot scroll or wrap long content");
		}
		System.Windows.Controls.Panel panel2 = (System.Windows.Controls.Panel)dockPanel.Children [1];
		double fontSize2 = textBox.FontSize;
		action ((System.Windows.Controls.Button)panel2.Children [2]);
		if (textBox.FontSize <= fontSize2) {
			throw new Exception ("Text preview font zoom failed");
		}
		window3.Height = 420.0;
		window3.Show ();
		window3.UpdateLayout ();
		if (scrollViewer.ScrollableHeight <= 0.0) {
			throw new Exception ("Long OCR text does not create a scrollable reading area");
		}
		MouseWheelEventArgs e = new MouseWheelEventArgs (Mouse.PrimaryDevice, Environment.TickCount, -120);
		e.RoutedEvent = Mouse.PreviewMouseWheelEvent;
		MouseWheelEventArgs e2 = e;
		scrollViewer.RaiseEvent (e2);
		window3.UpdateLayout ();
		if (!e2.Handled || scrollViewer.VerticalOffset <= 0.0) {
			throw new Exception ("Ordinary mouse-wheel scrolling failed in text preview");
		}
		FrameworkElement frameworkElement2 = (FrameworkElement)window3.Content;
		RenderTargetBitmap renderTargetBitmap2 = new RenderTargetBitmap ((int)frameworkElement2.ActualWidth, (int)frameworkElement2.ActualHeight, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap2.Render (frameworkElement2);
		ImageFiles.Save (renderTargetBitmap2, System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "text-reading-preview.png"));
		Canvas canvas = new Canvas ();
		canvas.Width = 1200.0;
		canvas.Height = 800.0;
		Canvas canvas2 = canvas;
		DockPanel dockPanel2 = (DockPanel)(window2.Content = (DockPanel)ViewZoom.Image (canvas2));
		window2.Show ();
		window2.UpdateLayout ();
		System.Windows.Controls.Panel panel3 = (System.Windows.Controls.Panel)dockPanel2.Children [0];
		ScaleTransform scaleTransform = (ScaleTransform)canvas2.LayoutTransform;
		double scaleX = scaleTransform.ScaleX;
		action ((System.Windows.Controls.Button)panel3.Children [2]);
		window2.UpdateLayout ();
		if (scaleTransform.ScaleX <= scaleX) {
			throw new Exception ("Image zoom did not enlarge canvas");
		}
		System.Windows.Point point = canvas2.TranslatePoint (new System.Windows.Point (200.0, 150.0), dockPanel2);
		System.Windows.Point point2 = dockPanel2.TranslatePoint (point, canvas2);
		if (Math.Abs (point2.X - 200.0) > 0.01 || Math.Abs (point2.Y - 150.0) > 0.01) {
			throw new Exception ("Scaled selection coordinates changed");
		}
		action ((System.Windows.Controls.Button)panel3.Children [4]);
		window2.UpdateLayout ();
		if (scaleTransform.ScaleX != 1.0) {
			throw new Exception ("Original image size failed");
		}
		action ((System.Windows.Controls.Button)panel3.Children [3]);
		window2.UpdateLayout ();
		if (Math.Abs (scaleTransform.ScaleX - scaleX) > 0.01) {
			throw new Exception ("Fit image failed");
		}
		byte[] pixels = new byte[19200];
		BitmapSource bitmap = BitmapSource.Create (80, 60, 96.0, 96.0, PixelFormats.Bgra32, null, pixels, 320);
		window4 = ImageWindows.PreviewImage (this.window, bitmap, "原文图片");
		window5 = ImageWindows.PreviewImage (this.window, bitmap, "译文图片");
		window4.UpdateLayout ();
		window5.UpdateLayout ();
		if (!window4.IsVisible || !window5.IsVisible || window4 == window5) {
			throw new Exception ("Source and translated images cannot remain open simultaneously");
		}
		File.WriteAllText (System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "zoom-test-results.txt"), "PASS: popup maximize/restore; text zoom/reset/limits; OCR text preview stays on-screen; ordinary mouse wheel scrolls long text; image editor zoom/original/fit; source and translated image windows remain open simultaneously; scaled canvas coordinates.");
	} catch (Exception ex) {
		File.WriteAllText (System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "zoom-test-results.txt"), "FAIL: " + ex);
		Environment.ExitCode = 1;
	} finally {
		selectionPopup.Window.Close ();
		if (window3 != null) {
			window3.Close ();
		}
		if (window4 != null) {
			window4.Close ();
		}
		if (window5 != null) {
			window5.Close ();
		}
		window2.Close ();
	}
}

}
}
