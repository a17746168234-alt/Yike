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
public partial class App {
private void History ()
{
	HistoryCenter (false, "text");
}


private void TextHistoryDialog ()
{
	HistoryCenter (false, "text");
}


private void ImageHistory ()
{
	HistoryCenter (false, "image");
}


private void HistoryPreview ()
{
	List<Entry> list = history;
	history = new List<Entry> {
		new Entry {
			Original = "你好，世界",
			Result = "Hello, world.",
			Source = "ZH-HANS",
			Target = "EN-US",
			Engine = "DeepL",
			Date = DateTime.Now.AddDays (-1.0),
			Pinned = true
		},
		new Entry {
			Original = "Something went wrong. Please try again later.",
			Result = "出现了一些问题，请稍后重试。",
			Source = "EN-US",
			Target = "ZH-HANS",
			Engine = "DeepL",
			Date = DateTime.Now.AddDays (-2.0)
		},
		new Entry {
			Original = "简洁的界面让翻译更专注。",
			Result = "A clean interface keeps translation focused.",
			Source = "ZH-HANS",
			Target = "EN-US",
			Engine = "DeepL",
			Date = DateTime.Now.AddDays (-2.0)
		}
	};
	try {
		HistoryCenter (true, "text");
	} finally {
		history = list;
	}
}


private void HistoryCenter (bool preview, string initialTab)
{
	Window dialog = null;
	Action refresh = null;
	Grid grid = new Grid ();
	grid.Background = OverlayBrush ("#F1171F22");
	Grid grid2 = grid;
	grid2.RowDefinitions.Add (new RowDefinition {
		Height = new GridLength (88.0)
	});
	grid2.RowDefinitions.Add (new RowDefinition {
		Height = new GridLength (145.0)
	});
	grid2.RowDefinitions.Add (new RowDefinition {
		Height = new GridLength (1.0)
	});
	grid2.RowDefinitions.Add (new RowDefinition ());
	Grid grid3 = new Grid ();
	grid3.Margin = new Thickness (30.0, 18.0, 28.0, 14.0);
	Grid grid4 = grid3;
	StackPanel stackPanel = IconText ("\ue81c", "历史记录", 25.0);
	foreach (TextBlock item in stackPanel.Children.OfType<TextBlock> ()) {
		item.Foreground = OverlayBrush ("#F2F6F7");
	}
	stackPanel.Children.OfType<TextBlock> ().Last ().FontSize = 26.0;
	stackPanel.Children.OfType<TextBlock> ().Last ().FontWeight = FontWeights.SemiBold;
	grid4.Children.Add (stackPanel);
	StackPanel stackPanel2 = new StackPanel ();
	stackPanel2.Orientation = System.Windows.Controls.Orientation.Horizontal;
	stackPanel2.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
	stackPanel2.VerticalAlignment = VerticalAlignment.Center;
	StackPanel stackPanel3 = stackPanel2;
	System.Windows.Controls.Button button = OverlayButton ("清空全部", delegate {
		string text = ((initialTab == "text") ? "全部文字历史" : "全部图片历史");
		if (System.Windows.MessageBox.Show (dialog, "确定清空" + text + "吗？此操作无法撤销。", "Yike", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes) {
			if (initialTab == "text") {
				history.Clear ();
				Store.Write ("history.json", history);
			} else {
				ClearAllImageHistory ();
			}
			refresh ();
		}
	});
	button.Foreground = OverlayBrush ("#FF5D62");
	button.Margin = new Thickness (0.0, 0.0, 8.0, 0.0);
	stackPanel3.Children.Add (button);
	System.Windows.Controls.Button button2 = OverlayButton ("完成", delegate {
		dialog.Close ();
	}, true);
	button2.Margin = new Thickness (0.0);
	stackPanel3.Children.Add (button2);
	grid4.Children.Add (stackPanel3);
	grid2.Children.Add (grid4);
	Grid grid5 = new Grid ();
	grid5.Margin = new Thickness (30.0, 5.0, 30.0, 18.0);
	Grid grid6 = grid5;
	grid6.RowDefinitions.Add (new RowDefinition {
		Height = new GridLength (52.0)
	});
	grid6.RowDefinitions.Add (new RowDefinition ());
	Grid.SetRow (grid6, 1);
	grid2.Children.Add (grid6);
	StackPanel stackPanel4 = new StackPanel ();
	stackPanel4.Orientation = System.Windows.Controls.Orientation.Horizontal;
	stackPanel4.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
	stackPanel4.VerticalAlignment = VerticalAlignment.Top;
	StackPanel stackPanel5 = stackPanel4;
	System.Windows.Controls.Button textTab = OverlayButton ("文字", delegate {
		initialTab = "text";
		refresh ();
	}, true);
	textTab.MinWidth = 82.0;
	textTab.Margin = new Thickness (0.0, 0.0, 3.0, 0.0);
	stackPanel5.Children.Add (textTab);
	System.Windows.Controls.Button imageTab = OverlayButton ("图片", delegate {
		initialTab = "image";
		refresh ();
	});
	imageTab.MinWidth = 82.0;
	imageTab.Margin = new Thickness (0.0);
	stackPanel5.Children.Add (imageTab);
	grid6.Children.Add (stackPanel5);
	Grid grid7 = new Grid ();
	grid7.Margin = new Thickness (0.0, 13.0, 0.0, 0.0);
	Grid grid8 = grid7;
	Grid.SetRow (grid8, 1);
	grid6.Children.Add (grid8);
	Border border = new Border ();
	border.CornerRadius = new CornerRadius (15.0);
	border.Background = OverlayBrush ("#481C2528");
	border.BorderBrush = OverlayBrush ("#344145");
	border.BorderThickness = new Thickness (1.0);
	Border element = border;
	grid8.Children.Add (element);
	System.Windows.Controls.TextBox search = new System.Windows.Controls.TextBox {
		Padding = new Thickness (47.0, 11.0, 16.0, 11.0),
		FontSize = 15.0,
		Foreground = OverlayBrush ("#E7ECEE"),
		Background = System.Windows.Media.Brushes.Transparent,
		BorderThickness = new Thickness (0.0),
		CaretBrush = OverlayBrush ("#2789F7")
	};
	grid8.Children.Add (search);
	TextBlock textBlock = new TextBlock ();
	textBlock.Text = "\ue721";
	textBlock.FontFamily = new System.Windows.Media.FontFamily ("Segoe Fluent Icons");
	textBlock.FontSize = 20.0;
	textBlock.Foreground = OverlayBrush ("#819095");
	textBlock.Margin = new Thickness (16.0, 0.0, 0.0, 0.0);
	textBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
	textBlock.VerticalAlignment = VerticalAlignment.Center;
	textBlock.IsHitTestVisible = false;
	TextBlock element2 = textBlock;
	grid8.Children.Add (element2);
	TextBlock placeholder = new TextBlock {
		Text = "搜索原文或译文…",
		FontSize = 15.0,
		Foreground = OverlayBrush ("#718083"),
		Margin = new Thickness (48.0, 0.0, 0.0, 0.0),
		VerticalAlignment = VerticalAlignment.Center,
		IsHitTestVisible = false
	};
	grid8.Children.Add (placeholder);
	Border border2 = new Border ();
	border2.Background = OverlayBrush ("#344145");
	Border element3 = border2;
	Grid.SetRow (element3, 2);
	grid2.Children.Add (element3);
	StackPanel list = new StackPanel {
		Margin = new Thickness (30.0, 25.0, 30.0, 18.0)
	};
	ScrollViewer scroll = new ScrollViewer {
		Content = list,
		VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
		HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
	};
	scroll.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e) {
		scroll.ScrollToVerticalOffset (Math.Max (0.0, scroll.VerticalOffset - (double)e.Delta));
		e.Handled = true;
	};
	Grid.SetRow (scroll, 3);
	grid2.Children.Add (scroll);
	refresh = delegate {
		placeholder.Visibility = ((!string.IsNullOrEmpty (search.Text)) ? Visibility.Collapsed : Visibility.Visible);
		bool flag = initialTab == "text";
		textTab.Background = OverlayBrush (flag ? "#1677F2" : "#2A3336");
		textTab.Foreground = (flag ? System.Windows.Media.Brushes.White : OverlayBrush ("#CFD7D9"));
		imageTab.Background = OverlayBrush (flag ? "#2A3336" : "#1677F2");
		imageTab.Foreground = (flag ? OverlayBrush ("#CFD7D9") : System.Windows.Media.Brushes.White);
		list.Children.Clear ();
		if (flag) {
			BuildTextHistoryList (list, search.Text, dialog, refresh);
		} else {
			BuildImageHistoryList (list, search.Text, dialog, refresh);
		}
	};
	search.TextChanged += delegate {
		refresh ();
	};
	dialog = OverlayDialog ("历史记录", 1040.0, 790.0, grid2, 32.0);
	dialog.MinWidth = 820.0;
	dialog.MinHeight = 620.0;
	SetOverlayResources (dialog);
	grid4.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) {
		if (!DialogChrome.IsInteractiveSource (e.OriginalSource as DependencyObject, grid4) && e.LeftButton == MouseButtonState.Pressed) {
			dialog.DragMove ();
		}
	};
	refresh ();
	if (preview) {
		dialog.ContentRendered += delegate {
			dialog.UpdateLayout ();
			FrameworkElement frameworkElement = (FrameworkElement)dialog.Content;
			RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap ((int)frameworkElement.ActualWidth, (int)frameworkElement.ActualHeight, 96.0, 96.0, PixelFormats.Pbgra32);
			renderTargetBitmap.Render (frameworkElement);
			ImageFiles.Save (renderTargetBitmap, System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "history-preview.png"));
			dialog.Close ();
		};
	}
	ShowDimmed (dialog);
}


