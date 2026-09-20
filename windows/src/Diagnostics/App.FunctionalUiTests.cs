using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
namespace WindowsTranslator {
public partial class App {
	private static void UiCheck(bool value, string message) { if (!value) throw new Exception(message); }
	private System.Windows.Controls.Button UiButton(DependencyObject root, string caption) {
		return UiDescendants(root).OfType<System.Windows.Controls.Button>().First(b=>object.Equals(b.Content,caption));
	}
	private async Task VerifyFunctionalUiAsync() {
		string report = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"functional-ui-results.txt");
		try {
			ShowSettings(false,"appearance"); Window first = settingsWindow;
			foreach(string page in new[] { "appearance","account","deepl","speech","shortcuts","permissions","ocr","history","about" }) {
				ShowSettings(false,page); settingsWindow.UpdateLayout();
				UiCheck(settingsWindow==first && first.IsVisible, "settings navigation creates/reuses wrong window: " + page);
				if(page=="account") UiCheck(UiDescendants(first.Content as DependencyObject).OfType<TextBlock>().Any(t=>t.Text=="账号与安全") && UiDescendants(first.Content as DependencyObject).OfType<System.Windows.Controls.Button>().Any(b=>object.Equals(b.Content,"登录")) && UiDescendants(first.Content as DependencyObject).OfType<System.Windows.Controls.Button>().Any(b=>object.Equals(b.Content,"发送验证码")), "account registration and login UI is missing");
				FrameworkElement content = (FrameworkElement)first.Content;
				RenderTargetBitmap image = new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);
				image.Render(content); ImageFiles.Save(image,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-"+page+"-"+prefs.Appearance+".png"));
			}
			UiCheck(UiDescendants(first.Content as DependencyObject).OfType<TextBlock>().Any(t=>t.Text.Contains("当前安装版本") && t.Text.Contains(typeof(App).Assembly.GetName().Version.ToString(3))), "About version doesn't come from running assembly");
			RemoteAccountSession savedSession=remoteSession; bool savedPreviewMode=previewMode;
			try {
				previewMode=false;
				remoteSession=new RemoteAccountSession { Token="preview-token", Email="profile@example.com", DisplayName="Yike 用户", Granted=200000, Remaining=200000 };
				DependencyObject profileRoot=BuildAccountSettingsPage(false) as DependencyObject;
				UiCheck(UiDescendants(profileRoot).OfType<TextBlock>().Any(t=>t.Text=="个人资料") && UiDescendants(profileRoot).OfType<System.Windows.Controls.Button>().Any(b=>object.Equals(b.Content,"保存昵称")) && UiDescendants(profileRoot).OfType<System.Windows.Controls.Button>().Any(b=>object.Equals(b.Content,"选择头像")) && UiDescendants(profileRoot).OfType<System.Windows.Controls.Button>().Any(b=>object.Equals(b.Content,"恢复默认头像")), "signed-in nickname/avatar controls are missing");
			} finally { remoteSession=savedSession; previewMode=savedPreviewMode; }
			ShowSettings(false,"speech"); first.UpdateLayout();
			DependencyObject root = first.Content as DependencyObject;
			UiCheck(!UiButton(root,"停止").IsEnabled && !UiButton(root,"暂停").IsEnabled, "idle settings playback controls are active");
			UiButton(root,"男声").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
			UiCheck(prefs.VoiceGender=="male", "voice choice does not update preference");
			UiDescendants(root).OfType<Slider>().Single().Value=3;
			UiCheck(prefs.Rate==3, "speech speed setting not applied");
			CheckBox online=UiDescendants(root).OfType<CheckBox>().Single(); online.IsChecked=false;
			UiCheck(!prefs.OnlineSpeech,"online/local switch does not update preference");
			ShowSettings(false,"history"); first.UpdateLayout(); root=first.Content as DependencyObject;
			foreach(CheckBox box in UiDescendants(root).OfType<CheckBox>()) box.IsChecked=false;
			UiCheck(!prefs.History && !prefs.ImageHistory,"history toggles do not update preferences");
			ShowSettings(false,"ocr"); first.UpdateLayout();
			System.Windows.Controls.ComboBox languages=UiDescendants(first.Content as DependencyObject).OfType<System.Windows.Controls.ComboBox>().Single(); languages.SelectedIndex=2;
			UiCheck(prefs.OcrLanguage=="EN-US","OCR language selection not applied");
			ShowSettings(false,"permissions"); first.UpdateLayout(); root=first.Content as DependencyObject;
			System.Windows.Controls.Button refresh=UiButton(root,"刷新状态"); refresh.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
			DateTime deadline=DateTime.UtcNow.AddSeconds(20); while(!refresh.IsEnabled && DateTime.UtcNow<deadline) await Task.Delay(50);
			UiCheck(refresh.IsEnabled && UiDescendants(root).OfType<TextBlock>().Any(t=>t.Text.StartsWith("已检测 ")),"component refresh didn't finish");
			first.Close();
			SelectionError("close button test"); Window popup=activeSelectionPopup;
			((System.Windows.Controls.Button)popup.FindName("ClosePopupButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
			UiCheck(!popup.IsVisible && activeSelectionPopup==null,"top-right selection close doesn't close and clear active popup");
			Grid header=new Grid(); System.Windows.Controls.Button button=new System.Windows.Controls.Button(); TextBlock glyph=new TextBlock { Text="×" }; button.Content=glyph; header.Children.Add(button);
			UiCheck(DialogChrome.IsInteractiveSource(glyph,header),"button text can trigger title drag");
			File.WriteAllText(report,"PASS: all nine settings pages navigate in one window; account registration/login plus signed-in nickname/avatar controls are present; actual assembly version displayed; voice gender/speed/local-online and history/OCR preferences applied; idle playback controls disabled; live component refresh completed; popup close clears active window; header button descendants excluded from dragging. Preview tests do not write user preferences.\n" + RuntimeStatus.Microphone() + "\n" + RuntimeStatus.Recognizers() + "\n" + await OcrService.InstalledLanguages());
		} catch(Exception ex) { File.WriteAllText(report,"FAIL: "+ex); Environment.ExitCode=1; }
		finally { if(settingsWindow!=null) settingsWindow.Close(); CloseActiveSelection(); }
	}
}
}
