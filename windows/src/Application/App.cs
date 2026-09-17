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
[STAThread]
public static void Main (string[] args)
{
	ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
	if (args.Contains ("--self-test")) {
		Tests.Run ();
		return;
	}
	if (args.Contains ("--whisper-input-test")) {
		Tests.WhisperInputIntegration ();
		return;
	}
	if (!args.Contains ("--render-preview")) {
		bool createdNew;
		singleton = new Mutex (true, "Local\\Yike.Desktop", out createdNew);
		activation = new EventWaitHandle (false, EventResetMode.AutoReset, "Local\\Yike.Activate");
		if (!createdNew) {
			activation.Set ();
			return;
		}
	}
	System.Windows.Application application = new System.Windows.Application ();
	application.ShutdownMode = ShutdownMode.OnMainWindowClose;
	application.DispatcherUnhandledException += delegate(object s, DispatcherUnhandledExceptionEventArgs e) {
		System.Windows.MessageBox.Show (e.Exception.Message, "Yike");
		e.Handled = true;
	};
	new App ().Start (application, args);
	application.Run ();
}


private void Button (string name, Action action)
{
	Find<System.Windows.Controls.Button> (name).Click += delegate {
		action ();
	};
}


private void Status (string text)
{
	status.Text = text;
}


private void Start (System.Windows.Application app, string[] args)
{
	previewMode = args.Contains ("--render-preview");
	prefs = Store.Read ("preferences.json", new Preferences ());
	history = Store.Read ("history.json", new List<Entry> ());
	using (FileStream stream = File.OpenRead (System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "MainWindow.xaml"))) {
		window = (Window)XamlReader.Load ((Stream)stream);
	}
	app.Resources = window.Resources;
	app.MainWindow = window;
	input = Find<System.Windows.Controls.TextBox> ("SourceText");
	output = Find<System.Windows.Controls.TextBox> ("ResultText");
	status = Find<TextBlock> ("Status");
	source = Find<System.Windows.Controls.ComboBox> ("SourceLanguage");
	target = Find<System.Windows.Controls.ComboBox> ("TargetLanguage");
	if (activation != null) {
		ThreadPool.RegisterWaitForSingleObject (activation, delegate {
			window.Dispatcher.BeginInvoke (new Action (Show));
		}, null, -1, false);
	}
	defaultAppIcon = new BitmapImage (new Uri (System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "app.png")));
	Find<System.Windows.Controls.Image> ("AppIcon").Source = defaultAppIcon;
	window.Icon = defaultAppIcon;
	ApplyAccountIdentity ();
	for (int num = 0; num < codes.Length; num++) {
		source.Items.Add (new ComboBoxItem {
			Content = names [num],
			Tag = codes [num]
		});
		if (num > 0) {
			target.Items.Add (new ComboBoxItem {
				Content = names [num],
				Tag = codes [num]
			});
		}
	}
	Select (source, prefs.Source);
	Select (target, prefs.Target);
	BindTextInput ();
	BindMainWindowActions ();
	window.SourceInitialized += delegate {
		IntPtr handle = new WindowInteropHelper (window).Handle;
		HwndSource.FromHwnd (handle).AddHook (Hook);
		if (!args.Contains ("--render-preview") && !Native.RegisterHotKey (handle, 1, 16390u, 70u)) {
			SelectionError ("Ctrl+Shift+F 注册失败，可能已被其他程序占用。请关闭冲突程序后重启 Yike。");
		}
		ApplyAppearance ();
	};
	window.Closing += delegate(object s, CancelEventArgs e) {
		if (!exiting) {
			ResetVoiceShortcut (true);
			e.Cancel = true;
			window.Hide ();
		}
	};
	window.Closed += delegate {
		Cancel ();
		CloseActiveSelection ();
		speech.Dispose ();
		voiceInput.Dispose ();
		if (tray != null) {
			tray.Dispose ();
		}
		Native.UnregisterHotKey (new WindowInteropHelper (window).Handle, 1);
	};
	Icon icon;
	try {
		icon = new Icon (System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "app.ico"));
	} catch {
		icon = SystemIcons.Application;
	}
	tray = new NotifyIcon {
		Text = "Yike",
		Icon = icon,
		Visible = true
	};
	ApplyAccountIdentity ();
	ContextMenuStrip contextMenuStrip = new ContextMenuStrip ();
	contextMenuStrip.Items.Add ("显示翻译", null, delegate {
		Show ();
	});
	contextMenuStrip.Items.Add ("完全退出", null, delegate {
		exiting = true;
		window.Close ();
	});
	tray.ContextMenuStrip = contextMenuStrip;
	tray.DoubleClick += delegate {
		Show ();
	};
	SystemEvents.UserPreferenceChanged += SystemThemeChanged;
	window.Closed += delegate {
		SystemEvents.UserPreferenceChanged -= SystemThemeChanged;
	};
	ApplyAppearance ();
	window.Show ();
	SchedulePreview (args);
}


private void Show ()
{
	window.Show ();
	window.WindowState = WindowState.Normal;
	window.Activate ();
}


private void Select (System.Windows.Controls.ComboBox combo, string code)
{
	foreach (ComboBoxItem item in (IEnumerable)combo.Items) {
		if ((string)item.Tag == code) {
			combo.SelectedItem = item;
			return;
		}
	}
	combo.SelectedIndex = 0;
}


private string Code (System.Windows.Controls.ComboBox combo)
{
	return (string)((ComboBoxItem)combo.SelectedItem).Tag;
}


private void SavePreferences ()
{
	if (!previewMode) {
		Store.Write ("preferences.json", prefs);
	}
}

}
}
