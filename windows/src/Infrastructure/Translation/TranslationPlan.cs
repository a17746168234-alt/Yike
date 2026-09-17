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
public sealed class TranslationPlan
{
	public readonly List<string> Units = new List<string> ();

	private readonly string[] lines;

	private readonly List<int> lineIndexes = new List<int> ();

	private TranslationPlan (string text)
	{
		lines = (text ?? "").Replace ("\r\n", "\n").Replace ('\r', '\n').Split ('\n');
		for (int i = 0; i < lines.Length; i++) {
			if (!string.IsNullOrWhiteSpace (lines [i])) {
				lineIndexes.Add (i);
				Units.Add (lines [i]);
			}
		}
	}

	public static TranslationPlan Create (string text)
	{
		return new TranslationPlan (text);
	}

	public string Compose (IList<string> translated)
	{
		string[] array = new string[lines.Length];
		for (int i = 0; i < array.Length; i++) {
			array [i] = "";
		}
		for (int j = 0; j < lineIndexes.Count && j < translated.Count; j++) {
			array [lineIndexes [j]] = translated [j] ?? "";
		}
		return string.Join (Environment.NewLine, array);
	}
}

}
