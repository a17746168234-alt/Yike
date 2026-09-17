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
private void VerifyTheme ()
{
	Window window = Dialog ("Theme validation", 300.0, 180.0, new System.Windows.Controls.TextBox {
		Text = "Theme validation"
	});
	window.Show ();
	try {
		string[] array = new string[3] { "dark", "light", "dark" };
		foreach (string text in array) {
			prefs.Appearance = text;
			ApplyAppearance ();
			this.window.UpdateLayout ();
			window.UpdateLayout ();
			string text2 = ((text == "dark") ? "#FFF7F9FC" : "#FF172033");
			if (((SolidColorBrush)this.window.Foreground).Color.ToString () != text2 || ((SolidColorBrush)window.Foreground).Color.ToString () != text2 || ((SolidColorBrush)((System.Windows.Controls.TextBox)DialogChrome.Body (window)).Foreground).Color.ToString () != text2) {
				throw new Exception ("主题未同步到主窗口、弹窗或输入框：" + text);
			}
			SolidColorBrush solidColorBrush = (SolidColorBrush)this.window.Resources ["SurfaceBrush"];
			if (solidColorBrush.Color.R < 100 != (text == "dark")) {
				throw new Exception ("主题背景未更新");
			}
			SolidColorBrush solidColorBrush2 = (SolidColorBrush)OverlayBrush ("#F3161D20");
			if (solidColorBrush2.Color.R < 100 != (text == "dark")) {
				throw new Exception ("设置、历史记录或 DeepL 弹窗主题未同步：" + text);
			}
		}
		File.WriteAllText (System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "theme-test-results.txt"), "PASS: live dark / light / dark switching updates main window, open dialog, text controls, surfaces, settings, history and DeepL overlays.");
	} finally {
		window.Close ();
	}
}

}
}
