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
internal static class ViewZoom
{
	internal static System.Windows.Controls.Button Button (string caption, Action action)
	{
		System.Windows.Controls.Button button = new System.Windows.Controls.Button ();
		button.Content = caption;
		button.Padding = new Thickness (8.0, 5.0, 8.0, 5.0);
		button.MinWidth = 30.0;
		button.ToolTip = caption;
		System.Windows.Controls.Button button2 = button;
		button2.Click += delegate {
			action ();
		};
		return button2;
	}

	internal static FrameworkElement Text (params System.Windows.Controls.TextBox[] boxes)
	{
		WrapPanel wrapPanel = new WrapPanel ();
		wrapPanel.VerticalAlignment = VerticalAlignment.Center;
		WrapPanel wrapPanel2 = wrapPanel;
		double[] sizes = Array.ConvertAll (boxes, (System.Windows.Controls.TextBox box) => box.FontSize);
		double scale = 1.0;
		System.Windows.Controls.Button reset = new System.Windows.Controls.Button {
			Content = "100%",
			Padding = new Thickness (6.0, 5.0, 6.0, 5.0),
			ToolTip = "恢复字号 · Ctrl+0"
		};
		Action<double> change = delegate(double value) {
			scale = Math.Max (0.75, Math.Min (3.0, value));
			for (int i = 0; i < boxes.Length; i++) {
				boxes [i].FontSize = sizes [i] * scale;
			}
			reset.Content = Math.Round (scale * 100.0) + "%";
		};
		wrapPanel2.Children.Add (Button ("A−", delegate {
			change (scale - 0.25);
		}));
		wrapPanel2.Children.Add (reset);
		wrapPanel2.Children.Add (Button ("A+", delegate {
			change (scale + 0.25);
		}));
		reset.Click += delegate {
			change (1.0);
		};
		System.Windows.Controls.TextBox[] array = boxes;
		foreach (System.Windows.Controls.TextBox textBox in array) {
			textBox.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e) {
				if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.None) {
					change (scale + ((e.Delta > 0) ? 0.25 : (-0.25)));
					e.Handled = true;
				}
			};
			textBox.PreviewKeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e) {
				if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.None) {
					if (e.Key == Key.Add || e.Key == Key.OemPlus) {
						change (scale + 0.25);
					} else if (e.Key == Key.Subtract || e.Key == Key.OemMinus) {
						change (scale - 0.25);
					} else {
						if (e.Key != Key.D0 && e.Key != Key.NumPad0) {
							return;
						}
						change (1.0);
					}
					e.Handled = true;
				}
			};
		}
		return wrapPanel2;
	}

	internal static FrameworkElement Image (Canvas canvas)
	{
		DockPanel dockPanel = new DockPanel ();
		WrapPanel wrapPanel = new WrapPanel ();
		wrapPanel.Margin = new Thickness (0.0, 0.0, 0.0, 8.0);
		WrapPanel wrapPanel2 = wrapPanel;
		DockPanel.SetDock (wrapPanel2, Dock.Top);
		dockPanel.Children.Add (wrapPanel2);
		ScaleTransform transform = new ScaleTransform (1.0, 1.0);
		canvas.LayoutTransform = transform;
		ScrollViewer scroll = new ScrollViewer {
			Content = canvas,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		dockPanel.Children.Add (scroll);
		bool fitting = true;
		TextBlock label = new TextBlock {
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness (8.0, 0.0, 8.0, 0.0)
		};
		Action<double> scale = delegate(double value) {
			ScaleTransform scaleTransform = transform;
			double scaleX = (transform.ScaleY = Math.Max (0.01, Math.Min (8.0, value)));
			scaleTransform.ScaleX = scaleX;
			label.Text = Math.Round (transform.ScaleX * 100.0) + "%";
		};
		Action fit = delegate {
			scale (Math.Min (Math.Max (1.0, scroll.ActualWidth - 20.0) / canvas.Width, Math.Max (1.0, scroll.ActualHeight - 20.0) / canvas.Height));
		};
		Action<double> zoom = delegate(double value) {
			fitting = false;
			scale (value);
		};
		wrapPanel2.Children.Add (Button ("−", delegate {
			zoom (transform.ScaleX / 1.25);
		}));
		wrapPanel2.Children.Add (label);
		wrapPanel2.Children.Add (Button ("+", delegate {
			zoom (transform.ScaleX * 1.25);
		}));
		wrapPanel2.Children.Add (Button ("适合窗口", delegate {
			fitting = true;
			fit ();
		}));
		wrapPanel2.Children.Add (Button ("原尺寸", delegate {
			zoom (1.0);
		}));
		scroll.SizeChanged += delegate {
			if (fitting) {
				fit ();
			}
		};
		scroll.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e) {
			if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.None) {
				zoom (transform.ScaleX * ((e.Delta > 0) ? 1.25 : 0.8));
				e.Handled = true;
			}
		};
		dockPanel.ToolTip = "Ctrl+滚轮缩放图片；滚动条移动图片；在图片上拖动框选";
		return dockPanel;
	}
}

}
