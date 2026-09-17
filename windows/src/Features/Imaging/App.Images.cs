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
private async void LoadImage (string path)
{
	Clear ();
	int ticket = revision;
	string fullPath = System.IO.Path.GetFullPath (path);
	bool temporaryCapture = string.Equals (System.IO.Path.GetDirectoryName (fullPath), Store.Root, StringComparison.OrdinalIgnoreCase) && System.IO.Path.GetFileName (fullPath).StartsWith ("capture-", StringComparison.OrdinalIgnoreCase);
	try {
		int num = default(int);
		int num2 = num;
		int num3 = 0;
		try {
			original = ImageFiles.Load (path);
			if (original.PixelWidth > 10000 || original.PixelHeight > 10000) {
				throw new InvalidOperationException ("请使用宽高不超过 10000 像素的图片。");
			}
			Find<System.Windows.Controls.Image> ("SourceImage").Source = original;
			Find<System.Windows.Controls.Image> ("SourceImage").Visibility = Visibility.Visible;
			input.Visibility = Visibility.Collapsed;
			Find<FrameworkElement> ("SourcePlaceholder").Visibility = Visibility.Collapsed;
			Status ("正在使用 Windows OCR 识别图片…");
			OcrDocument doc = await OcrService.Ocr (path, prefs.OcrLanguage);
			if (ticket == revision) {
				document = doc;
				input.Text = string.Join (Environment.NewLine, doc.Regions.Select ((Region r) => r.Text));
				Find<FrameworkElement> ("SourceImageHint").Visibility = ((doc.Regions.Count <= 0) ? Visibility.Collapsed : Visibility.Visible);
				Find<System.Windows.Controls.Button> ("ImageToolsButton").Visibility = Visibility.Visible;
				if (doc.Regions.Count == 0) {
					Status ("未识别到文字，请更换清晰图片或安装相应 Windows OCR 语言包。");
				} else {
					Translate ();
				}
			}
		} catch (Exception ex) {
			if (ticket == revision) {
				Status ("图片识别失败：" + ex.Message);
			}
		}
	} finally {
		if (temporaryCapture) {
			try {
				File.Delete (fullPath);
			} catch {
			}
		}
	}
}


private void Render ()
{
	rendered = ImageRenderer.Render (original, document);
	Find<System.Windows.Controls.Image> ("ResultImage").Source = rendered;
	Find<System.Windows.Controls.Image> ("ResultImage").Visibility = Visibility.Visible;
	Find<FrameworkElement> ("ResultImageHint").Visibility = Visibility.Visible;
	output.Visibility = Visibility.Collapsed;
	UpdateResult ();
}


private async void Capture (bool ocrOnly = false)
{
	Cancel ();
	Status ("3 秒后开始框选，请切换到目标窗口…");
	window.Hide ();
	await Task.Delay (3000);
	try {
		string text = ScreenshotCapture.Capture ();
		Show ();
		if (text != null) {
			if (ocrOnly) {
				BitmapSource bitmap = ImageFiles.Load (text);
				RegionOcr (bitmap);
				File.Delete (text);
			} else {
				LoadImage (text);
			}
		}
	} catch (Exception ex) {
		Show ();
		Status ("截图失败：" + ex.Message);
	}
}


private void ImageTools ()
{
	System.Windows.Controls.ContextMenu menu = new System.Windows.Controls.ContextMenu ();
	Action<string, Action> action = delegate(string name, Action action2) {
		System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem {
			Header = name
		};
		menuItem.Click += delegate {
			action2 ();
		};
		menu.Items.Add (menuItem);
	};
	action ("查看译图文字", delegate {
		if (document != null) {
			ImageWindows.PreviewText (window, string.Join (Environment.NewLine, document.Regions.Select ((Region r) => r.Translated)), "译文");
		}
	});
	action ("OCR 识图 / 自主框选翻译", delegate {
		if (original != null) {
			RegionOcr (original);
		}
	});
	action ("编辑原图文字 / OCR 区域", delegate {
		if (document != null) {
			ImageWindows.Edit (window, original, document, false);
			input.Text = string.Join ("\n", document.Regions.Select ((Region r) => r.Text));
			Translate ();
		}
	});
	action ("编辑译文框", delegate {
		if (document != null) {
			ImageWindows.Edit (window, original, document, true);
			Render ();
		}
	});
	action ("高对比效果", delegate {
		if (document != null) {
			foreach (Region region in document.Regions) {
				region.HighContrast = true;
			}
			Render ();
		}
	});
	action ("自然融入效果", delegate {
		if (document != null) {
			foreach (Region region2 in document.Regions) {
				region2.HighContrast = false;
			}
			Render ();
		}
	});
	action ("原图 / 译图滑杆对比", delegate {
		if (rendered != null) {
			ImageWindows.Compare (window, original, rendered);
		}
	});
	action ("保存原尺寸 PNG", delegate {
		if (rendered != null) {
			Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog {
				Filter = "PNG 图片|*.png",
				FileName = "译文.png"
			};
			if (saveFileDialog.ShowDialog (window) == true) {
				ImageFiles.Save (rendered, saveFileDialog.FileName);
				Status ("译图已保存");
			}
		}
	});
	action ("复制译文文字", delegate {
		if (output.Text.Length > 0) {
			System.Windows.Clipboard.SetText (output.Text);
		}
	});
	menu.IsOpen = true;
}

}
}
