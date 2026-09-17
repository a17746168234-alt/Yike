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
internal static class SelectionReader
{
	private struct Input
	{
		public uint Type;

		public InputData Data;
	}

	[StructLayout (LayoutKind.Explicit)]
	private struct InputData
	{
		[FieldOffset (0)]
		public KeyboardInput Keyboard;

		[FieldOffset (0)]
		public MouseInput Mouse;
	}

	private struct KeyboardInput
	{
		public ushort Key;

		public ushort Scan;

		public uint Flags;

		public uint Time;

		public UIntPtr Extra;
	}

	private struct MouseInput
	{
		public int X;

		public int Y;

		public uint Data;

		public uint Flags;

		public uint Time;

		public UIntPtr Extra;
	}

	[DllImport ("user32.dll")]
	private static extern short GetAsyncKeyState (int key);

	[DllImport ("user32.dll")]
	private static extern IntPtr GetForegroundWindow ();

	[DllImport ("user32.dll", SetLastError = true)]
	private static extern uint SendInput (uint count, Input[] inputs, int size);

	private static bool KeysDown ()
	{
		int[] array = new int[6] { 16, 17, 18, 91, 92, 70 };
		foreach (int key in array) {
			if ((GetAsyncKeyState (key) & 0x8000) != 0) {
				return true;
			}
		}
		return false;
	}

	private static Input Key (ushort key, bool up)
	{
		return new Input {
			Type = 1u,
			Data = new InputData {
				Keyboard = new KeyboardInput {
					Key = key,
					Flags = (up ? 2u : 0u)
				}
			}
		};
	}

	internal static async Task WaitForRelease (Func<bool> down, Func<bool> sameWindow, Func<int, Task> delay, int attempts)
	{
		for (int i = 0; i < attempts; i++) {
			if (!sameWindow ()) {
				throw new InvalidOperationException ("取词时窗口已切换，请在目标窗口重新选中文字后再试。");
			}
			if (!down ()) {
				return;
			}
			await delay (25);
		}
		throw new InvalidOperationException ("请松开 Ctrl、Shift 和 F 键，再重试。");
	}

	internal static async Task<string> ReadCopiedText (Func<uint> sequence, uint before, Func<string> read, Func<int, Task> delay, int attempts)
	{
		for (int i = 0; i < attempts; i++) {
			if (sequence () != before) {
				try {
					string text = read ();
					if (!string.IsNullOrWhiteSpace (text)) {
						return text;
					}
				} catch (ExternalException) {
				}
			}
			await delay (25);
		}
		throw new InvalidOperationException ("未取得选中文字。请先选中可复制的文字再按 Ctrl+Shift+F；若目标程序以管理员身份运行，请让两个程序使用相同权限。");
	}

	internal static async Task<string> Capture ()
	{
		IntPtr foreground = GetForegroundWindow ();
		await WaitForRelease (KeysDown, () => GetForegroundWindow () == foreground, Task.Delay, 200);
		System.Windows.DataObject saved = null;
		for (int i = 0; i < 8; i++) {
			try {
				System.Windows.IDataObject dataObject = System.Windows.Clipboard.GetDataObject ();
				saved = new System.Windows.DataObject ();
				if (dataObject != null) {
					string[] formats = dataObject.GetFormats (false);
					foreach (string format in formats) {
						saved.SetData (format, dataObject.GetData (format, false));
					}
				}
			} catch (Exception) {
				saved = null;
				goto IL_019d;
			}
			break;
			IL_019d:
			await Task.Delay (25);
		}
		await WaitForRelease (KeysDown, () => GetForegroundWindow () == foreground, Task.Delay, 200);
		uint before = Native.GetClipboardSequenceNumber ();
		Input[] keys = new Input[4] {
			Key (17, false),
			Key (67, false),
			Key (67, true),
			Key (17, true)
		};
		if (SendInput ((uint)keys.Length, keys, Marshal.SizeOf (typeof(Input))) != keys.Length) {
			SendInput (2u, new Input[2] {
				Key (67, true),
				Key (17, true)
			}, Marshal.SizeOf (typeof(Input)));
			throw new InvalidOperationException ("无法向目标程序发送复制快捷键，请检查目标程序与翻译程序是否使用相同权限。");
		}
		string text = await ReadCopiedText (Native.GetClipboardSequenceNumber, before, () => (!System.Windows.Clipboard.ContainsText ()) ? null : System.Windows.Clipboard.GetText (), Task.Delay, 80);
		uint copied = Native.GetClipboardSequenceNumber ();
		if (saved != null) {
			int i2 = 0;
			while (i2 < 8 && Native.GetClipboardSequenceNumber () == copied) {
				try {
					System.Windows.Clipboard.SetDataObject (saved, true);
				} catch (ExternalException) {
					goto IL_0498;
				}
				break;
				IL_0498:
				await Task.Delay (25);
				i2++;
			}
		}
		return text;
	}
}

}
