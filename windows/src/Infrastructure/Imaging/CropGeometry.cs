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
internal static class CropGeometry
{
	internal static Int32Rect CropBounds (Rect selection, int width, int height)
	{
		double num = Math.Max (0.0, Math.Min (width, selection.Left));
		double num2 = Math.Max (0.0, Math.Min (height, selection.Top));
		double a = Math.Max (num, Math.Min (width, selection.Right));
		double a2 = Math.Max (num2, Math.Min (height, selection.Bottom));
		return new Int32Rect ((int)Math.Floor (num), (int)Math.Floor (num2), (int)Math.Ceiling (a) - (int)Math.Floor (num), (int)Math.Ceiling (a2) - (int)Math.Floor (num2));
	}
}

}
