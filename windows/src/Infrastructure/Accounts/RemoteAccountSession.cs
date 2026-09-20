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
public sealed class RemoteAccountSession
{
	public string Token { get; set; }

	public string Email { get; set; }

	public string Username { get; set; }

	public string DisplayName { get; set; }

	public string AvatarPngBase64 { get; set; }

	public long Granted { get; set; }

	public long Used { get; set; }

	public long Remaining { get; set; }
}


internal static class RemoteAccountSessionStore
{
	private static string Path {
		get { return System.IO.Path.Combine (Store.Root, "remote-session.bin"); }
	}

	public static RemoteAccountSession Load ()
	{
		try {
			byte[] protectedBytes = File.ReadAllBytes (Path);
			byte[] bytes = ProtectedData.Unprotect (protectedBytes, null, DataProtectionScope.CurrentUser);
			RemoteAccountSession session = Store.Json.Deserialize<RemoteAccountSession> (Encoding.UTF8.GetString (bytes));
			if (session == null || string.IsNullOrWhiteSpace (session.Token) || session.Token.Length > 128) {
				return null;
			}
			RemoteAccountCustomizationStore.Apply (session);
			return session;
		} catch {
			return null;
		}
	}

	public static void Save (RemoteAccountSession session)
	{
		if (session == null || string.IsNullOrWhiteSpace (session.Token)) {
			Clear ();
			return;
		}
		Directory.CreateDirectory (Store.Root);
		string path = Path;
		string temp = path + ".tmp";
		byte[] bytes = Encoding.UTF8.GetBytes (Store.Json.Serialize (session));
		File.WriteAllBytes (temp, ProtectedData.Protect (bytes, null, DataProtectionScope.CurrentUser));
		if (File.Exists (path)) File.Replace (temp, path, null); else File.Move (temp, path);
	}

	public static void Clear ()
	{
		try { if (File.Exists (Path)) File.Delete (Path); } catch { }
		try { if (File.Exists (Path + ".tmp")) File.Delete (Path + ".tmp"); } catch { }
	}
}


internal sealed class RemoteAccountCustomization
{
	public string Email { get; set; }

	public string DisplayName { get; set; }

	public string AvatarPngBase64 { get; set; }
}


internal static class RemoteAccountCustomizationStore
{
	private static string Path {
		get { return System.IO.Path.Combine (Store.Root, "remote-profiles.bin"); }
	}

	public static void Apply (RemoteAccountSession session)
	{
		if (session == null || string.IsNullOrWhiteSpace (session.Email)) return;
		RemoteAccountCustomization profile = Load ().FirstOrDefault (item => string.Equals (item.Email, session.Email, StringComparison.OrdinalIgnoreCase));
		if (profile == null) return;
		session.DisplayName = profile.DisplayName;
		session.AvatarPngBase64 = profile.AvatarPngBase64;
	}

	public static void Save (RemoteAccountSession session)
	{
		if (session == null || string.IsNullOrWhiteSpace (session.Email)) throw new InvalidOperationException ("请先登录账号。");
		List<RemoteAccountCustomization> profiles = Load ();
		RemoteAccountCustomization profile = profiles.FirstOrDefault (item => string.Equals (item.Email, session.Email, StringComparison.OrdinalIgnoreCase));
		if (profile == null) {
			profile = new RemoteAccountCustomization { Email = session.Email.ToLowerInvariant () };
			profiles.Add (profile);
		}
		profile.DisplayName = session.DisplayName;
		profile.AvatarPngBase64 = session.AvatarPngBase64;
		Directory.CreateDirectory (Store.Root);
		string path = Path;
		string temp = path + ".tmp";
		byte[] bytes = Encoding.UTF8.GetBytes (Store.Json.Serialize (profiles));
		File.WriteAllBytes (temp, ProtectedData.Protect (bytes, null, DataProtectionScope.CurrentUser));
		if (File.Exists (path)) File.Replace (temp, path, null); else File.Move (temp, path);
	}

	private static List<RemoteAccountCustomization> Load ()
	{
		try {
			byte[] protectedBytes = File.ReadAllBytes (Path);
			byte[] bytes = ProtectedData.Unprotect (protectedBytes, null, DataProtectionScope.CurrentUser);
			List<RemoteAccountCustomization> profiles = Store.Json.Deserialize<List<RemoteAccountCustomization>> (Encoding.UTF8.GetString (bytes));
			if (profiles == null || profiles.Count > 100) return new List<RemoteAccountCustomization> ();
			return profiles.Where (item => item != null && !string.IsNullOrWhiteSpace (item.Email)).ToList ();
		} catch {
			return new List<RemoteAccountCustomization> ();
		}
	}
}
}
