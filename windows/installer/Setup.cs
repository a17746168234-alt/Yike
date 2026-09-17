using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: CompilationRelaxations(8)]
[assembly: AssemblyVersion("0.0.0.0")]
internal static class Setup
{
	private const string AppName = "Yike";

	private const string LegacyAppName = "WindowsTranslator";

	private const string DisplayName = "Yike";

	private const string ShortcutIconName = "app-rounded.ico";

	[STAThread]
	private static int Main(string[] args)
	{
		bool flag = args.Any((string arg) => string.Equals(arg, "--silent", StringComparison.OrdinalIgnoreCase));
		bool flag2 = !flag || args.Any((string arg) => string.Equals(arg, "--launch", StringComparison.OrdinalIgnoreCase));
		try
		{
			if (args.Any((string arg) => string.Equals(arg, "--verify", StringComparison.OrdinalIgnoreCase)))
			{
				VerifyPayload();
				return 0;
			}
			if (!IsCurrentInstallation())
			{
				Install();
			}
			if (flag2)
			{
				ProcessStartInfo processStartInfo = new ProcessStartInfo();
				processStartInfo.FileName = Path.Combine(InstallRoot(), "Yike.exe");
				processStartInfo.WorkingDirectory = InstallRoot();
				processStartInfo.UseShellExecute = true;
				Process.Start(processStartInfo);
			}
			return 0;
		}
		catch (Exception ex)
		{
			try
			{
				File.WriteAllText(Path.Combine(Path.GetTempPath(), "Yike-setup-error.log"), ex.ToString());
			}
			catch
			{
			}
			if (!flag)
			{
				MessageBox.Show("Yike 无法启动：\n\n" + ex.Message, "Yike", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
			return 1;
		}
	}

	private static string InstallRoot()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Yike");
	}

	private static Stream OpenPayload()
	{
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		string text = executingAssembly.GetManifestResourceNames().FirstOrDefault((string name) => name.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase));
		if (text == null)
		{
			throw new InvalidOperationException("安装包内容缺失。");
		}
		return executingAssembly.GetManifestResourceStream(text);
	}

