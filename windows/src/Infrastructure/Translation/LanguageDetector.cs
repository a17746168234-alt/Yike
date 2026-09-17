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
public static class LanguageDetector
{
	private static readonly Dictionary<string, string[]> LatinHints = new Dictionary<string, string[]> (StringComparer.OrdinalIgnoreCase) {
		{
			"EN",
			new string[12] {
				"the", "and", "is", "are", "this", "that", "you", "with", "for", "from",
				"hello", "please"
			}
		},
		{
			"DE",
			new string[12] {
				"der", "die", "das", "und", "ist", "nicht", "ich", "mit", "für", "ein",
				"eine", "bitte"
			}
		},
		{
			"FR",
			new string[12] {
				"le", "la", "les", "et", "est", "pas", "je", "avec", "pour", "une",
				"des", "bonjour"
			}
		},
		{
			"ES",
			new string[12] {
				"el", "la", "los", "las", "y", "es", "no", "con", "para", "una",
				"hola", "por"
			}
		},
		{
			"PT-BR",
			new string[12] {
				"o", "a", "os", "as", "e", "é", "não", "com", "para", "uma",
				"olá", "por"
			}
		},
		{
			"IT",
			new string[12] {
				"il", "la", "gli", "le", "e", "è", "non", "con", "per", "una",
				"ciao", "che"
			}
		}
	};

	public static DetectedLanguage Detect (string text)
	{
		if (string.IsNullOrWhiteSpace (text)) {
			return Result ("auto", "等待输入", false);
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		foreach (char c in text) {
			int num8 = c;
			if ((num8 >= 12352 && num8 <= 12543) || (num8 >= 12784 && num8 <= 12799)) {
				num2++;
			} else if ((num8 >= 44032 && num8 <= 55215) || (num8 >= 4352 && num8 <= 4607)) {
				num3++;
			} else if ((num8 >= 19968 && num8 <= 40959) || (num8 >= 13312 && num8 <= 19903)) {
				num++;
			} else if (num8 >= 1024 && num8 <= 1327) {
				num4++;
			} else if ((num8 >= 1536 && num8 <= 1791) || (num8 >= 1872 && num8 <= 1919)) {
				num5++;
			} else if (num8 >= 880 && num8 <= 1023) {
				num6++;
			} else if (char.IsLetter (c)) {
				num7++;
			}
		}
		if (num2 > 0) {
			return Result ("JA", "日本語", true);
		}
		if (num3 > 0) {
			return Result ("KO", "한국어", true);
		}
		if (num > 0) {
			return Result ("ZH-HANS", "中文", true);
		}
		if (num4 > 0) {
			return Result ("RU", "Русский", true);
		}
		if (num5 > 0) {
			return Result ("AR", "العربية", true);
		}
		if (num6 > 0) {
			return Result ("EL", "Ελληνικά", true);
		}
		if (num7 > 0) {
			return DetectLatin (text);
		}
		return Result ("auto", "其他语言", false);
	}

	private static DetectedLanguage DetectLatin (string text)
	{
		string input = text.ToLowerInvariant ();
		HashSet<string> words = new HashSet<string> (from Match m in Regex.Matches (input, "[\\p{L}]+", RegexOptions.CultureInvariant)
			select m.Value);
		Dictionary<string, int> dictionary = LatinHints.ToDictionary ((KeyValuePair<string, string[]> pair) => pair.Key, (KeyValuePair<string, string[]> pair) => pair.Value.Count (words.Contains));
		if (Regex.IsMatch (input, "[äöüß]")) {
			dictionary ["DE"] += 3;
		}
		if (Regex.IsMatch (input, "[àâçéèêëîïôûùüÿœ]")) {
			dictionary ["FR"] += 2;
		}
		if (Regex.IsMatch (input, "[ñ¡¿]")) {
			dictionary ["ES"] += 3;
		}
		if (Regex.IsMatch (input, "[ãõ]")) {
			dictionary ["PT-BR"] += 3;
		}
		string key = (from pair in dictionary
			orderby pair.Value descending, (!(pair.Key == "EN")) ? 1 : 0
			select pair).First ().Key;
		object obj;
		switch (key) {
		default:
			obj = "Italiano";
			break;
		case "PT-BR":
			obj = "Português";
			break;
		case "ES":
			obj = "Español";
			break;
		case "FR":
			obj = "Français";
			break;
		case "DE":
			obj = "Deutsch";
			break;
		case "EN":
			obj = "English";
			break;
		}
		string name = (string)obj;
		return Result (key, name, true);
	}

	public static bool CultureMatchesText (CultureInfo culture, string text)
	{
		if (culture == null) {
			return false;
		}
		string code = Detect (text).Code;
		string text2 = culture.TwoLetterISOLanguageName.ToUpperInvariant ();
		if (!(code == "ZH-HANS")) {
			if (!(code == "EN")) {
				return code.StartsWith (text2, StringComparison.OrdinalIgnoreCase);
			}
			return text2 == "EN";
		}
		return text2 == "ZH";
	}

	public static string ResolveForDeepL (string selectedSource, string text)
	{
		if (!string.Equals (selectedSource, "auto", StringComparison.OrdinalIgnoreCase)) {
			return selectedSource;
		}
		DetectedLanguage detectedLanguage = Detect (text);
		if (!detectedLanguage.CanUseAsDeepLSource) {
			return "auto";
		}
		return detectedLanguage.Code;
	}

	private static DetectedLanguage Result (string code, string name, bool supported)
	{
		DetectedLanguage detectedLanguage = new DetectedLanguage ();
		detectedLanguage.Code = code;
		detectedLanguage.Name = name;
		detectedLanguage.CanUseAsDeepLSource = supported;
		return detectedLanguage;
	}
}

}