private void BuildTextHistoryList (StackPanel list, string query, Window dialog, Action refresh)
{
	List<Entry> list2 = (from x in history
		where (x.Original + " " + x.Result).IndexOf (query ?? "", StringComparison.OrdinalIgnoreCase) >= 0
		orderby x.Pinned descending, x.Date descending
		select x).ToList ();
	if (list2.Count == 0) {
		list.Children.Add (HistoryEmpty ("没有找到文字翻译记录"));
		return;
	}
	foreach (Entry entry in list2) {
		Border border = new Border ();
		border.CornerRadius = new CornerRadius (20.0);
		border.BorderBrush = OverlayBrush ("#3B494D");
		border.BorderThickness = new Thickness (1.0);
		border.Background = OverlayBrush ("#521B2427");
		border.Padding = new Thickness (24.0, 20.0, 24.0, 18.0);
		border.Margin = new Thickness (0.0, 0.0, 0.0, 16.0);
		Border border2 = border;
		StackPanel stackPanel = (StackPanel)(border2.Child = new StackPanel ());
		Grid grid = new Grid ();
		StackPanel stackPanel2 = new StackPanel ();
		stackPanel2.Orientation = System.Windows.Controls.Orientation.Horizontal;
		StackPanel stackPanel3 = stackPanel2;
		stackPanel3.Children.Add (new TextBlock {
			Text = HistoryLanguage (entry.Source) + "  →  " + HistoryLanguage (entry.Target),
			Foreground = OverlayBrush ("#2789F7"),
			FontSize = 14.0,
			FontWeight = FontWeights.SemiBold
		});
		Border border3 = new Border ();
		border3.CornerRadius = new CornerRadius (10.0);
		border3.Background = OverlayBrush ("#293235");
		border3.Padding = new Thickness (9.0, 3.0, 9.0, 3.0);
		border3.Margin = new Thickness (12.0, 0.0, 0.0, 0.0);
		border3.Child = new TextBlock {
			Text = (string.IsNullOrWhiteSpace (entry.Engine) ? "DeepL" : entry.Engine),
			Foreground = OverlayBrush ("#A7B1B4"),
			FontSize = 12.0
		};
		Border element = border3;
		stackPanel3.Children.Add (element);
		grid.Children.Add (stackPanel3);
		TextBlock textBlock = new TextBlock ();
		textBlock.Text = HistoryDate (entry.Date) + (entry.Pinned ? "   ·   已置顶" : "");
		textBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		textBlock.Foreground = OverlayBrush ("#879296");
		textBlock.FontSize = 12.0;
		TextBlock element2 = textBlock;
		grid.Children.Add (element2);
		stackPanel.Children.Add (grid);
		stackPanel.Children.Add (new TextBlock {
			Text = entry.Original,
			TextWrapping = TextWrapping.Wrap,
			MaxHeight = 92.0,
			FontSize = 16.0,
			FontWeight = FontWeights.SemiBold,
			Foreground = OverlayBrush ("#F0F4F5"),
			Margin = new Thickness (0.0, 17.0, 0.0, 10.0)
		});
		stackPanel.Children.Add (new TextBlock {
			Text = entry.Result,
			TextWrapping = TextWrapping.Wrap,
			MaxHeight = 110.0,
			FontSize = 15.0,
			Foreground = OverlayBrush ("#AAB4B7")
		});
		Grid grid2 = new Grid ();
		grid2.Margin = new Thickness (0.0, 14.0, 0.0, 0.0);
		Grid grid3 = grid2;
		StackPanel stackPanel4 = new StackPanel ();
		stackPanel4.Orientation = System.Windows.Controls.Orientation.Horizontal;
		StackPanel stackPanel5 = stackPanel4;
		System.Windows.Controls.Button button = OverlayButton ("复制原文", delegate {
			System.Windows.Clipboard.SetText (entry.Original ?? "");
		});
		button.Margin = new Thickness (0.0, 0.0, 6.0, 0.0);
		stackPanel5.Children.Add (button);
		System.Windows.Controls.Button button2 = OverlayButton ("复制译文", delegate {
			System.Windows.Clipboard.SetText (entry.Result ?? "");
		});
		button2.Margin = new Thickness (0.0);
		stackPanel5.Children.Add (button2);
		grid3.Children.Add (stackPanel5);
		StackPanel stackPanel6 = new StackPanel ();
		stackPanel6.Orientation = System.Windows.Controls.Orientation.Horizontal;
		stackPanel6.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		StackPanel stackPanel7 = stackPanel6;
		System.Windows.Controls.Button element3 = OverlayButton ("在主界面打开", delegate {
			OpenTextHistory (entry);
			dialog.Close ();
		});
		stackPanel7.Children.Add (element3);
		System.Windows.Controls.Button element4 = OverlayButton (entry.Pinned ? "取消置顶" : "置顶", delegate {
			entry.Pinned = !entry.Pinned;
			Store.Write ("history.json", history);
			refresh ();
		});
		stackPanel7.Children.Add (element4);
		System.Windows.Controls.Button button3 = OverlayButton ("删除", delegate {
			history.Remove (entry);
			Store.Write ("history.json", history);
			refresh ();
		});
		button3.Foreground = OverlayBrush ("#FF686D");
		stackPanel7.Children.Add (button3);
		grid3.Children.Add (stackPanel7);
		stackPanel.Children.Add (grid3);
		list.Children.Add (border2);
	}
}


