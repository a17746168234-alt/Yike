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
public static class SpeechText
{
	public static string NormalizeMixedLanguages (string value)
	{
		if (string.IsNullOrWhiteSpace (value)) {
			return "";
		}
		string text = value.Trim ();
		StringBuilder stringBuilder = new StringBuilder (text.Length + 8);
		char c = '\0';
		string text2 = text;
		foreach (char c2 in text2) {
			if (c != 0 && !char.IsWhiteSpace (c) && !char.IsWhiteSpace (c2) && IsCjk (c) != IsCjk (c2) && ((IsCjk (c) && IsLatinOrDigit (c2)) || (IsLatinOrDigit (c) && IsCjk (c2)))) {
				stringBuilder.Append (' ');
			}
			stringBuilder.Append (c2);
			c = c2;
		}
		return stringBuilder.ToString ();
	}

	public static string Insertion (string prefix, string spoken, string suffix)
	{
		string text = NormalizeMixedLanguages (spoken);
		if (text.Length == 0) {
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder (text.Length + 2);
		char right = text [0];
		char left = text [text.Length - 1];
		if (!string.IsNullOrEmpty (prefix)) {
			char left2 = prefix [prefix.Length - 1];
			if (NeedsSpace (left2, right, false)) {
				stringBuilder.Append (' ');
			}
		}
		stringBuilder.Append (text);
		if (!string.IsNullOrEmpty (suffix)) {
			char right2 = suffix [0];
			if (NeedsSpace (left, right2, true)) {
				stringBuilder.Append (' ');
			}
		}
		return stringBuilder.ToString ();
	}

	public static string LanguageLabel (string text)
	{
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		bool flag4 = false;
		string text2 = text ?? "";
		foreach (char c in text2) {
			flag = flag || (c >= '㐀' && c <= '鿿');
			flag2 = flag2 || (c >= '\u3040' && c <= 'ヿ');
			flag3 = flag3 || (c >= '가' && c <= '\ud7af');
			flag4 |= IsLatin (c);
		}
		if (flag4 && (flag || flag2 || flag3)) {
			return (flag2 ? "日本語" : (flag3 ? "한국어" : "中文")) + " / English";
		}
		if (flag2) {
			return "日本語";
		}
		if (flag3) {
			return "한국어";
		}
		if (flag) {
			return "中文";
		}
		if (flag4) {
			return "English";
		}
		return LanguageDetector.Detect (text).Name;
	}

	private static bool NeedsSpace (char left, char right, bool beforeSuffix)
	{
		if (char.IsWhiteSpace (left) || char.IsWhiteSpace (right)) {
			return false;
		}
		if (IsClosingPunctuation (right) || IsOpeningPunctuation (left)) {
			return false;
		}
		if (IsCjk (left) && IsCjk (right)) {
			return false;
		}
		if (IsLatinOrDigit (left) && IsLatinOrDigit (right)) {
			return true;
		}
		if ((IsCjk (left) && IsLatinOrDigit (right)) || (IsLatinOrDigit (left) && IsCjk (right))) {
			return true;
		}
		if (beforeSuffix && !char.IsPunctuation (left)) {
			return !char.IsPunctuation (right);
		}
		return false;
	}

	private static bool IsCjk (char value)
	{
		if ((value < '㐀' || value > '鿿') && (value < '\u3040' || value > 'ヿ')) {
			if (value >= '가') {
				return value <= '\ud7af';
			}
			return false;
		}
		return true;
	}

	private static bool IsLatin (char value)
	{
		if (value <= 'ɏ') {
			return char.IsLetter (value);
		}
		return false;
	}

	private static bool IsLatinOrDigit (char value)
	{
		if (!IsLatin (value)) {
			return char.IsDigit (value);
		}
		return true;
	}

	private static bool IsClosingPunctuation (char value)
	{
		return "，。！？、；：,.!?;:%)]}〉》」』".IndexOf (value) >= 0;
	}

	private static bool IsOpeningPunctuation (char value)
	{
		return "([{〈《「『".IndexOf (value) >= 0;
	}
}

}
