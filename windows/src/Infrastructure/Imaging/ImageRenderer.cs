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
public static class ImageRenderer
{
	public static BitmapSource Render (BitmapSource original, OcrDocument doc)
	{
		int pixelWidth = original.PixelWidth;
		int pixelHeight = original.PixelHeight;
		DrawingVisual drawingVisual = new DrawingVisual ();
		FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap (original, PixelFormats.Bgra32, null, 0.0);
		byte[] array = new byte[pixelWidth * pixelHeight * 4];
		formatConvertedBitmap.CopyPixels (array, pixelWidth * 4, 0);
		using (DrawingContext drawingContext = drawingVisual.RenderOpen ()) {
			drawingContext.DrawImage (original, new Rect (0.0, 0.0, pixelWidth, pixelHeight));
			foreach (Region region in doc.Regions) {
				if (string.IsNullOrEmpty (region.Translated)) {
					continue;
				}
				double num = Math.Max (0.0, Math.Min (pixelWidth - 1, region.X));
				double num2 = Math.Max (0.0, Math.Min (pixelHeight - 1, region.Y));
				double num3 = Math.Max (1.0, Math.Min ((double)pixelWidth - num, region.Width));
				double num4 = Math.Max (1.0, Math.Min ((double)pixelHeight - num2, region.Height));
				System.Windows.Media.Color color = Colors.White;
				if (!region.HighContrast) {
					long num5 = 0L;
					long num6 = 0L;
					long num7 = 0L;
					long num8 = 0L;
					int num9 = Math.Max (0, (int)num2 - 3);
					int num10 = Math.Min (pixelHeight - 1, (int)(num2 + num4) + 3);
					for (int i = (int)num; (double)i < Math.Min (pixelWidth, num + num3); i++) {
						int[] array2 = new int[2] { num9, num10 };
						foreach (int num11 in array2) {
							int num12 = (num11 * pixelWidth + i) * 4;
							num7 += array [num12];
							num6 += array [num12 + 1];
							num5 += array [num12 + 2];
							num8++;
						}
					}
					if (num8 > 0) {
						color = System.Windows.Media.Color.FromRgb ((byte)(num5 / num8), (byte)(num6 / num8), (byte)(num7 / num8));
					}
				}
				drawingContext.DrawRectangle (new SolidColorBrush (color), null, new Rect (num, num2, num3, num4));
				System.Windows.Media.Brush foreground = System.Windows.Media.Brushes.Black;
				try {
					foreground = new SolidColorBrush ((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString (region.Color));
				} catch {
				}
				double num13 = Math.Max (4.0, Math.Min ((region.FontSize > 0.0) ? region.FontSize : (num4 * 0.8), 1000.0));
				FormattedText formattedText;
				while (true) {
					formattedText = new FormattedText (region.Translated, CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, new Typeface ("Microsoft YaHei UI"), num13, foreground, 1.0);
					formattedText.MaxTextWidth = num3;
					formattedText.Trimming = TextTrimming.None;
					if (formattedText.Height <= num4 || num13 <= 4.0) {
						break;
					}
					num13 -= 0.5;
				}
				drawingContext.PushClip (new RectangleGeometry (new Rect (num, num2, num3, num4)));
				drawingContext.DrawText (formattedText, new System.Windows.Point (num, num2));
				drawingContext.Pop ();
			}
		}
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap (pixelWidth, pixelHeight, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render (drawingVisual);
		renderTargetBitmap.Freeze ();
		return renderTargetBitmap;
	}
}

}
