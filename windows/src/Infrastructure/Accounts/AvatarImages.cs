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
internal static class AvatarImages
{
	private const int OutputSize = 256;

	private const long MaximumInputBytes = 12582912L;

	public static byte[] CreatePng (string path)
	{
		if (string.IsNullOrWhiteSpace (path) || !File.Exists (path)) {
			throw new InvalidOperationException ("请选择存在的头像图片");
		}
		FileInfo fileInfo = new FileInfo (path);
		if (fileInfo.Length <= 0 || fileInfo.Length > 12582912) {
			throw new InvalidOperationException ("头像原图不能超过 12 MB");
		}
		BitmapFrame bitmapFrame;
		using (FileStream bitmapStream = File.Open (path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
			BitmapDecoder bitmapDecoder = BitmapDecoder.Create (bitmapStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
			if (bitmapDecoder.Frames.Count == 0) {
				throw new InvalidOperationException ("无法读取这张图片");
			}
			bitmapFrame = bitmapDecoder.Frames [0];
		}
		int num = Math.Min (bitmapFrame.PixelWidth, bitmapFrame.PixelHeight);
		if (num < 16) {
			throw new InvalidOperationException ("头像图片尺寸至少需要 16 × 16");
		}
		int x = (bitmapFrame.PixelWidth - num) / 2;
		int y = (bitmapFrame.PixelHeight - num) / 2;
		BitmapSource source = new CroppedBitmap (bitmapFrame, new Int32Rect (x, y, num, num));
		if (num != 256) {
			source = new TransformedBitmap (source, new ScaleTransform (256.0 / (double)num, 256.0 / (double)num));
		}
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder ();
		pngBitmapEncoder.Frames.Add (BitmapFrame.Create (source));
		using (MemoryStream memoryStream = new MemoryStream ()) {
			pngBitmapEncoder.Save (memoryStream);
			return memoryStream.ToArray ();
		}
	}

	public static BitmapSource Load (AccountProfile profile)
	{
		if (profile != null) {
			return Load (profile.AvatarPngBase64);
		}
		return null;
	}

	public static BitmapSource Load (string base64)
	{
		if (string.IsNullOrWhiteSpace (base64)) {
			return null;
		}
		try {
			byte[] buffer = Convert.FromBase64String (base64);
			using (MemoryStream streamSource = new MemoryStream (buffer, false)) {
				BitmapImage bitmapImage = new BitmapImage ();
				bitmapImage.BeginInit ();
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.StreamSource = streamSource;
				bitmapImage.EndInit ();
				bitmapImage.Freeze ();
				return bitmapImage;
			}
		} catch {
			return null;
		}
	}
}

}
