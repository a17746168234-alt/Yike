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
private Window window;


private System.Windows.Controls.TextBox input;


private System.Windows.Controls.TextBox output;


private TextBlock status;


private System.Windows.Controls.ComboBox source;


private System.Windows.Controls.ComboBox target;


private Preferences prefs;


private List<Entry> history;


private CancellationTokenSource pending;


private int revision;


private bool busy;


private OcrDocument document;


private BitmapSource original;


private BitmapSource rendered;


private SpeechPlayer speech = new SpeechPlayer ();


private SpeechInput voiceInput = new SpeechInput ();


private readonly AccountStore accounts = new AccountStore ();


private BitmapSource defaultAppIcon;


private Window settingsWindow;
private Action<string> selectSettingsPage;
private bool selectionHotkeyRegistered;
private CancellationTokenSource updateCheckCancel;


private VoiceWaveform voiceWaveform;


private readonly SpeechDraft voiceDraft = new SpeechDraft ();


private bool applyingVoiceDraft;


private DispatcherTimer spaceHoldTimer;


private bool spaceKeyDown;


private bool spaceVoiceMode;


private bool settingOutputText;


private NotifyIcon tray;


private bool exiting;


private bool selecting;


private bool previewMode;


private Window activeSelectionPopup;


private CancellationTokenSource activeSelectionCancel;


private static Mutex singleton;


private static EventWaitHandle activation;


private readonly string[] codes = new string[11] {
	"auto", "ZH-HANS", "EN-US", "JA", "KO", "DE", "FR", "ES", "RU", "PT-BR",
	"IT"
};


private readonly string[] names = new string[11] {
	"自动检测", "中文", "English", "日本語", "한국어", "Deutsch", "Français", "Español", "Русский", "Português",
	"Italiano"
};


private readonly string[] ocrCodes = new string[5] { "auto", "ZH-HANS", "EN-US", "JA", "KO" };


private readonly string[] ocrNames = new string[5] { "自动检测", "中文", "English", "日本語", "한국어" };


private static readonly Dictionary<string, string> LightOverlayPalette = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
	{ "#F3161D20", "#FBFFFFFF" },
	{ "#F1171F22", "#FBFFFFFF" },
	{ "#E5171F22", "#FBFFFFFF" },
	{ "#171F22", "#FBFFFFFF" },
	{ "#D91B282A", "#F1F5F9" },
	{ "#1B2427", "#F1F5F9" },
	{ "#8A1B2326", "#FFFFFFFF" },
	{ "#781B2326", "#FFFFFFFF" },
	{ "#521B2427", "#FFFFFFFF" },
	{ "#481C2528", "#FFFFFFFF" },
	{ "#20282B", "#EEF3F8" },
	{ "#222B2E", "#EEF3F8" },
	{ "#293235", "#EEF3F8" },
	{ "#2A3336", "#EEF3F8" },
	{ "#303A3D", "#EEF3F8" },
	{ "#344044", "#D7E1EC" },
	{ "#344145", "#D7E1EC" },
	{ "#354044", "#D7E1EC" },
	{ "#354145", "#D7E1EC" },
	{ "#3B494D", "#D7E1EC" },
	{ "#55636D71", "#D7E1EC" },
	{ "#253C56", "#DCEBFA" },
	{ "#142A3C", "#EAF4FF" },
	{ "#172B3D", "#EAF4FF" },
	{ "#183758", "#DCEEFF" },
	{ "#26445D", "#BDD9F2" },
	{ "#F4F7F8", "#1D2939" },
	{ "#F2F6F7", "#1D2939" },
	{ "#F1F5F6", "#1D2939" },
	{ "#EEF3F4", "#1D2939" },
	{ "#EDF2F3", "#1D2939" },
	{ "#E7ECEE", "#1D2939" },
	{ "#F0F4F5", "#1D2939" },
	{ "#E2E8EA", "#1D2939" },
	{ "#DCE3E5", "#1D2939" },
	{ "#CFD7D9", "#1D2939" },
	{ "#718083", "#667085" },
	{ "#819095", "#667085" },
	{ "#829095", "#667085" },
	{ "#879296", "#667085" },
	{ "#8E999D", "#667085" },
	{ "#8F9A9D", "#667085" },
	{ "#929DA0", "#667085" },
	{ "#9AA6A9", "#667085" },
	{ "#9DA7AA", "#667085" },
	{ "#A7B1B4", "#667085" },
	{ "#AAB4B7", "#667085" },
	{ "#536166", "#667085" },
	{ "#AFCBE5", "#3F627F" }
};

}
}
