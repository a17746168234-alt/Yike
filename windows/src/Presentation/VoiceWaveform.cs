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
public sealed class VoiceWaveform
{
	private readonly List<Border> bars = new List<Border> ();

	private readonly TextBlock label;

	public StackPanel Content { get; private set; }

	public VoiceWaveform ()
	{
		Content = new StackPanel {
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		Content.Children.Add (new TextBlock {
			Text = "\ue720",
			FontFamily = new System.Windows.Media.FontFamily ("Segoe Fluent Icons"),
			FontSize = 15.0,
			VerticalAlignment = VerticalAlignment.Center
		});
		Grid grid = new Grid {
			Width = 29.0,
			Height = 18.0,
			Margin = new Thickness (7.0, 0.0, 3.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		for (int i = 0; i < 5; i++) {
			Border border = new Border {
				Width = 3.0,
				Height = 3.0,
				CornerRadius = new CornerRadius (1.5),
				HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness ((double)i * 5.5, 0.0, 0.0, 0.0)
			};
			border.SetResourceReference (Border.BackgroundProperty, "AccentBrush");
			bars.Add (border);
			grid.Children.Add (border);
		}
		Content.Children.Add (grid);
		label = new TextBlock {
			Text = "正在聆听",
			FontFamily = new System.Windows.Media.FontFamily ("Microsoft YaHei UI"),
			FontSize = 13.0,
			Margin = new Thickness (4.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		Content.Children.Add (label);
		Update (0);
	}

	public void Update (int level)
	{
		int num = Math.Max (0, Math.Min (100, level));
		double num2 = Math.Sqrt ((double)num / 100.0);
		double[] array = new double[5] { 0.48, 0.82, 1.0, 0.7, 0.42 };
		for (int i = 0; i < bars.Count; i++) {
			bars [i].Height = 3.0 + 15.0 * num2 * array [i];
		}
		label.Text = ((num > 0) ? "语音输入中" : "正在聆听");
	}
}

}
