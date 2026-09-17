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
internal sealed class UpdateCheckResult
{
	public bool Success { get; private set; }

	public bool UpdateAvailable { get; private set; }

	public Version CurrentVersion { get; private set; }

	public Version LatestVersion { get; private set; }

	public string DownloadUrl { get; private set; }

	public string Notes { get; private set; }

	public string Message { get; private set; }
	public string ReleaseUrl { get; private set; }
	public string Sha256 { get; private set; }
	public long Size { get; private set; }
	public bool CanInstall { get { return Success && UpdateAvailable && Sha256 != null && Size > 0; } }

	public static UpdateCheckResult Found (Version current, Version latest, string url, string notes, string releaseUrl = null, string sha256 = null, long size = 0)
	{
		UpdateCheckResult updateCheckResult = new UpdateCheckResult ();
		updateCheckResult.Success = true;
		updateCheckResult.UpdateAvailable = latest > current;
		updateCheckResult.CurrentVersion = current;
		updateCheckResult.LatestVersion = latest;
		updateCheckResult.DownloadUrl = url;
		updateCheckResult.Notes = notes;
		updateCheckResult.ReleaseUrl = releaseUrl ?? url;
		updateCheckResult.Sha256 = sha256;
		updateCheckResult.Size = size;
		return updateCheckResult;
	}

	public static UpdateCheckResult Failed (Version current, string message)
	{
		UpdateCheckResult updateCheckResult = new UpdateCheckResult ();
		updateCheckResult.CurrentVersion = current;
		updateCheckResult.Message = message;
		return updateCheckResult;
	}
}

}
