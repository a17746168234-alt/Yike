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
public static class ImageFiles
{
	public static BitmapSource Load (string path)
	{
		using (FileStream streamSource = new FileStream (System.IO.Path.GetFullPath (path), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
			BitmapImage bitmapImage = new BitmapImage ();
			bitmapImage.BeginInit ();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.StreamSource = streamSource;
			bitmapImage.EndInit ();
			bitmapImage.Freeze ();
			return bitmapImage;
		}
	}

	public static void Save (BitmapSource image, string path)
	{
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder ();
		pngBitmapEncoder.Frames.Add (BitmapFrame.Create (image));
		using (FileStream stream = File.Create (path)) {
			pngBitmapEncoder.Save (stream);
		}
	}
}

}
