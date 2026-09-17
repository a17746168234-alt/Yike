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
internal sealed class WhisperStreamParser
{
	private readonly StringBuilder line = new StringBuilder ();

	private bool escape;

	private bool csi;

	private string preview = "";

	public event Action<string, bool> Text;

	public event Action Ready;

	public void Feed (string chunk)
	{
		foreach (char c in chunk) {
			if (escape) {
				if (!csi && c == '[') {
					csi = true;
				} else if (!csi || (c >= '@' && c <= '~')) {
					escape = false;
					csi = false;
				}
				continue;
			}
			switch (c) {
			case '\u001b':
				escape = true;
				break;
			case '\r':
				if (line.ToString ().Trim () == "[Start speaking]") {
					if (this.Ready != null) {
						this.Ready ();
					}
				} else {
					Preview ();
				}
				line.Clear ();
				break;
			case '\n':
				Commit ();
				line.Clear ();
				break;
			default:
				if (!char.IsControl (c) && line.Length < 16384) {
					line.Append (c);
				}
				break;
			}
		}
	}

	public void Preview ()
	{
		string text = line.ToString ().Trim ();
		if (text == "[Start speaking]") {
			return;
		}
		string text2 = Clean (text);
		if (text2.Length != 0 && !(text2 == preview)) {
			preview = text2;
			if (this.Text != null) {
				this.Text (text2, false);
			}
		}
	}

	public void Complete ()
	{
		Commit ();
		line.Clear ();
	}

	private void Commit ()
	{
		if (line.ToString ().Trim () == "[Start speaking]") {
			if (this.Ready != null) {
				this.Ready ();
			}
			return;
		}
		string text = Clean (line.ToString ());
		if (text.Length == 0) {
			text = preview;
		}
		if (text.Length > 0 && this.Text != null) {
			this.Text (text, true);
		}
		preview = "";
	}

	internal static string Clean (string raw)
	{
		if (string.IsNullOrWhiteSpace (raw)) {
			return "";
		}
		string input = Regex.Replace (raw, "\\x1B\\[[0-9;]*[A-Za-z]", "");
		input = Regex.Replace (input, "<\\|[^>]+\\|>|\\[(?:BLANK_AUDIO|SILENCE|NO_SPEECH)\\]", "", RegexOptions.IgnoreCase).Trim ();
		if (!(input == "[Start speaking]")) {
			return input;
		}
		return "";
	}
}

}
