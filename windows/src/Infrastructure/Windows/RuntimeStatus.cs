using System;
using System.Linq;
using System.Speech.Recognition;
using Microsoft.Win32;
namespace WindowsTranslator {
internal static class RuntimeStatus {
	public static string Microphone () {
		try {
			const string consentPath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";
			foreach (RegistryKey hive in new[] { Registry.CurrentUser, Registry.LocalMachine }) {
				using (RegistryKey key = hive.OpenSubKey(consentPath)) {
					if (key != null && string.Equals(key.GetValue("Value") as string, "Deny", StringComparison.OrdinalIgnoreCase)) return "系统麦克风开关已关闭，请打开系统设置";
				}
				using (RegistryKey key = hive.OpenSubKey(consentPath + @"\NonPackaged")) {
					if (key != null && string.Equals(key.GetValue("Value") as string, "Deny", StringComparison.OrdinalIgnoreCase)) return "桌面应用麦克风访问已关闭，请打开系统设置";
				}
			}
			using (MicrophoneLevel meter = new MicrophoneLevel()) { meter.Read(); }
			return "检测到默认麦克风，实际访问请启动语音输入确认";
		} catch (Exception) { return "未检测到可用的默认麦克风，请检查设备与系统设置"; }
	}
	public static string Recognizers () {
		try {
			string[] names = SpeechRecognitionEngine.InstalledRecognizers().Select(r => r.Culture.DisplayName).Distinct().ToArray();
			return names.Length == 0 ? "未安装 Windows 语音识别组件；可改用自动检测" : string.Join("、", names);
		} catch (Exception) { return "无法读取识别组件，请打开系统语音设置确认"; }
	}
}
}
