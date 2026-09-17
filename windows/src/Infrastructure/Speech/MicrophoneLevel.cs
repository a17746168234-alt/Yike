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
internal sealed class MicrophoneLevel : IDisposable
{
	[ComImport]
	[Guid ("BCDE0395-E52F-467C-8E3D-C4579291692E")]
	private class Enumerator
	{
	}

	[ComImport]
	[Guid ("A95664D2-9614-4F35-A746-DE8DB63617E6")]
	[InterfaceType (ComInterfaceType.InterfaceIsIUnknown)]
	private interface IDevices
	{
		[PreserveSig]
		int EnumEndpoints (int flow, uint mask, out IntPtr devices);

		[PreserveSig]
		int GetDefaultEndpoint (int flow, int role, out IDevice device);
	}

	[ComImport]
	[InterfaceType (ComInterfaceType.InterfaceIsIUnknown)]
	[Guid ("D666063F-1587-4E43-81F1-B948E807363F")]
	private interface IDevice
	{
		[PreserveSig]
		int Activate (ref Guid id, uint context, IntPtr parameters, [MarshalAs (UnmanagedType.IUnknown)] out object instance);
	}

	[ComImport]
	[InterfaceType (ComInterfaceType.InterfaceIsIUnknown)]
	[Guid ("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
	private interface IMeter
	{
		[PreserveSig]
		int GetPeakValue (out float peak);
	}

	private IMeter meter;

	public MicrophoneLevel ()
	{
		IDevices devices = (IDevices)new Enumerator ();
		IDevice device = null;
		try {
			Marshal.ThrowExceptionForHR (devices.GetDefaultEndpoint (1, 0, out device));
			Guid id = typeof(IMeter).GUID;
			object instance;
			Marshal.ThrowExceptionForHR (device.Activate (ref id, 23u, IntPtr.Zero, out instance));
			meter = (IMeter)instance;
		} finally {
			if (device != null) {
				Marshal.ReleaseComObject (device);
			}
			Marshal.ReleaseComObject (devices);
		}
	}

	public int Read ()
	{
		float peak;
		Marshal.ThrowExceptionForHR (meter.GetPeakValue (out peak));
		return Math.Max (0, Math.Min (100, (int)Math.Round (peak * 100f)));
	}

	public void Dispose ()
	{
		if (meter != null) {
			Marshal.ReleaseComObject (meter);
			meter = null;
		}
	}
}

}
