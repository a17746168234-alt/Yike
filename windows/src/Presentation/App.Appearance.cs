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
private string LightOverlayColor (string color)
{
	string value;
	if (!LightOverlayPalette.TryGetValue (color, out value)) {
		return color;
	}
	return value;
}


private SolidColorBrush ThemeBrush (string color)
{
	return new SolidColorBrush ((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString (color));
}


private bool IsDarkAppearance ()
{
	if (prefs.Appearance == "dark") {
		return true;
	}
	if (prefs.Appearance == "light") {
		return false;
	}
	try {
		return Convert.ToInt32 (Registry.GetValue ("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "AppsUseLightTheme", 1)) == 0;
	} catch {
		return false;
	}
}


private void ApplyAppearance ()
{
	bool flag = IsDarkAppearance ();
	string[] array = new string[5] { "WindowBrush", "PanelBrush", "InkBrush", "MutedBrush", "LineBrush" };
	string[] array2 = (flag ? new string[5] { "#090D15", "#111827", "#F7F9FC", "#96A2B7", "#263246" } : new string[5] { "#F3F6FC", "#FFFFFF", "#172033", "#697386", "#DDE5F0" });
	for (int i = 0; i < array.Length; i++) {
		window.Resources [array [i]] = ThemeBrush (array2 [i]);
	}
	window.Resources ["BackdropBrush"] = ThemeBrush (flag ? "#070B12" : "#EDF2F9");
	window.Resources ["SurfaceBrush"] = ThemeBrush (flag ? "#111827" : "#FCFDFF");
	window.Resources ["ControlBrush"] = ThemeBrush (flag ? "#1B2636" : "#F1F4F9");
	window.Resources ["SoftAccentBrush"] = ThemeBrush (flag ? "#29265C" : "#ECEBFF");
	IntPtr handle = new WindowInteropHelper (window).Handle;
	if (handle != IntPtr.Zero) {
		int value = (flag ? 1 : 0);
		Native.DwmSetWindowAttribute (handle, 20, ref value, 4);
		HwndSource hwndSource = HwndSource.FromHwnd (handle);
		if (hwndSource != null) {
			hwndSource.CompositionTarget.BackgroundColor = (flag ? ((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString ("#090D15")) : ((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString ("#F3F6FC")));
		}
	}
	DialogChrome.SyncOwnerPalette (window);
}

}
}
