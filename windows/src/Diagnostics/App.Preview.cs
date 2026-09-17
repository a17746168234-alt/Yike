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
private void SchedulePreview (string[] args)
{
	if (args.Contains ("--render-preview")) {
		window.Dispatcher.BeginInvoke ((Action)async delegate {
			prefs.Appearance = (args.Contains ("--dark") ? "dark" : "light");
			ApplyAppearance ();
			await RunPreview (args);
			exiting = true;
			window.Close ();
		}, DispatcherPriority.ApplicationIdle);
	}
}


private async Task RunPreview (string[] args)
{
	if (args.Contains ("--zoom-test")) {
		VerifyZoom ();
	} else if (args.Contains ("--selection-preview")) {
		SelectionPreview (args.Contains ("--error"), args.Contains ("--loading"));
	} else if (args.Contains ("--region-preview")) {
		BitmapSource bitmap = ImageFiles.Load (System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "region-preview-input.png"));
		RegionOcr (bitmap, true);
	} else if (args.Contains ("--speech-test")) {
		await Tests.SpeechIntegration ();
	} else if (args.Contains ("--theme-test")) {
		VerifyTheme ();
	} else if (args.Contains ("--ui-audit-test")) {
		VerifyUiAudit ();
	} else if (args.Contains ("--settings-preview")) {
		SettingsPreview ();
	} else if (args.Contains ("--engine-preview")) {
		EnginePreview ();
	} else if (args.Contains ("--history-preview")) {
		HistoryPreview ();
	} else if (args.Contains ("--deepl-help-preview")) {
		DeepLHelpPreview ();
	} else if (args.Contains ("--about-preview")) {
		AboutPreview ();
	} else {
		RenderMainWindowPreview (args.Contains ("--dark"));
	}
}


private void RenderMainWindowPreview (bool dark)
{
	Status ("Enter 翻译 · Shift+Enter 换行 · Ctrl+Shift+F 划词翻译");
	window.UpdateLayout ();
	FrameworkElement frameworkElement = (FrameworkElement)window.Content;
	DrawingVisual drawingVisual = new DrawingVisual ();
	using (DrawingContext drawingContext = drawingVisual.RenderOpen ()) {
		Rect rectangle = new Rect (0.0, 0.0, frameworkElement.ActualWidth, frameworkElement.ActualHeight);
		System.Windows.Media.Color color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString (dark ? "#181B22" : "#F4F5F8");
		drawingContext.DrawRectangle (new SolidColorBrush (color), null, rectangle);
		drawingContext.DrawRectangle (new VisualBrush (frameworkElement), null, rectangle);
	}
	RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap ((int)frameworkElement.ActualWidth, (int)frameworkElement.ActualHeight, 96.0, 96.0, PixelFormats.Pbgra32);
	renderTargetBitmap.Render (drawingVisual);
	ImageFiles.Save (renderTargetBitmap, System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, dark ? "preview-dark.png" : "preview.png"));
}

}
}
