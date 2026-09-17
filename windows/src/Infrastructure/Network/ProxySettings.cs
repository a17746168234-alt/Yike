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
internal static class ProxySettings
{
	private static readonly Lazy<string> current = new Lazy<string> (Resolve);

	public static string Current {
		get {
			return current.Value;
		}
	}

	private static string Resolve ()
	{
		string[] array = new string[3] { "YIKE_PROXY", "HTTPS_PROXY", "HTTP_PROXY" };
		foreach (string variable in array) {
			string text = Normalize (Environment.GetEnvironmentVariable (variable));
			if (text != null) {
				return text;
			}
		}
		int[] array2 = new int[5] { 3067, 7890, 10809, 8080, 8888 };
		foreach (int num in array2) {
			if (IsListening (num)) {
				return "http://127.0.0.1:" + num;
			}
		}
		return null;
	}

	internal static string Normalize (string value)
	{
		if (string.IsNullOrWhiteSpace (value)) {
			return null;
		}
		value = value.Trim ();
		if (!value.Contains ("://")) {
			value = "http://" + value;
		}
		Uri result;
		if (!Uri.TryCreate (value, UriKind.Absolute, out result)) {
			return null;
		}
		if (result.Scheme != "http" && result.Scheme != "https") {
			return null;
		}
		if (string.IsNullOrWhiteSpace (result.Host) || result.Port <= 0) {
			return null;
		}
		return result.AbsoluteUri.TrimEnd ('/');
	}

	private static bool IsListening (int port)
	{
		using (TcpClient tcpClient = new TcpClient ()) {
			try {
				IAsyncResult asyncResult = tcpClient.BeginConnect (IPAddress.Loopback, port, null, null);
				if (!asyncResult.AsyncWaitHandle.WaitOne (120)) {
					return false;
				}
				tcpClient.EndConnect (asyncResult);
				return true;
			} catch {
				return false;
			}
		}
	}

	public static HttpClientHandler CreateHandler (string proxy)
	{
		HttpClientHandler httpClientHandler = new HttpClientHandler ();
		if (proxy != null) {
			httpClientHandler.Proxy = new WebProxy (proxy);
			httpClientHandler.UseProxy = true;
		}
		return httpClientHandler;
	}
}

}
