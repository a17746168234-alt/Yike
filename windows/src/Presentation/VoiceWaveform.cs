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
public sealed class VoiceWaveform : IDisposable
{
	private readonly List<Border> bars = new List<Border> ();

	private readonly TextBlock label;

	private readonly DispatcherTimer animationTimer;

	private double targetLevel;

	private double displayedLevel;

	private double phase;

	private bool disposed;

	public StackPanel Content { get; private set; }

	internal int BarCount { get { return bars.Count; } }

	internal bool IsAnimating { get { return animationTimer.IsEnabled; } }

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
			Width = 43.0,
			Height = 18.0,
			Margin = new Thickness (7.0, 0.0, 3.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		for (int i = 0; i < 9; i++) {
			Border border = new Border {
				Width = 2.6,
				Height = 3.0,
				CornerRadius = new CornerRadius (1.3),
				HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness ((double)i * 5.0, 0.0, 0.0, 0.0)
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
		animationTimer = new DispatcherTimer (DispatcherPriority.Render) {
			Interval = TimeSpan.FromMilliseconds (55.0)
		};
		animationTimer.Tick += Animate;
		animationTimer.Start ();
		Update (0);
	}

	public void Update (int level)
	{
		int num = Math.Max (0, Math.Min (100, level));
		targetLevel = Math.Sqrt ((double)num / 100.0);
		label.Text = ((num > 1) ? "语音输入中" : "正在聆听");
		if (targetLevel > displayedLevel) displayedLevel = targetLevel * 0.72;
		Animate (this, EventArgs.Empty);
	}

	private void Animate (object sender, EventArgs e)
	{
		if (disposed) return;
		displayedLevel += (targetLevel - displayedLevel) * (targetLevel > displayedLevel ? 0.48 : 0.20);
		phase += 0.48;
		for (int i = 0; i < bars.Count; i++) {
			double wave = 0.28 + 0.72 * Math.Abs (Math.Sin (phase - i * 0.54));
			double idle = 1.0 + 1.5 * Math.Abs (Math.Sin (phase * 0.55 - i * 0.41));
			bars [i].Height = Math.Min (18.0, idle + 15.5 * displayedLevel * wave);
		}
		targetLevel *= 0.94;
	}

	public void Dispose ()
	{
		if (disposed) return;
		disposed = true;
		animationTimer.Stop ();
		animationTimer.Tick -= Animate;
	}
}

}
