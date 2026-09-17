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
internal sealed class ProcessJob : IDisposable
{
	private struct BasicLimitInformation
	{
		public long PerProcessUserTimeLimit;

		public long PerJobUserTimeLimit;

		public uint LimitFlags;

		public UIntPtr MinimumWorkingSetSize;

		public UIntPtr MaximumWorkingSetSize;

		public uint ActiveProcessLimit;

		public UIntPtr Affinity;

		public uint PriorityClass;

		public uint SchedulingClass;
	}

	private struct IoCounters
	{
		public ulong ReadOperationCount;

		public ulong WriteOperationCount;

		public ulong OtherOperationCount;

		public ulong ReadTransferCount;

		public ulong WriteTransferCount;

		public ulong OtherTransferCount;
	}

	private struct ExtendedLimitInformation
	{
		public BasicLimitInformation BasicLimitInformation;

		public IoCounters IoInfo;

		public UIntPtr ProcessMemoryLimit;

		public UIntPtr JobMemoryLimit;

		public UIntPtr PeakProcessMemoryUsed;

		public UIntPtr PeakJobMemoryUsed;
	}

	private const uint KillOnJobClose = 8192u;

	private IntPtr handle;

	private ProcessJob (IntPtr handle)
	{
		this.handle = handle;
	}

	public static ProcessJob Attach (Process process)
	{
		if (process == null) {
			throw new ArgumentNullException ("process");
		}
		IntPtr intPtr = CreateJobObject (IntPtr.Zero, null);
		if (intPtr == IntPtr.Zero) {
			throw new Win32Exception (Marshal.GetLastWin32Error ());
		}
		try {
			ExtendedLimitInformation info = new ExtendedLimitInformation {
				BasicLimitInformation = {
					LimitFlags = 8192u
				}
			};
			if (!SetInformationJobObject (intPtr, 9, ref info, (uint)Marshal.SizeOf (typeof(ExtendedLimitInformation)))) {
				throw new Win32Exception (Marshal.GetLastWin32Error ());
			}
			if (!AssignProcessToJobObject (intPtr, process.Handle)) {
				throw new Win32Exception (Marshal.GetLastWin32Error ());
			}
			return new ProcessJob (intPtr);
		} catch {
			CloseHandle (intPtr);
			throw;
		}
	}

	public static ProcessJob AttachOrTerminate (Process process)
	{
		try {
			return Attach (process);
		} catch {
			try {
				if (process != null && !process.HasExited) {
					process.Kill ();
				}
				if (process != null) {
					process.WaitForExit (2000);
				}
			} catch {
			}
			throw;
		}
	}

	public void Dispose ()
	{
		IntPtr intPtr = Interlocked.Exchange (ref handle, IntPtr.Zero);
		if (intPtr != IntPtr.Zero) {
			CloseHandle (intPtr);
		}
	}

	[DllImport ("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr CreateJobObject (IntPtr attributes, string name);

	[DllImport ("kernel32.dll", SetLastError = true)]
	[return: MarshalAs (UnmanagedType.Bool)]
	private static extern bool SetInformationJobObject (IntPtr job, int infoClass, ref ExtendedLimitInformation info, uint length);

	[DllImport ("kernel32.dll", SetLastError = true)]
	[return: MarshalAs (UnmanagedType.Bool)]
	private static extern bool AssignProcessToJobObject (IntPtr job, IntPtr process);

	[DllImport ("kernel32.dll", SetLastError = true)]
	[return: MarshalAs (UnmanagedType.Bool)]
	private static extern bool CloseHandle (IntPtr handle);
}

}
