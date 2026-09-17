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
private void VerifyUiAudit ()
{
	string path = System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "ui-audit-results.txt");
	try {
		this.window.UpdateLayout ();
		if (!string.IsNullOrEmpty (this.window.Title)) {
			throw new Exception ("标题栏左上角仍显示应用名称。");
		}
		if (this.window.WindowStyle != WindowStyle.None || Find<System.Windows.Controls.Button> ("MinimizeWindowButton") == null || Find<System.Windows.Controls.Button> ("MaximizeWindowButton") == null || Find<System.Windows.Controls.Button> ("CloseWindowButton") == null) {
			throw new Exception ("无图标自定义标题栏未正确启用。");
		}
		WindowChrome windowChrome = WindowChrome.GetWindowChrome (this.window);
		Border border = Find<Border> ("MainWindowFrame");
		if (windowChrome == null || !this.window.AllowsTransparency || border.CornerRadius.TopLeft != 32.0 || windowChrome.CornerRadius.TopLeft != 32.0 || windowChrome.CornerRadius.TopRight != 32.0 || windowChrome.CornerRadius.BottomLeft != 32.0 || windowChrome.CornerRadius.BottomRight != 32.0) {
			throw new Exception ("主窗口四角未与设置窗口使用相同圆角。");
		}
		Window window = Dialog ("窗口样式检查", 420.0, 280.0, new Grid ());
		try {
			List<System.Windows.Controls.Button> list = (from x in UiDescendants ((DependencyObject)window.Content).OfType<System.Windows.Controls.Button> ()
				where ((x.Tag as string) ?? "").StartsWith ("dialog-traffic-")
				select x).ToList ();
			if (window.WindowStyle != WindowStyle.None || !window.AllowsTransparency || !window.ShowInTaskbar || list.Count != 3) {
				throw new Exception ("子窗口未使用可恢复的红黄绿窗口控制点。");
			}
		} finally {
			window.Close ();
		}
		if (Find<TextBlock> ("Status").FontSize < 11.5 || Find<TextBlock> ("Status").FontSize > 12.0) {
			throw new Exception ("左下角快捷键提示字号不符合要求。");
		}
		if (Find<TextBlock> ("EngineStatus").Text != "内容由 DeepL 在线处理") {
			throw new Exception ("主页 DeepL 状态提示未更新。");
		}
		System.Windows.Controls.Button button = Find<System.Windows.Controls.Button> ("SettingsButton");
		TextBlock textBlock = button.Content as TextBlock;
		if (textBlock == null || textBlock.ActualWidth <= 0.0 || textBlock.ActualWidth >= button.ActualWidth) {
			throw new Exception ("设置图标仍可能被按钮裁剪。");
		}
		string[] array = new string[2] { "EngineButton", "HistoryButton" };
		foreach (string text in array) {
			System.Windows.Controls.Button button2 = Find<System.Windows.Controls.Button> (text);
			TextBlock textBlock2 = button2.Content as TextBlock;
			if (textBlock2 == null || textBlock2.Inlines.Count != 3) {
				throw new Exception (text + " 没有使用共享基线的图标名称布局。");
			}
			if (textBlock2.ActualHeight <= 0.0 || textBlock2.ActualHeight > button2.ActualHeight) {
				throw new Exception (text + " 图标名称布局超出按钮。");
			}
		}
		if (this.window.FindName ("ReferenceContext") != null || this.window.FindName ("ContextExpander") != null) {
			throw new Exception ("翻译语境控件仍然存在。");
		}
		if (spaceHoldTimer == null || spaceHoldTimer.Interval != TimeSpan.FromMilliseconds (300.0)) {
			throw new Exception ("长按空格语音输入未绑定或触发时长错误。");
		}
		input.Focus ();
		Keyboard.Focus (input);
		PresentationSource inputSource = PresentationSource.FromVisual (this.window);
		System.Windows.Input.KeyEventArgs e = new System.Windows.Input.KeyEventArgs (Keyboard.PrimaryDevice, inputSource, Environment.TickCount, Key.Space);
		e.RoutedEvent = Keyboard.PreviewKeyDownEvent;
		System.Windows.Input.KeyEventArgs e2 = e;
		VoiceShortcutKeyDown (this.window, e2);
		if (!e2.Handled || !spaceKeyDown || !spaceHoldTimer.IsEnabled) {
			throw new Exception ("按下空格没有启动长按语音计时。");
		}
		System.Windows.Input.KeyEventArgs e3 = new System.Windows.Input.KeyEventArgs (Keyboard.PrimaryDevice, inputSource, Environment.TickCount, Key.Space);
		e3.RoutedEvent = Keyboard.PreviewKeyUpEvent;
		System.Windows.Input.KeyEventArgs e4 = e3;
		VoiceShortcutKeyUp (this.window, e4);
		if (!e4.Handled || spaceKeyDown || spaceHoldTimer.IsEnabled || input.Text != " ") {
			throw new Exception ("松开空格没有结束计时或保留短按输入。");
		}
		input.Clear ();
		if (input.ContextMenu == null || input.ContextMenu.Items.Count != 4) {
			throw new Exception ("输入框现代复制粘贴菜单未完整绑定。");
		}
		if (output.IsReadOnly || output.ContextMenu == null || output.ContextMenu.Items.Count != 4) {
			throw new Exception ("译文框未开放编辑、删除或完整编辑菜单。");
		}
		VoiceWaveform voiceWaveform = new VoiceWaveform ();
		voiceWaveform.Update (90);
		Grid grid = (Grid)voiceWaveform.Content.Children [1];
		if (grid.Children.Cast<Border> ().Max ((Border item) => item.Height) < 12.0) {
			throw new Exception ("语音输入波形未随音量变化。");
		}
		input.Text = "你好";
		input.CaretIndex = input.Text.Length;
		BeginVoiceDraft ();
		ApplyVoiceDraft ("OpenAI", false);
		if (input.Text != "你好 OpenAI") {
			throw new Exception ("语音临时结果没有实时写入或区分中英文边界。");
		}
		ApplyVoiceDraft ("OpenAI助手", true);
		if (input.Text != "你好 OpenAI 助手" || voiceDraft.Length != 0) {
			throw new Exception ("语音最终结果没有替换临时草稿。");
		}
		input.Clear ();
		voiceDraft.Cancel ();
		if (Find<System.Windows.Controls.Button> ("ImageToolsButton").IsEnabled) throw new Exception ("没有图片时译图工具仍可点击空操作。");
		if (Find<System.Windows.Controls.Button> ("TranslateButton").IsEnabled || Find<System.Windows.Controls.Button> ("ClearButton").IsEnabled || Find<System.Windows.Controls.Button> ("SpeakButton").IsEnabled || Find<System.Windows.Controls.Button> ("CopyButton").IsEnabled) {
			throw new Exception ("空状态操作按钮没有正确禁用。");
		}
		if (Find<System.Windows.Controls.Button> ("PauseSpeechButton").Visibility != Visibility.Collapsed || Find<System.Windows.Controls.Button> ("StopSpeechButton").Visibility != Visibility.Collapsed) {
			throw new Exception ("未朗读时仍显示暂停或停止按钮。");
		}
		input.Text = "hello";
		if (!Find<System.Windows.Controls.Button> ("TranslateButton").IsEnabled || !Find<System.Windows.Controls.Button> ("ClearButton").IsEnabled) {
			throw new Exception ("输入文字后翻译操作没有启用。");
		}
		output.Text = "你好";
		if (!Find<System.Windows.Controls.Button> ("SpeakButton").IsEnabled || !Find<System.Windows.Controls.Button> ("CopyButton").IsEnabled) {
			throw new Exception ("生成译文后朗读或复制没有启用。");
		}
		Clear ();
		if (Find<TextBlock> ("Status").Text == "已清空") {
			throw new Exception ("清空后仍显示已清空状态。");
		}
		SelectionError ("单窗口测试一");
		Window window2 = activeSelectionPopup;
		SelectionError ("单窗口测试二");
		Window window3 = activeSelectionPopup;
		if (window2 == null || window3 == null || window2 == window3 || window2.IsVisible || !window3.IsVisible) {
			throw new Exception ("划词翻译窗口没有只保留最新一个。");
		}
		System.Windows.Controls.ComboBox comboBox = (System.Windows.Controls.ComboBox)window3.FindName ("PopupTargetLanguage");
		if (comboBox == null || comboBox.Items.Count != codes.Length - 1) {
			throw new Exception ("划词翻译目标语言选择不完整。");
		}
		if (window3.FindName ("ClosePopup") == null || window3.FindName ("MinimizePopup") == null || window3.FindName ("MaximizePopup") == null) {
			throw new Exception ("划词翻译窗口缺少红黄绿控制点。");
		}
		CloseActiveSelection ();
		UIElement root2 = BuildAboutPage ();
		if (!UiDescendants (root2).OfType<System.Windows.Controls.Button> ().Any ((System.Windows.Controls.Button x) => object.Equals (x.Content, "检查更新"))) {
			throw new Exception ("关于与更新页缺少检查更新入口。");
		}
		ShowSettings (false, "appearance");
		this.window.UpdateLayout ();
		if (settingsWindow == null || !settingsWindow.IsVisible || !settingsWindow.IsEnabled || !this.window.IsEnabled) {
			throw new Exception ("设置窗口仍以模态方式锁定主窗口。");
		}
		settingsWindow.UpdateLayout(); if (UiDescendants((DependencyObject)settingsWindow.Content).OfType<TextBlock>().Any(x => x.Text == "账号与安全")) throw new Exception("已删除的账号与安全仍在设置中。"); settingsWindow.Close();
		File.WriteAllText (path, "PASS: top-right icons and labels share one inline baseline; DeepL privacy text is present; translation context controls are absent; space keydown starts the hold-to-talk timer and keyup ends it; live speech drafts update in place and separate Chinese/English boundaries; settings icon fits; text menu has 4 actions; clear restores the shortcut hint; empty-state actions are disabled; translation/result actions enable correctly; idle speech controls stay hidden; selection translation keeps only the latest popup and offers all target languages; account and security section is absent; all other existing UI checks passed.");
	} catch (Exception ex) {
		File.WriteAllText (path, "FAIL: " + ex);
		Environment.ExitCode = 1;
		CloseActiveSelection ();
	}
}


private IEnumerable<DependencyObject> UiDescendants (DependencyObject root)
{
	if (root == null) {
		yield break;
	}
	yield return root;
	int count = VisualTreeHelper.GetChildrenCount (root);
	for (int i = 0; i < count; i++) {
		foreach (DependencyObject item in UiDescendants (VisualTreeHelper.GetChild (root, i))) {
			yield return item;
		}
	}
	ContentControl content = root as ContentControl;
	if (count != 0 || content == null || !(content.Content is DependencyObject)) {
		yield break;
	}
	foreach (DependencyObject item2 in UiDescendants ((DependencyObject)content.Content)) {
		yield return item2;
	}
}

}
}
