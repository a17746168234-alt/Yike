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
private void Toggle (StackPanel panel, string caption, bool value, Action<bool> changed)
{
	System.Windows.Controls.CheckBox checkBox = new System.Windows.Controls.CheckBox ();
	checkBox.Content = caption;
	checkBox.IsChecked = value;
	checkBox.FontSize = 15.0;
	checkBox.Foreground = OverlayBrush ("#EDF2F3");
	System.Windows.Controls.CheckBox checkBox2 = checkBox;
	checkBox2.Checked += delegate {
		changed (true);
	};
	checkBox2.Unchecked += delegate {
		changed (false);
	};
	panel.Children.Add (checkBox2);
}


private void Clear ()
{
	Cancel ();
	document = null;
	original = null;
	rendered = null;
	input.Clear ();
	ClearOutputText ();
	Find<System.Windows.Controls.Image> ("SourceImage").Visibility = Visibility.Collapsed;
	Find<System.Windows.Controls.Image> ("ResultImage").Visibility = Visibility.Collapsed;
	Find<FrameworkElement> ("SourceImageHint").Visibility = Visibility.Collapsed;
	Find<FrameworkElement> ("ResultImageHint").Visibility = Visibility.Collapsed;
	input.Visibility = Visibility.Visible;
	output.Visibility = Visibility.Visible;
	Find<System.Windows.Controls.Button> ("ImageToolsButton").Visibility = Visibility.Collapsed;
	UpdateResult ();
	Status ("Enter 翻译 · Shift+Enter 换行 · Ctrl+Shift+F 划词翻译");
}

}
}
