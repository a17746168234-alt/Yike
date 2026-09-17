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
public static class Store
{
	public static readonly string Root;

	private static readonly string LegacyRoot;

	public static readonly JavaScriptSerializer Json;

	public static string Key {
		get {
			try {
				return Encoding.UTF8.GetString (ProtectedData.Unprotect (File.ReadAllBytes (System.IO.Path.Combine (Root, "key.bin")), null, DataProtectionScope.CurrentUser));
			} catch {
				return "";
			}
		}
		set {
			Directory.CreateDirectory (Root);
			string text = System.IO.Path.Combine (Root, "key.bin");
			string text2 = text + ".tmp";
			if (string.IsNullOrWhiteSpace (value)) {
				if (File.Exists (text)) {
					File.Delete (text);
				}
				if (File.Exists (text2)) {
					File.Delete (text2);
				}
			} else {
				byte[] bytes = ProtectedData.Protect (Encoding.UTF8.GetBytes (value.Trim ()), null, DataProtectionScope.CurrentUser);
				File.WriteAllBytes (text2, bytes);
				if (File.Exists (text)) {
					File.Replace (text2, text, null);
				} else {
					File.Move (text2, text);
				}
			}
		}
	}

	static Store ()
	{
		Root = System.IO.Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Yike");
		LegacyRoot = System.IO.Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "WindowsTranslator");
		Json = new JavaScriptSerializer {
			MaxJsonLength = 16000000
		};
		MigrateLegacyData ();
	}

	private static void MigrateLegacyData ()
	{
		try {
			if (!Directory.Exists (LegacyRoot)) {
				return;
			}
			string[] files = Directory.GetFiles (LegacyRoot, "*", SearchOption.AllDirectories);
			foreach (string text in files) {
				string path = text.Substring (LegacyRoot.Length).TrimStart (System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
				string text2 = System.IO.Path.Combine (Root, path);
				if (!File.Exists (text2)) {
					Directory.CreateDirectory (System.IO.Path.GetDirectoryName (text2));
					File.Copy (text, text2);
				}
			}
		} catch {
		}
	}

	public static T Read<T> (string name, T fallback)
	{
		try {
			return Json.Deserialize<T> (File.ReadAllText (System.IO.Path.Combine (Root, name), Encoding.UTF8));
		} catch {
			return fallback;
		}
	}

	public static void Write (string name, object data)
	{
		Directory.CreateDirectory (Root);
		string text = System.IO.Path.Combine (Root, name);
		File.WriteAllText (text + ".tmp", Json.Serialize (data), Encoding.UTF8);
		if (File.Exists (text)) {
			File.Replace (text + ".tmp", text, null);
		} else {
			File.Move (text + ".tmp", text);
		}
	}

	public static List<Entry> TrimHistory (IEnumerable<Entry> entries)
	{
		return (from e in entries
			orderby e.Pinned descending, e.Date descending
			select e).ToList ();
	}
}

}
