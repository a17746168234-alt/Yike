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
internal sealed class AccountResult
{
	public bool Success { get; private set; }

	public string Message { get; private set; }

	public AccountProfile Profile { get; private set; }

	public static AccountResult Ok (string message, AccountProfile profile = null)
	{
		AccountResult accountResult = new AccountResult ();
		accountResult.Success = true;
		accountResult.Message = message;
		accountResult.Profile = profile;
		return accountResult;
	}

	public static AccountResult Fail (string message)
	{
		AccountResult accountResult = new AccountResult ();
		accountResult.Success = false;
		accountResult.Message = message;
		return accountResult;
	}
}

}
