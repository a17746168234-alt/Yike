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
private IntPtr Hook (IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
{
	if (msg == 786 && w.ToInt32 () == 1) {
		handled = true;
		SelectedText ();
	}
	return IntPtr.Zero;
}


private void CloseActiveSelection ()
{
	if (activeSelectionCancel != null) {
		try {
			activeSelectionCancel.Cancel ();
		} catch {
		}
		activeSelectionCancel = null;
	}
	if (activeSelectionPopup != null) {
		try {
			activeSelectionPopup.Close ();
		} catch {
		}
		activeSelectionPopup = null;
	}
}


private async void SelectedText ()
{
	if (selecting) {
		return;
	}
	CloseActiveSelection ();
	selecting = true;
	string text;
	try {
		int num = default(int);
		int num2 = num;
		int num3 = 0;
		try {
			text = await SelectionReader.Capture ();
		} catch (Exception ex) {
			SelectionError ("划词翻译失败：" + ex.Message);
			return;
		}
	} finally {
		selecting = false;
	}
	TranslateSelection (text);
}


private void TranslateSelection (string text)
{
	string sourceCode = Code (source);
	string targetCode = Code (target);
	SelectionPopup view = CreateSelectionPopup (text, targetCode, sourceCode);
	Window popup = view.Window;
	activeSelectionPopup = popup;
	popup.Closed += delegate {
		if (activeSelectionPopup == popup) {
			if (activeSelectionCancel != null) {
				activeSelectionCancel.Cancel ();
			}
			activeSelectionCancel = null;
			activeSelectionPopup = null;
		}
	};
	view.TargetLanguage.SelectionChanged += delegate {
		if (activeSelectionPopup == popup) {
			string text2 = Code (view.TargetLanguage);
			Select (target, text2);
			TranslateSelectionInPopup (text, sourceCode, text2, view);
		}
	};
	popup.Show ();
	TranslateSelectionInPopup (text, sourceCode, targetCode, view);
}


private async void TranslateSelectionInPopup (string text, string sourceCode, string targetCode, SelectionPopup view)
{
	Window popup = view.Window;
	if (activeSelectionPopup != popup) {
		return;
	}
	if (activeSelectionCancel != null) {
		activeSelectionCancel.Cancel ();
	}
	CancellationTokenSource cancel = (activeSelectionCancel = new CancellationTokenSource ());
	int index = Array.IndexOf (codes, targetCode);
	view.Window.Title = "划词翻译 · Yike";
	((TextBlock)popup.FindName ("Language")).Text = "译文 · " + ((index >= 0) ? names [index] : targetCode);
	view.Succeeded = false;
	view.Result.Text = "";
	view.Copy.IsEnabled = false;
	view.Loading.Visibility = Visibility.Visible;
	view.State.Text = "正在翻译…";
	try {
		int num = default(int);
		int num2 = num;
		int num3 = 0;
		try {
			List<string> result = await new DeepL (Store.Key).Translate (new string[1] { text }, LanguageDetector.ResolveForDeepL (sourceCode, text), targetCode, null, cancel.Token);
			if (!cancel.IsCancellationRequested && activeSelectionCancel == cancel) {
				view.Complete (result [0], true);
			}
		} catch (OperationCanceledException) {
		} catch (Exception ex2) {
			if (!cancel.IsCancellationRequested && activeSelectionCancel == cancel) {
				view.Complete (ex2.Message, false);
			}
		}
	} finally {
		if (activeSelectionCancel == cancel) {
			activeSelectionCancel = null;
		}
		cancel.Dispose ();
	}
}


private void SelectionError (string message)
{
	CloseActiveSelection ();
	Status (message);
	SelectionPopup view = CreateSelectionPopup ("未获取到可翻译的文字", Code (target));
	activeSelectionPopup = view.Window;
	view.Complete (message, false);
	view.Window.Closed += delegate {
		if (activeSelectionPopup == view.Window) {
			activeSelectionPopup = null;
		}
	};
	view.Window.Show ();
}

}
}