	private static string PayloadDestination(string root, string relative)
	{
		string value = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		string fullPath = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
		if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("安装包包含无效路径。");
		}
		return fullPath;
	}

	private static bool IsCurrentInstallation()
	{
		string text = InstallRoot();
		if (!File.Exists(Path.Combine(text, "Yike.exe")))
		{
			return false;
		}
		try
		{
			using (Stream stream = OpenPayload())
			{
				using (ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Read))
				{
					using (SHA256 sHA = SHA256.Create())
					{
						foreach (ZipArchiveEntry entry in zipArchive.Entries)
						{
							if (entry.Name.Length == 0)
							{
								continue;
							}
							string text2 = PayloadDestination(text, entry.FullName);
							if (!File.Exists(text2) || new FileInfo(text2).Length != entry.Length)
							{
								return false;
							}
							using (FileStream inputStream = File.OpenRead(text2))
							{
								using (Stream inputStream2 = entry.Open())
								{
									if (!sHA.ComputeHash(inputStream).SequenceEqual(sHA.ComputeHash(inputStream2)))
									{
										return false;
									}
								}
							}
						}
					}
				}
			}
			return true;
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}

	private static void Install()
	{
		string text = InstallRoot();
		string installedExe = Path.Combine(text, "Yike.exe");
		string legacyRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WindowsTranslator");
		ReplacePayload(text, installedExe, legacyRoot);
		string exePath = Path.Combine(text, "Yike.exe");
		string iconPath = Path.Combine(text, "app-rounded.ico");
		ConfigureInstallation(text, exePath, iconPath);
	}

	private static void ReplacePayload(string root, string installedExe, string legacyRoot)
	{
		string directoryName = Path.GetDirectoryName(root);
		Directory.CreateDirectory(directoryName);
		string text = root + ".update-" + Guid.NewGuid().ToString("N");
		string text2 = root + ".backup-" + Guid.NewGuid().ToString("N");
		bool flag = false;
		bool flag2 = false;
		try
		{
			Directory.CreateDirectory(text);
			ExtractPayload(text);
			ValidateInstallation(text);
			StopInstalledProcess("Yike", installedExe);
			StopInstalledProcess("whisper-stream", Path.Combine(root, "whisper-runtime", "Release", "whisper-stream.exe"));
			StopInstalledProcess("WindowsTranslator", Path.Combine(legacyRoot, "WindowsTranslator.exe"));
			if (Directory.Exists(root))
			{
				Directory.Move(root, text2);
				flag = true;
			}
			try
			{
				Directory.Move(text, root);
				flag2 = true;
			}
			catch
			{
				if (flag && !Directory.Exists(root))
				{
					Directory.Move(text2, root);
				}
				throw;
			}
			if (flag)
			{
				DeleteOwnedTemporary(text2, directoryName, "Yike.backup-");
			}
		}
		finally
		{
			if (Directory.Exists(text))
			{
				DeleteOwnedTemporary(text, directoryName, "Yike.update-");
			}
			if (Directory.Exists(text2) && !Directory.Exists(root))
			{
				Directory.Move(text2, root);
			}
			else if (Directory.Exists(text2) && flag2)
			{
				DeleteOwnedTemporary(text2, directoryName, "Yike.backup-");
			}
		}
	}

	private static void ExtractPayload(string destinationRoot)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		using (Stream stream = OpenPayload())
		{
			using (ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Read))
			{
				foreach (ZipArchiveEntry entry in zipArchive.Entries)
				{
					string text = PayloadDestination(destinationRoot, entry.FullName);
					if (!hashSet.Add(text))
					{
						throw new InvalidDataException("安装包包含重复路径。");
					}
					if (entry.Name.Length == 0)
					{
						Directory.CreateDirectory(text);
						continue;
					}
					Directory.CreateDirectory(Path.GetDirectoryName(text));
					using (Stream stream2 = entry.Open())
					{
						using (FileStream destination = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None))
						{
							stream2.CopyTo(destination);
						}
					}
				}
			}
		}
	}

	private static void ValidateInstallation(string root)
	{
		string[] array = new string[15]
		{
			"Yike.exe",
			"MainWindow.xaml",
			"SelectionWindow.xaml",
			"ocr.ps1",
			"speech-online.py",
			"speech-local.ps1",
			"app.png",
			"app.ico",
			"update-feed.json",
			"uninstall.ps1",
			Path.Combine("speech-runtime", "python.exe"),
			Path.Combine("whisper-runtime", "ggml-base-q5_1.bin"),
			Path.Combine("whisper-runtime", "Release", "whisper-stream.exe"),
			Path.Combine("whisper-runtime", "Release", "whisper.dll"),
			Path.Combine("whisper-runtime", "Release", "SDL2.dll")
		};
		string[] array2 = array;
		foreach (string text in array2)
		{
			string text2 = Path.Combine(root, text);
			if (!File.Exists(text2) || new FileInfo(text2).Length == 0)
			{
				throw new InvalidDataException("安装包缺少必要文件：" + text);
			}
		}
	}

	private static void ConfigureInstallation(string root, string exePath, string iconPath)
	{
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs", "Yike");
		CreateShortcut(Path.Combine(path, "Yike.lnk"), exePath, root, iconPath);
		CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Yike.lnk"), exePath, root, iconPath);
		RemoveLegacyRegistration();
		string text = Path.Combine(root, "uninstall.ps1");
		using (RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Yike"))
		{
			registryKey.SetValue("DisplayName", "Yike");
			registryKey.SetValue("DisplayVersion", "1.2.0");
			registryKey.SetValue("Publisher", "Yike");
			registryKey.SetValue("InstallLocation", root);
			registryKey.SetValue("DisplayIcon", (File.Exists(iconPath) ? iconPath : exePath) + ",0");
			registryKey.SetValue("UninstallString", "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + text + "\"");
			registryKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
			registryKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
		}
	}

	private static void VerifyPayload()
	{
		string text = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
		string text2 = Path.Combine(text, "Yike-verify-" + Guid.NewGuid().ToString("N"));
		try
		{
			Directory.CreateDirectory(text2);
			ExtractPayload(text2);
			ValidateInstallation(text2);
		}
		finally
		{
			if (Directory.Exists(text2))
			{
				DeleteOwnedTemporary(text2, text, "Yike-verify-");
			}
		}
	}

	private static void DeleteOwnedTemporary(string path, string expectedParent, string prefix)
	{
		string path2 = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
		string b = Path.GetFullPath(expectedParent).TrimEnd(Path.DirectorySeparatorChar);
		string fileName = Path.GetFileName(path2);
		Guid result;
		if (!string.Equals(Path.GetDirectoryName(path2), b, StringComparison.OrdinalIgnoreCase) || !fileName.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(fileName.Substring(prefix.Length), "N", out result))
		{
			throw new InvalidOperationException("临时安装目录校验失败，已保留文件。");
		}
		Directory.Delete(path2, true);
	}

	private static void StopInstalledProcess(string processName, string expectedPath)
	{
		Process[] processesByName = Process.GetProcessesByName(processName);
		foreach (Process process in processesByName)
		{
			try
			{
				if (string.Equals(process.MainModule.FileName, expectedPath, StringComparison.OrdinalIgnoreCase))
				{
					if (!process.CloseMainWindow() || !process.WaitForExit(3000))
					{
						process.Kill();
					}
					if (!process.WaitForExit(5000))
					{
						throw new IOException("旧版程序尚未退出，请关闭后重试。");
					}
				}
			}
			catch (InvalidOperationException)
			{
			}
			catch (Win32Exception)
			{
			}
			finally
			{
				process.Dispose();
			}
		}
	}

	private static void RemoveLegacyRegistration()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
		DeleteFileIfPresent(Path.Combine(folderPath, "Windows翻译.lnk"));
		DeleteFileIfPresent(Path.Combine(folderPath, "WindowsTranslator.lnk"));
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs", "WindowsTranslator");
		DeleteFileIfPresent(Path.Combine(text, "Windows翻译.lnk"));
		DeleteFileIfPresent(Path.Combine(text, "WindowsTranslator.lnk"));
		try
		{
			if (Directory.Exists(text) && !Directory.EnumerateFileSystemEntries(text).Any())
			{
				Directory.Delete(text);
			}
		}
		catch
		{
		}
		try
		{
			Registry.CurrentUser.DeleteSubKeyTree("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\WindowsTranslator", false);
		}
		catch
		{
		}
	}

	private static void DeleteFileIfPresent(string path)
	{
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	private static void CreateShortcut(string path, string target, string workingDirectory, string iconPath)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		DeleteFileIfPresent(path);
		Type typeFromProgID = Type.GetTypeFromProgID("WScript.Shell");
		if (typeFromProgID == null)
		{
			throw new InvalidOperationException("无法创建 Windows 快捷方式。");
		}
		dynamic val = Activator.CreateInstance(typeFromProgID);
		dynamic val2 = val.CreateShortcut(path);
		val2.TargetPath = target;
		val2.WorkingDirectory = workingDirectory;
		val2.IconLocation = (File.Exists(iconPath) ? iconPath : target) + ",0";
		val2.Save();
	}
}
