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
private void OpenOcr ()
{
	Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog ();
	openFileDialog.Title = "选择图片 · OCR 识图";
	openFileDialog.Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff";
	Microsoft.Win32.OpenFileDialog openFileDialog2 = openFileDialog;
	if (openFileDialog2.ShowDialog (window) == true) {
		RecognizeOnly (openFileDialog2.FileName, false);
	}
}


private async void RecognizeOnly (string path, bool temporary)
{
	System.Windows.Controls.TextBox text = new System.Windows.Controls.TextBox {
		Text = "正在识别图片文字…",
		IsReadOnly = true,
		AcceptsReturn = true,
		TextWrapping = TextWrapping.Wrap,
		VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
		Margin = new Thickness (0.0, 14.0, 0.0, 14.0),
		Padding = new Thickness (14.0),
		FontSize = 16.0
	};
	DockPanel root = new DockPanel {
		Margin = new Thickness (20.0)
	};
	TextBlock hint = Label ("OCR 识图 · 本机识别", true);
	DockPanel.SetDock (hint, Dock.Top);
	root.Children.Add (hint);
	WrapPanel actions = new WrapPanel {
		Orientation = System.Windows.Controls.Orientation.Horizontal
	};
	DockPanel.SetDock (actions, Dock.Bottom);
	root.Children.Add (actions);
	root.Children.Add (text);
	Window dialog = Dialog ("OCR 识图 · 提取文字", 720.0, 560.0, root);
	dialog.MinWidth = 520.0;
	dialog.MinHeight = 360.0;
	bool closed = false;
	dialog.Closed += delegate {
		closed = true;
	};
	System.Windows.Controls.Button copy = ActionButton ("复制文字", delegate {
		System.Windows.Clipboard.SetText (text.Text);
		hint.Text = "已复制识别文字";
	});
	System.Windows.Controls.Button use = ActionButton ("放入翻译", delegate {
		Clear ();
		input.Text = text.Text;
		dialog.Close ();
		Show ();
	});
	bool isEnabled = (use.IsEnabled = false);
	copy.IsEnabled = isEnabled;
	actions.Children.Add (ViewZoom.Text (text));
	actions.Children.Add (copy);
	actions.Children.Add (use);
	actions.Children.Add (ActionButton ("最大化 / 还原", delegate {
		dialog.WindowState = ((dialog.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}));
	actions.Children.Add (ActionButton ("关闭", delegate {
		dialog.Close ();
	}));
	dialog.Show ();
	try {
		int num = default(int);
		int num2 = num;
		int num3 = 0;
		try {
			OcrDocument result = await OcrService.Ocr (path, prefs.OcrLanguage);
			if (!closed) {
				text.Text = string.Join (Environment.NewLine, result.Regions.Select ((Region r) => r.Text));
				text.IsReadOnly = false;
				hint.Text = ((result.Regions.Count == 0) ? "未识别到文字，请换用清晰图片或检查识别语言。" : ("识别完成 · " + result.Regions.Count + " 个文字区域 · 可直接编辑"));
				bool isEnabled2 = (use.IsEnabled = text.Text.Length > 0);
				copy.IsEnabled = isEnabled2;
			}
		} catch (Exception ex) {
			if (!closed) {
				text.Text = "";
				hint.Text = "识别失败：" + ex.Message;
			}
		}
	} finally {
		if (temporary && System.IO.Path.GetDirectoryName (System.IO.Path.GetFullPath (path)) == Store.Root && System.IO.Path.GetFileName (path).StartsWith ("capture-")) {
			try {
				File.Delete (path);
			} catch {
			}
		}
	}
}

}
}
