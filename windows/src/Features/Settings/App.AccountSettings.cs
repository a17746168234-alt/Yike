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
private async void RefreshRemoteAccount ()
{
	if (remoteSession == null) return;
	try {
		using (YikeAccountClient client = new YikeAccountClient ()) await client.Refresh (remoteSession, CancellationToken.None);
		RemoteAccountSessionStore.Save (remoteSession);
		ApplyAccountIdentity ();
	} catch (YikeApiException ex) {
		if (ex.ErrorCode == "login_required") {
			remoteSession = null;
			RemoteAccountSessionStore.Clear ();
			ApplyAccountIdentity ();
			ShowSettings (false, "account");
			Status ("登录已失效，请重新登录。");
		}
	} catch {
		// Keep the encrypted session for temporary offline or proxy failures.
	}
}


private UIElement BuildAccountSettingsPage (bool preview)
{
	ContentControl host = new ContentControl ();
	Action render = null;
	render = delegate {
		RemoteAccountSession shownSession = (preview || previewMode) ? null : remoteSession;
		StackPanel cards = new StackPanel ();
		if (shownSession != null) {
			StackPanel profile = Card (cards, "\ue77b", "个人资料");
			Grid profileGrid = new Grid ();
			profileGrid.ColumnDefinitions.Add (new ColumnDefinition { Width = new GridLength (96.0) });
			profileGrid.ColumnDefinitions.Add (new ColumnDefinition ());
			Border avatarFrame = new Border {
				Width = 76.0, Height = 76.0, CornerRadius = new CornerRadius (38.0),
				Background = OverlayBrush ("#2A3336"), BorderBrush = OverlayBrush ("#354145"), BorderThickness = new Thickness (1.0),
				HorizontalAlignment = System.Windows.HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
			};
			System.Windows.Controls.Image avatarPreview = new System.Windows.Controls.Image { Width = 68.0, Height = 68.0, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
			avatarFrame.Child = avatarPreview;
			SetAccountAvatarPreview (avatarPreview, shownSession);
			profileGrid.Children.Add (avatarFrame);
			StackPanel profileFields = new StackPanel ();
			Grid.SetColumn (profileFields, 1);
			profileGrid.Children.Add (profileFields);
			profileFields.Children.Add (Label ("昵称"));
			System.Windows.Controls.TextBox nickname = AccountTextBox (string.IsNullOrWhiteSpace (shownSession.DisplayName) ? "" : shownSession.DisplayName);
			nickname.MaxLength = 40;
			profileFields.Children.Add (nickname);
			TextBlock profileFeedback = Label ("昵称和头像按账号加密保存在本机，不会上传到服务器。", true);
			profileFeedback.Margin = new Thickness (0.0, 0.0, 0.0, 10.0);
			profileFields.Children.Add (profileFeedback);
			StackPanel profileActions = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
			System.Windows.Controls.Button saveName = OverlayButton ("保存昵称", delegate {
				string value = (nickname.Text ?? "").Trim ();
				if (value.Length < 1 || value.Length > 40) { profileFeedback.Text = "昵称需为 1 至 40 个字符。"; return; }
				remoteSession.DisplayName = value;
				RemoteAccountCustomizationStore.Save (remoteSession);
				RemoteAccountSessionStore.Save (remoteSession);
				ApplyAccountIdentity ();
				profileFeedback.Text = "昵称已保存。";
				Status ("账号昵称已更新");
			}, true);
			saveName.Margin = new Thickness (0.0, 0.0, 8.0, 0.0);
			profileActions.Children.Add (saveName);
			System.Windows.Controls.Button chooseAvatar = OverlayButton ("选择头像", delegate {
				Microsoft.Win32.OpenFileDialog picker = new Microsoft.Win32.OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff" };
				if (picker.ShowDialog (settingsWindow ?? window) != true) return;
				try {
					remoteSession.AvatarPngBase64 = Convert.ToBase64String (AvatarImages.CreatePng (picker.FileName));
					RemoteAccountCustomizationStore.Save (remoteSession);
					RemoteAccountSessionStore.Save (remoteSession);
					SetAccountAvatarPreview (avatarPreview, remoteSession);
					ApplyAccountIdentity ();
					profileFeedback.Text = "头像已裁切为正方形并安全保存。";
					Status ("账号头像已更新");
				} catch (Exception ex) { profileFeedback.Text = ex.Message; }
			});
			chooseAvatar.Margin = new Thickness (0.0, 0.0, 8.0, 0.0);
			profileActions.Children.Add (chooseAvatar);
			System.Windows.Controls.Button resetAvatar = OverlayButton ("恢复默认头像", delegate {
				remoteSession.AvatarPngBase64 = null;
				RemoteAccountCustomizationStore.Save (remoteSession);
				RemoteAccountSessionStore.Save (remoteSession);
				SetAccountAvatarPreview (avatarPreview, remoteSession);
				ApplyAccountIdentity ();
				profileFeedback.Text = "已恢复默认头像。";
			});
			resetAvatar.Margin = new Thickness (0.0);
			profileActions.Children.Add (resetAvatar);
			profileFields.Children.Add (profileActions);
			profile.Children.Add (profileGrid);

			StackPanel account = Card (cards, "\ue77b", "已登录 Yike 账号");
			TextBlock email = Label (shownSession.Email ?? shownSession.Username ?? "Yike 用户");
			email.FontSize = 18.0;
			email.FontWeight = FontWeights.SemiBold;
			account.Children.Add (email);
			TextBlock quota = Label ("体验额度剩余 " + shownSession.Remaining.ToString ("N0") + " / " + shownSession.Granted.ToString ("N0") + " 字符", true);
			quota.Margin = new Thickness (0.0, 8.0, 0.0, 14.0);
			account.Children.Add (quota);
			TextBlock feedback = Label ("会话令牌使用 Windows 数据保护加密，仅保存在这台电脑。", true);
			account.Children.Add (feedback);
			StackPanel actions = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness (0.0, 14.0, 0.0, 0.0) };
			System.Windows.Controls.Button refresh = null;
			refresh = OverlayButton ("刷新额度", async delegate {
				refresh.IsEnabled = false;
				feedback.Text = "正在刷新账号信息…";
				try {
					using (YikeAccountClient client = new YikeAccountClient ()) await client.Refresh (remoteSession, CancellationToken.None);
					RemoteAccountSessionStore.Save (remoteSession);
					ApplyAccountIdentity ();
					Status ("Yike 账号额度已刷新");
					render ();
				} catch (Exception ex) { feedback.Text = ex.Message; refresh.IsEnabled = true; }
			});
			refresh.Margin = new Thickness (0.0, 0.0, 8.0, 0.0);
			actions.Children.Add (refresh);
			System.Windows.Controls.Button logout = null;
			logout = OverlayButton ("退出登录", async delegate {
				logout.IsEnabled = false;
				feedback.Text = "正在退出登录…";
				try {
					using (YikeAccountClient client = new YikeAccountClient ()) await client.Logout (remoteSession, CancellationToken.None);
					remoteSession = null;
					RemoteAccountSessionStore.Clear ();
					ApplyAccountIdentity ();
					Status ("已退出 Yike 账号");
					render ();
				} catch (Exception ex) { feedback.Text = ex.Message; logout.IsEnabled = true; }
			});
			logout.Foreground = OverlayBrush ("#FF656A");
			logout.Margin = new Thickness (0.0);
			actions.Children.Add (logout);
			account.Children.Add (actions);
			StackPanel service = Card (cards, "\ue774", "公共翻译服务");
			service.Children.Add (Label ("登录后优先使用 Yike 公共 DeepL 体验额度；公共额度不可用或当前语言不受支持时，才使用你在“DeepL 密钥与帮助”中配置的个人密钥。服务器不长期保存原文，译文仅为请求去重短时缓存。", true));
		} else {
			StackPanel intro = Card (cards, "\ue77b", "账号与安全");
			intro.Children.Add (Label ("注册并验证邮箱后，可一次领取 200,000 字符公共 DeepL 体验额度。已有账号可直接登录；密码不会保存在本机。", true));

			StackPanel login = Card (cards, "\ue72e", "登录");
			System.Windows.Controls.TextBox loginEmail = AccountTextBox (preview ? "demo@example.com" : "");
			PasswordBox loginPassword = AccountPasswordBox ();
			login.Children.Add (Label ("邮箱"));
			login.Children.Add (loginEmail);
			login.Children.Add (Label ("密码"));
			login.Children.Add (loginPassword);
			TextBlock loginFeedback = Label ("", true);
			loginFeedback.Margin = new Thickness (0.0, 4.0, 0.0, 8.0);
			login.Children.Add (loginFeedback);
			System.Windows.Controls.Button loginButton = null;
			loginButton = OverlayButton ("登录", async delegate {
				loginButton.IsEnabled = false;
				loginFeedback.Text = "正在登录…";
				try {
					using (YikeAccountClient client = new YikeAccountClient ()) remoteSession = await client.Login (loginEmail.Text, loginPassword.Password, CancellationToken.None);
					RemoteAccountCustomizationStore.Apply (remoteSession);
					RemoteAccountSessionStore.Save (remoteSession);
					ApplyAccountIdentity ();
					Status ("Yike 账号登录成功");
					render ();
				} catch (Exception ex) { loginFeedback.Text = ex.Message; loginButton.IsEnabled = true; }
			}, true);
			loginButton.Margin = new Thickness (0.0);
			login.Children.Add (loginButton);

			StackPanel register = Card (cards, "\ue8fa", "注册新账号");
			System.Windows.Controls.TextBox registerEmail = AccountTextBox (preview ? "new@example.com" : "");
			PasswordBox registerPassword = AccountPasswordBox ();
			PasswordBox registerConfirm = AccountPasswordBox ();
			System.Windows.Controls.TextBox code = AccountTextBox ("");
			code.MaxLength = 6;
			code.IsEnabled = false;
			register.Children.Add (Label ("邮箱"));
			register.Children.Add (registerEmail);
			register.Children.Add (Label ("密码（至少 10 个字符）"));
			register.Children.Add (registerPassword);
			register.Children.Add (Label ("确认密码"));
			register.Children.Add (registerConfirm);
			register.Children.Add (Label ("邮件验证码"));
			register.Children.Add (code);
			TextBlock registerFeedback = Label (preview ? "验证码发送后在这里输入 6 位数字完成注册。" : "", true);
			registerFeedback.Margin = new Thickness (0.0, 4.0, 0.0, 8.0);
			register.Children.Add (registerFeedback);
			string challengeId = null;
			string challengeEmail = null;
			StackPanel registerActions = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
			System.Windows.Controls.Button verify = null;
			System.Windows.Controls.Button send = null;
			send = OverlayButton ("发送验证码", async delegate {
				if (registerPassword.Password != registerConfirm.Password) { registerFeedback.Text = "两次输入的密码不一致。"; return; }
				send.IsEnabled = false;
				registerFeedback.Text = "正在发送验证码…";
				try {
					YikeRegistrationChallenge challenge;
					using (YikeAccountClient client = new YikeAccountClient ()) challenge = await client.SendRegistration (registerEmail.Text, registerPassword.Password, CancellationToken.None);
					challengeId = challenge.Id;
					challengeEmail = registerEmail.Text.Trim ();
					code.IsEnabled = true;
					verify.IsEnabled = true;
					registerFeedback.Text = challenge.Message;
					code.Focus ();
				} catch (Exception ex) { registerFeedback.Text = ex.Message; }
				finally { send.IsEnabled = true; }
			});
			send.Margin = new Thickness (0.0, 0.0, 8.0, 0.0);
			registerActions.Children.Add (send);
			verify = OverlayButton ("验证并注册", async delegate {
				verify.IsEnabled = false;
				registerFeedback.Text = "正在验证邮箱并创建账号…";
				try {
					using (YikeAccountClient client = new YikeAccountClient ()) remoteSession = await client.VerifyRegistration (challengeEmail, challengeId, code.Text.Trim (), CancellationToken.None);
					RemoteAccountCustomizationStore.Apply (remoteSession);
					RemoteAccountSessionStore.Save (remoteSession);
					ApplyAccountIdentity ();
					Status ("注册成功，公共翻译额度已到账");
					render ();
				} catch (Exception ex) { registerFeedback.Text = ex.Message; verify.IsEnabled = true; }
			}, true);
			verify.Margin = new Thickness (0.0);
			verify.IsEnabled = preview;
			registerActions.Children.Add (verify);
			register.Children.Add (registerActions);
		}
		host.Content = SettingsPage (cards);
	};
	render ();
	return host;
}


private void SetAccountAvatarPreview (System.Windows.Controls.Image image, RemoteAccountSession session)
{
	BitmapSource avatar = AvatarImages.Load ((session == null) ? null : session.AvatarPngBase64);
	image.Source = avatar ?? defaultAppIcon;
	image.Stretch = (avatar == null) ? Stretch.Uniform : Stretch.UniformToFill;
	image.Clip = new EllipseGeometry (new System.Windows.Point (34.0, 34.0), 34.0, 34.0);
}


private System.Windows.Controls.TextBox AccountTextBox (string value)
{
	return new System.Windows.Controls.TextBox {
		Text = value ?? "", Height = 42.0, Padding = new Thickness (11.0, 8.0, 11.0, 8.0),
		Margin = new Thickness (0.0, 6.0, 0.0, 12.0), FontSize = 14.0
	};
}


private PasswordBox AccountPasswordBox ()
{
	return new PasswordBox {
		Height = 42.0, Padding = new Thickness (11.0, 8.0, 11.0, 8.0),
		Margin = new Thickness (0.0, 6.0, 0.0, 12.0), FontSize = 14.0
	};
}
}
}
