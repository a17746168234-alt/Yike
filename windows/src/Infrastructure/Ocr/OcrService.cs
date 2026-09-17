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
public static class OcrService
{
	public static async Task<string> InstalledLanguages ()
	{
		string output = System.IO.Path.Combine (System.IO.Path.GetTempPath (), "yike-ocr-languages-" + Guid.NewGuid ().ToString ("N") + ".json");
		try {
			using (Process process = Process.Start (new ProcessStartInfo {
				FileName = System.IO.Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe"),
				Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "ocr.ps1") + "\" -ListLanguages -OutputPath \"" + output + "\"",
				UseShellExecute = false, CreateNoWindow = true
			})) {
				if (process == null) throw new InvalidOperationException ();
				using (ProcessJob.AttachOrTerminate (process)) {
					if (!await Task.Run (() => process.WaitForExit (10000))) { process.Kill (); throw new TimeoutException (); }
					if (process.ExitCode != 0 || !File.Exists (output)) throw new InvalidOperationException ();
					OcrLanguages result = Store.Json.Deserialize<OcrLanguages> (File.ReadAllText (output));
					return result.Languages == null || result.Languages.Length == 0 ? "未安装 OCR 语言组件，请安装所需语言" : string.Join ("、", result.Languages);
				}
			}
		} catch (Exception) { return "无法检测 OCR 语言组件，请打开系统语言设置确认"; }
		finally { if (File.Exists (output)) File.Delete (output); }
	}
	private sealed class OcrLanguages { public string[] Languages { get; set; } }

	public static async Task<OcrDocument> Ocr (string path, string language)
	{
		string output = System.IO.Path.Combine (System.IO.Path.GetTempPath (), string.Concat ("translator-ocr-", Guid.NewGuid (), ".json"));
		ProcessStartInfo start = new ProcessStartInfo {
			FileName = System.IO.Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe"),
			Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "ocr.ps1") + "\" -ImagePath \"" + System.IO.Path.GetFullPath (path) + "\" -OutputPath \"" + output + "\" -Language " + language,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		try {
			Process process = Process.Start (start);
			try {
				if (process == null) {
					throw new InvalidOperationException ("Windows OCR 未能启动。");
				}
				using (ProcessJob.AttachOrTerminate (process)) {
					if (!(await Task.Run (() => process.WaitForExit (60000)))) {
						process.Kill ();
						throw new TimeoutException ("OCR 超时，请缩小图片后重试。");
					}
					if (!File.Exists (output)) {
						throw new InvalidOperationException ("Windows OCR 未能运行。");
					}
					string json = File.ReadAllText (output);
					Dictionary<string, object> dict = Store.Json.Deserialize<Dictionary<string, object>> (json);
					if (dict.ContainsKey ("Error")) {
						throw new InvalidOperationException ((string)dict ["Error"]);
					}
					return Store.Json.Deserialize<OcrDocument> (json);
				}
			} finally {
				if (process != null) {
					((IDisposable)process).Dispose ();
				}
			}
		} finally {
			if (File.Exists (output)) {
				File.Delete (output);
			}
		}
	}
}

}