private void BuildImageHistoryList (StackPanel list, string query, Window dialog, Action refresh)
{
	List<ImageEntry> list2 = (from x in Store.Read ("images.json", new List<ImageEntry> ()).Take (10)
		where ImageSearchText (x).IndexOf (query ?? "", StringComparison.OrdinalIgnoreCase) >= 0
		select x).ToList ();
	if (list2.Count == 0) {
		list.Children.Add (HistoryEmpty ("没有找到图片翻译记录"));
		return;
	}
	foreach (ImageEntry entry in list2) {
		string path = System.IO.Path.Combine (Store.Root, "images", entry.Id + "-translated.png");
		if (!File.Exists (path)) {
			continue;
		}
		Border border = new Border ();
		border.CornerRadius = new CornerRadius (20.0);
		border.BorderBrush = OverlayBrush ("#3B494D");
		border.BorderThickness = new Thickness (1.0);
		border.Background = OverlayBrush ("#521B2427");
		border.Padding = new Thickness (18.0);
		border.Margin = new Thickness (0.0, 0.0, 0.0, 16.0);
		Border border2 = border;
		Grid grid = new Grid ();
		grid.ColumnDefinitions.Add (new ColumnDefinition {
			Width = new GridLength (220.0)
		});
		grid.ColumnDefinitions.Add (new ColumnDefinition ());
		border2.Child = grid;
		Border border3 = new Border ();
		border3.Height = 145.0;
		border3.CornerRadius = new CornerRadius (13.0);
		border3.Background = OverlayBrush ("#222B2E");
		border3.ClipToBounds = true;
		border3.Child = new System.Windows.Controls.Image {
			Source = ImageFiles.Load (path),
			Stretch = Stretch.Uniform
		};
		Border element = border3;
		grid.Children.Add (element);
		StackPanel stackPanel = new StackPanel ();
		stackPanel.Margin = new Thickness (22.0, 2.0, 0.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		Grid.SetColumn (stackPanel2, 1);
		grid.Children.Add (stackPanel2);
		Grid grid2 = new Grid ();
		grid2.Children.Add (new TextBlock {
			Text = HistoryLanguage (entry.Source) + "  →  " + HistoryLanguage (entry.Target) + "   图片翻译",
			Foreground = OverlayBrush ("#2789F7"),
			FontSize = 14.0,
			FontWeight = FontWeights.SemiBold
		});
		grid2.Children.Add (new TextBlock {
			Text = HistoryDate (entry.Date),
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
			Foreground = OverlayBrush ("#879296"),
			FontSize = 12.0
		});
		stackPanel2.Children.Add (grid2);
		string text = ImageSearchText (entry);
		stackPanel2.Children.Add (new TextBlock {
			Text = (string.IsNullOrWhiteSpace (text) ? "图片翻译记录" : text),
			TextWrapping = TextWrapping.Wrap,
			MaxHeight = 70.0,
			FontSize = 14.0,
			Foreground = OverlayBrush ("#AAB4B7"),
			Margin = new Thickness (0.0, 18.0, 0.0, 12.0)
		});
		StackPanel stackPanel3 = new StackPanel ();
		stackPanel3.Orientation = System.Windows.Controls.Orientation.Horizontal;
		StackPanel stackPanel4 = stackPanel3;
		System.Windows.Controls.Button button = OverlayButton ("在主界面打开", delegate {
			OpenImageHistory (entry);
			dialog.Close ();
		});
		button.Margin = new Thickness (0.0, 0.0, 8.0, 0.0);
		stackPanel4.Children.Add (button);
		System.Windows.Controls.Button button2 = OverlayButton ("复制译文", delegate {
			System.Windows.Clipboard.SetText (string.Join (Environment.NewLine, entry.Document.Regions.Select ((Region r) => r.Translated ?? "")));
		});
		button2.Margin = new Thickness (0.0, 0.0, 8.0, 0.0);
		stackPanel4.Children.Add (button2);
		System.Windows.Controls.Button button3 = OverlayButton ("删除", delegate {
			DeleteImageHistory (entry);
			refresh ();
		});
		button3.Foreground = OverlayBrush ("#FF686D");
		button3.Margin = new Thickness (0.0);
		stackPanel4.Children.Add (button3);
		stackPanel2.Children.Add (stackPanel4);
		list.Children.Add (border2);
	}
}


private FrameworkElement HistoryEmpty (string text)
{
	StackPanel stackPanel = new StackPanel ();
	stackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
	stackPanel.VerticalAlignment = VerticalAlignment.Center;
	stackPanel.Margin = new Thickness (0.0, 90.0, 0.0, 0.0);
	StackPanel stackPanel2 = stackPanel;
	stackPanel2.Children.Add (new TextBlock {
		Text = "\ue81c",
		FontFamily = new System.Windows.Media.FontFamily ("Segoe Fluent Icons"),
		FontSize = 42.0,
		Foreground = OverlayBrush ("#536166"),
		HorizontalAlignment = System.Windows.HorizontalAlignment.Center
	});
	stackPanel2.Children.Add (new TextBlock {
		Text = text,
		FontSize = 15.0,
		Foreground = OverlayBrush ("#829095"),
		Margin = new Thickness (0.0, 14.0, 0.0, 0.0),
		HorizontalAlignment = System.Windows.HorizontalAlignment.Center
	});
	return stackPanel2;
}


private string HistoryLanguage (string code)
{
	int num = Array.IndexOf (codes, code);
	object obj;
	if (num < 0) {
		obj = code;
		if (obj == null) {
			return "";
		}
	} else {
		obj = names [num];
	}
	return (string)obj;
}


private string HistoryDate (DateTime date)
{
	int days = (DateTime.Now.Date - date.Date).Days;
	switch (days) {
	case 0:
		return "今天";
	case 1:
		return "1天前";
	case 2:
	case 3:
	case 4:
	case 5:
	case 6:
	case 7:
	case 8:
	case 9:
	case 10:
	case 11:
	case 12:
	case 13:
	case 14:
	case 15:
	case 16:
	case 17:
	case 18:
	case 19:
	case 20:
	case 21:
	case 22:
	case 23:
	case 24:
	case 25:
	case 26:
	case 27:
	case 28:
	case 29:
		return days + "天前";
	default:
		return date.ToString ("yyyy-MM-dd");
	}
}


private string ImageSearchText (ImageEntry entry)
{
	if (entry.Document != null) {
		return string.Join (" ", entry.Document.Regions.Select ((Region r) => (r.Text ?? "") + " " + (r.Translated ?? "")));
	}
	return "";
}


private void OpenTextHistory (Entry entry)
{
	Clear ();
	Select (source, entry.Source);
	Select (target, entry.Target);
	input.Text = entry.Original;
	output.Text = entry.Result;
	UpdateResult ();
}


private void OpenImageHistory (ImageEntry entry)
{
	Clear ();
	Select (source, entry.Source);
	Select (target, entry.Target);
	document = entry.Document;
	original = ImageFiles.Load (System.IO.Path.Combine (Store.Root, "images", entry.Id + "-source.png"));
	input.Text = string.Join (Environment.NewLine, document.Regions.Select ((Region r) => r.Text));
	output.Text = string.Join (Environment.NewLine, document.Regions.Select ((Region r) => r.Translated));
	input.Visibility = Visibility.Collapsed;
	Find<System.Windows.Controls.Image> ("SourceImage").Source = original;
	Find<System.Windows.Controls.Image> ("SourceImage").Visibility = Visibility.Visible;
	Find<FrameworkElement> ("SourceImageHint").Visibility = Visibility.Visible;
	Find<FrameworkElement> ("SourcePlaceholder").Visibility = Visibility.Collapsed;
	Find<System.Windows.Controls.Button> ("ImageToolsButton").Visibility = Visibility.Visible;
	Render ();
}


private void DeleteImageHistory (ImageEntry entry)
{
	List<ImageEntry> list = Store.Read ("images.json", new List<ImageEntry> ());
	list.RemoveAll ((ImageEntry x) => x.Id == entry.Id);
	Store.Write ("images.json", list.Take (10).ToList ());
	string[] array = new string[2] { "-source.png", "-translated.png" };
	foreach (string text in array) {
		string path = System.IO.Path.Combine (Store.Root, "images", entry.Id + text);
		try {
			if (File.Exists (path)) {
				File.Delete (path);
			}
		} catch {
		}
	}
}


private void ClearAllImageHistory ()
{
	List<ImageEntry> list = Store.Read ("images.json", new List<ImageEntry> ());
	foreach (ImageEntry item in list) {
		string[] array = new string[2] { "-source.png", "-translated.png" };
		foreach (string text in array) {
			string path = System.IO.Path.Combine (Store.Root, "images", item.Id + text);
			try {
				if (File.Exists (path)) {
					File.Delete (path);
				}
			} catch {
			}
		}
	}
	Store.Write ("images.json", new List<ImageEntry> ());
}


private void SaveImageHistory ()
{
	string text = Guid.NewGuid ().ToString ("N");
	string text2 = System.IO.Path.Combine (Store.Root, "images");
	Directory.CreateDirectory (text2);
	ImageFiles.Save (original, System.IO.Path.Combine (text2, text + "-source.png"));
	ImageFiles.Save (rendered, System.IO.Path.Combine (text2, text + "-translated.png"));
	List<ImageEntry> list = Store.Read ("images.json", new List<ImageEntry> ());
	list.Insert (0, new ImageEntry {
		Id = text,
		Date = DateTime.Now,
		Document = document,
		Source = Code (source),
		Target = Code (target)
	});
	foreach (ImageEntry item in list.Skip (10)) {
		string[] array = new string[2] { "-source.png", "-translated.png" };
		foreach (string text3 in array) {
			string path = System.IO.Path.Combine (text2, item.Id + text3);
			if (File.Exists (path)) {
				File.Delete (path);
			}
		}
	}
	Store.Write ("images.json", list.Take (10).ToList ());
}

}
}
