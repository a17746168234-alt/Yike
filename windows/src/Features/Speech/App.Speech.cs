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
private async void SpeakText (string text, string targetCode, bool propagate = false)
{
	if (!string.IsNullOrWhiteSpace (text)) {
		await speech.Speak (text, targetCode, prefs.VoiceGender, prefs.Rate, prefs.OnlineSpeech);
	}
}


private void UpdateSpeechButtons ()
{
	bool flag = speech.State != "idle";
	System.Windows.Controls.Button button = Find<System.Windows.Controls.Button> ("PauseSpeechButton");
	System.Windows.Controls.Button button2 = Find<System.Windows.Controls.Button> ("StopSpeechButton");
	SetButtonIcon ("PauseSpeechButton", (speech.State == "paused") ? "\ue768" : "\ue769", (speech.State == "paused") ? "继续" : "暂停");
	button.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
	button2.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
	button.IsEnabled = speech.State == "playing" || speech.State == "paused";
	button2.IsEnabled = flag;
	if (flag) {
		Status ((speech.State == "loading") ? "正在生成语音…" : ((speech.State == "paused") ? "朗读已暂停" : "正在朗读"));
	}
}

}
}
