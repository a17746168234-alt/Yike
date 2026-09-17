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
internal sealed class SpeechDraft
{
	private string expected;

	public int Start { get; private set; }

	public int Length { get; private set; }

	public bool Active {
		get {
			return expected != null;
		}
	}

	public void Begin (string document, int start, int length)
	{
		expected = document;
		Start = Math.Max (0, Math.Min (start, document.Length));
		Length = Math.Max (0, Math.Min (length, document.Length - Start));
	}

	public void Cancel ()
	{
		expected = null;
		Length = 0;
	}

	public bool TryApply (string document, string text, bool final, out string updated, out int caret)
	{
		updated = document;
		caret = Start;
		if (!Active || document != expected) {
			Cancel ();
			return false;
		}
		if (string.IsNullOrWhiteSpace (text)) {
			return false;
		}
		string text2 = document.Substring (0, Start);
		string text3 = document.Substring (Start + Length);
		string text4 = SpeechText.Insertion (text2, text, text3);
		updated = text2 + text4 + text3;
		Length = text4.Length;
		caret = Start + Length;
		expected = updated;
		if (final) {
			Start = caret;
			Length = 0;
		}
		return true;
	}
}

}
