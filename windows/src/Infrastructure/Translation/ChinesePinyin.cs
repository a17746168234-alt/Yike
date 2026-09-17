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
public static class ChinesePinyin
{
	private static readonly Regex cjk = new Regex ("[\\u3400-\\u4dbf\\u4e00-\\u9fff]");

	private static readonly Regex bracketed = new Regex ("\\s*[\\(\\[\\uFF08\\u3010]\\s*(?<text>[A-Za-z0-5üÜāáǎàēéěèīíǐìōóǒòūúǔùǖǘǚǜĀÁǍÀĒÉĚÈĪÍǏÌŌÓǑÒŪÚǓÙǕǗǙǛ]+(?:[\\s'’·-]+[A-Za-z0-5üÜāáǎàēéěèīíǐìōóǒòūúǔùǖǘǚǜĀÁǍÀĒÉĚÈĪÍǏÌŌÓǑÒŪÚǓÙǕǗǙǛ]+)*)\\s*[\\)\\]\\uFF09\\u3011]");

	private static readonly Regex pinyinLine = new Regex ("(?m)^[ \\t]*(?<text>[A-Za-z0-5üÜāáǎàēéěèīíǐìōóǒòūúǔùǖǘǚǜĀÁǍÀĒÉĚÈĪÍǏÌŌÓǑÒŪÚǓÙǕǗǙǛ]+(?:[ \\t'’·-]+[A-Za-z0-5üÜāáǎàēéěèīíǐìōóǒòūúǔùǖǘǚǜĀÁǍÀĒÉĚÈĪÍǏÌŌÓǑÒŪÚǓÙǕǗǙǛ]+)+)[ \\t]*\\r?$");

	private static readonly Regex tone = new Regex ("[1-5üÜāáǎàēéěèīíǐìōóǒòūúǔùǖǘǚǜĀÁǍÀĒÉĚÈĪÍǏÌŌÓǑÒŪÚǓÙǕǗǙǛ]");

	public static string Remove (string value, string target)
	{
		if (string.IsNullOrEmpty (value) || string.IsNullOrEmpty (target) || !target.StartsWith ("ZH", StringComparison.OrdinalIgnoreCase) || !cjk.IsMatch (value)) {
			return value;
		}
		value = bracketed.Replace (value, (Match m) => (!IsPinyin (m.Groups ["text"].Value)) ? m.Value : "");
		value = pinyinLine.Replace (value, (Match m) => (!IsPinyin (m.Groups ["text"].Value)) ? m.Value : "");
		return Regex.Replace (value, "[ \\t]{2,}", " ").Trim ();
	}

	private static bool IsPinyin (string value)
	{
		if (tone.IsMatch (value)) {
			return !Regex.IsMatch (value, "[^A-Za-z0-5üÜāáǎàēéěèīíǐìōóǒòūúǔùǖǘǚǜĀÁǍÀĒÉĚÈĪÍǏÌŌÓǑÒŪÚǓÙǕǗǙǛ\\s'’·-]");
		}
		return false;
	}
}

}
