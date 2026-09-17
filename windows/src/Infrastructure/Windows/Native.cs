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
public static class Native
{
	public struct Margins
	{
		public int Left;

		public int Right;

		public int Top;

		public int Bottom;
	}

	[DllImport ("dwmapi.dll")]
	public static extern int DwmExtendFrameIntoClientArea (IntPtr h, ref Margins margins);

	[DllImport ("user32.dll")]
	public static extern bool SetWindowPos (IntPtr h, IntPtr after, int x, int y, int width, int height, uint flags);

	[DllImport ("user32.dll")]
	public static extern bool RegisterHotKey (IntPtr h, int id, uint modifiers, uint key);

	[DllImport ("user32.dll")]
	public static extern bool UnregisterHotKey (IntPtr h, int id);

	[DllImport ("user32.dll")]
	public static extern uint GetClipboardSequenceNumber ();

	[DllImport ("dwmapi.dll")]
	public static extern int DwmSetWindowAttribute (IntPtr h, int attribute, ref int value, int size);
}

}
