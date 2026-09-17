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
private sealed class SelectionPopup
{
	public Window Window;

	public System.Windows.Controls.TextBox Result;

	public TextBlock State;

	public System.Windows.Controls.Button Copy;

	public System.Windows.Controls.ComboBox TargetLanguage;

	public System.Windows.Controls.ProgressBar Loading;

	public bool Succeeded;

	public void Complete (string text, bool success)
	{
		Succeeded = success;
		Result.Text = text;
		State.Text = (success ? "翻译完成" : "暂时无法翻译");
		Loading.Visibility = Visibility.Collapsed;
		Copy.IsEnabled = success;
	}
}


private SelectionPopup CreateSelectionPopup (string original, string targetCode, string sourceCode = "auto")
{
	Window popup;
	using (FileStream stream = File.OpenRead (System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "SelectionWindow.xaml"))) {
		popup = (Window)XamlReader.Load ((Stream)stream);
	}
	popup.Title = "划词翻译 · Yike";
	popup.Icon = window.Icon;
	SelectionPopup view = new SelectionPopup {
		Window = popup,
		Result = (System.Windows.Controls.TextBox)popup.FindName ("Translation"),
		State = (TextBlock)popup.FindName ("State"),
		Copy = (System.Windows.Controls.Button)popup.FindName ("Copy"),
		Loading = (System.Windows.Controls.ProgressBar)popup.FindName ("Loading"),
		TargetLanguage = (System.Windows.Controls.ComboBox)popup.FindName ("PopupTargetLanguage")
	};
	for (int i = 1; i < codes.Length; i++) {
		view.TargetLanguage.Items.Add (new ComboBoxItem {
			Content = names [i],
			Tag = codes [i]
		});
	}
	Select (view.TargetLanguage, targetCode);
	((System.Windows.Controls.TextBox)popup.FindName ("Original")).Text = original;
	int num = Array.IndexOf (codes, sourceCode);
	((TextBlock)popup.FindName ("OriginalLanguage")).Text = "原文 · " + ((num >= 0) ? names [num] : sourceCode);
	int num2 = Array.IndexOf (codes, targetCode);
	((TextBlock)popup.FindName ("Language")).Text = "译文 · " + ((num2 >= 0) ? names [num2] : targetCode);
	((System.Windows.Controls.Button)popup.FindName ("ClosePopup")).Click += delegate {
		popup.Close ();
	};
	((System.Windows.Controls.Button)popup.FindName ("MinimizePopup")).Click += delegate {
		popup.WindowState = WindowState.Minimized;
	};
	popup.PreviewKeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e) {
		if (e.Key == Key.Escape) {
			e.Handled = true;
			popup.Close ();
		}
	};
	System.Windows.Controls.Button maximize = (System.Windows.Controls.Button)popup.FindName ("MaximizePopup");
	Action toggleSize = delegate {
		popup.MaxHeight = SystemParameters.WorkArea.Height;
		popup.WindowState = ((popup.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	};
	maximize.Click += delegate {
		toggleSize ();
	};
	popup.StateChanged += delegate {
		maximize.ToolTip = ((popup.WindowState == WindowState.Maximized) ? "还原窗口" : "最大化");
	};
	((Grid)popup.FindName ("DragHeader")).MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) {
		if (!(e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase)) {
			if (e.ClickCount == 2) {
				toggleSize ();
				e.Handled = true;
			} else if (e.LeftButton == MouseButtonState.Pressed && popup.WindowState == WindowState.Normal) {
				popup.DragMove ();
			}
		}
	};
	((ContentControl)popup.FindName ("TextZoom")).Content = ViewZoom.Text ((System.Windows.Controls.TextBox)popup.FindName ("Original"), view.Result);
	System.Windows.Controls.Button pin = (System.Windows.Controls.Button)popup.FindName ("Pin");
	pin.Click += delegate {
		popup.Topmost = !popup.Topmost;
		pin.Content = (popup.Topmost ? "已置顶" : "置顶");
	};
	view.Copy.Click += delegate {
		try {
			System.Windows.Clipboard.SetText (view.Result.Text);
			view.State.Text = "已复制到剪贴板";
		} catch {
			view.State.Text = "剪贴板忙，请重试";
		}
	};
	((System.Windows.Controls.Button)popup.FindName ("OpenMain")).Click += delegate {
		if (!string.IsNullOrEmpty (original)) {
			Clear ();
			Select (source, sourceCode);
			Select (target, Code (view.TargetLanguage));
			input.Text = original;
			if (view.Succeeded) {
				output.Text = view.Result.Text;
				UpdateResult ();
			}
		}
		Show ();
		popup.Close ();
	};
	return view;
}


private void SelectionPreview (bool error, bool loading)
{
	SelectionPopup selectionPopup = CreateSelectionPopup ("Good design makes everyday things feel effortless.", "ZH-HANS");
	if (!loading) {
		selectionPopup.Complete (error ? "无法连接翻译服务，请检查网络连接后重新使用快捷键。" : "好的设计，让日常变得轻松自然。", !error);
	}
	selectionPopup.Window.Show ();
	selectionPopup.Window.UpdateLayout ();
	FrameworkElement frameworkElement = (FrameworkElement)selectionPopup.Window.Content;
	RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap ((int)frameworkElement.ActualWidth, (int)frameworkElement.ActualHeight, 96.0, 96.0, PixelFormats.Pbgra32);
	renderTargetBitmap.Render (frameworkElement);
	ImageFiles.Save (renderTargetBitmap, System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "selection-" + prefs.Appearance + (error ? "-error" : (loading ? "-loading" : "")) + ".png"));
	selectionPopup.Window.Close ();
}

}
}
