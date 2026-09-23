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
[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.0")]
[assembly: AssemblyInformationalVersion("2.1")]
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
			string registeredRoot = RegisteredInstallRoot();
			if (!string.IsNullOrWhiteSpace(registeredRoot))
			{
				try { registeredRoot = NormalizeInstallRoot(registeredRoot); }
				catch { registeredRoot = null; }
			}
			string requestedRoot = ArgumentValue(args, "--install-dir");
			string installRoot;
			try
			{
				installRoot = NormalizeInstallRoot(requestedRoot ?? registeredRoot ?? DefaultInstallRoot());
			}
			catch
			{
				if (!string.IsNullOrWhiteSpace(requestedRoot)) throw;
				installRoot = NormalizeInstallRoot(DefaultInstallRoot());
			}
			if (!flag && !ChooseInstallRoot(installRoot, registeredRoot, out installRoot))
			{
				return 0;
			}
			ValidateInstallDestination(installRoot, registeredRoot);
			Install(installRoot, registeredRoot);
			if (flag2)
			{
				ProcessStartInfo processStartInfo = new ProcessStartInfo();
				processStartInfo.FileName = Path.Combine(installRoot, "Yike.exe");
				processStartInfo.WorkingDirectory = installRoot;
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

	private static string DefaultInstallRoot()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Yike");
	}

	private static string ArgumentValue(string[] args, string name)
	{
		for (int i = 0; i < args.Length; i++)
		{
			if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)) return args[i].Substring(name.Length + 1);
			if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) return args[i + 1];
		}
		return null;
	}

	private static string RegisteredInstallRoot()
	{
		try
		{
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Yike"))
			{
				return key == null ? null : key.GetValue("InstallLocation") as string;
			}
		}
		catch
		{
			return null;
		}
	}

	private static string NormalizeInstallRoot(string path)
	{
		if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("请选择安装位置。");
		string expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
		if (!Path.IsPathRooted(expanded)) throw new InvalidDataException("安装位置必须是本机磁盘上的完整路径。");
		string fullPath = Path.GetFullPath(expanded).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		if (fullPath.StartsWith("\\\\", StringComparison.Ordinal)) throw new InvalidDataException("Yike 需要安装到本机磁盘，不能使用网络路径。");
		string driveRoot = Path.GetPathRoot(fullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		if (string.Equals(fullPath, driveRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("不能直接安装到磁盘根目录，请选择或创建一个 Yike 文件夹。");
		if (fullPath.Length > 180) throw new InvalidDataException("安装路径过长，请选择更短的文件夹路径。");
		DriveInfo drive = new DriveInfo(Path.GetPathRoot(fullPath));
		if (!drive.IsReady) throw new InvalidDataException("所选磁盘当前不可用。");
		return fullPath;
	}

	private static bool SamePath(string left, string right)
	{
		if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
		try { return string.Equals(NormalizeInstallRoot(left), NormalizeInstallRoot(right), StringComparison.OrdinalIgnoreCase); }
		catch { return false; }
	}

	private static bool IsInsidePath(string child, string parent)
	{
		string childPath = NormalizeInstallRoot(child) + Path.DirectorySeparatorChar;
		string parentPath = NormalizeInstallRoot(parent) + Path.DirectorySeparatorChar;
		return childPath.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsYikeInstallation(string root)
	{
		try
		{
			string exe = Path.Combine(root, "Yike.exe");
			return File.Exists(exe) && File.Exists(Path.Combine(root, "MainWindow.xaml")) && File.Exists(Path.Combine(root, "uninstall.ps1")) && string.Equals(FileVersionInfo.GetVersionInfo(exe).ProductName, "Yike for Windows", StringComparison.Ordinal);
		}
		catch
		{
			return false;
		}
	}

	private static void ValidateInstallDestination(string root, string registeredRoot)
	{
		root = NormalizeInstallRoot(root);
		if (!string.IsNullOrWhiteSpace(registeredRoot) && !SamePath(root, registeredRoot) && (IsInsidePath(root, registeredRoot) || IsInsidePath(registeredRoot, root)))
		{
			throw new InvalidDataException("新安装位置不能位于当前 Yike 文件夹内部，也不能包含当前 Yike 文件夹。");
		}
		if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any() && !IsYikeInstallation(root))
		{
			throw new InvalidDataException("所选文件夹不是空文件夹。为避免覆盖其他文件，请选择空文件夹或现有 Yike 安装目录。");
		}
	}

	private static string ExistingDirectory(string path)
	{
		string current = path;
		while (!string.IsNullOrWhiteSpace(current) && !Directory.Exists(current)) current = Path.GetDirectoryName(current);
		return string.IsNullOrWhiteSpace(current) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : current;
	}

	private static bool ChooseInstallRoot(string initialRoot, string registeredRoot, out string selectedRoot)
	{
		selectedRoot = null;
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		using (Form form = new Form())
		{
			form.Text = "安装 Yike 2.1";
			form.Width = 680;
			form.Height = 285;
			form.StartPosition = FormStartPosition.CenterScreen;
			form.FormBorderStyle = FormBorderStyle.FixedDialog;
			form.MaximizeBox = false;
			form.MinimizeBox = false;
			form.ShowIcon = false;
			form.AutoScaleMode = AutoScaleMode.Dpi;
			TableLayoutPanel layout = new TableLayoutPanel();
			layout.Dock = DockStyle.Fill;
			layout.Padding = new Padding(28, 24, 28, 22);
			layout.ColumnCount = 1;
			layout.RowCount = 5;
			layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
			Label title = new Label();
			title.Text = IsYikeInstallation(initialRoot) ? "更新或更改 Yike 安装位置" : "选择 Yike 安装位置";
			title.AutoSize = true;
			title.Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, 16f, System.Drawing.FontStyle.Bold);
			title.Margin = new Padding(0, 0, 0, 8);
			layout.Controls.Add(title, 0, 0);
			Label description = new Label();
			description.Text = "可以安装到任意可写的本机磁盘和文件夹。请选择空文件夹或现有 Yike 安装目录。";
			description.AutoSize = true;
			description.Margin = new Padding(0, 0, 0, 16);
			layout.Controls.Add(description, 0, 1);
			Label locationLabel = new Label();
			locationLabel.Text = "安装位置";
			locationLabel.AutoSize = true;
			locationLabel.Margin = new Padding(0, 0, 0, 6);
			layout.Controls.Add(locationLabel, 0, 2);
			TableLayoutPanel pathRow = new TableLayoutPanel();
			pathRow.Dock = DockStyle.Top;
			pathRow.Height = 34;
			pathRow.ColumnCount = 2;
			pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
			pathRow.Margin = new Padding(0, 0, 0, 18);
			TextBox pathBox = new TextBox();
			pathBox.Text = initialRoot;
			pathBox.Dock = DockStyle.Fill;
			pathBox.Margin = new Padding(0, 3, 10, 3);
			Button browseButton = new Button();
			browseButton.Text = "浏览…";
			browseButton.Dock = DockStyle.Fill;
			browseButton.Margin = new Padding(0);
			pathRow.Controls.Add(pathBox, 0, 0);
			pathRow.Controls.Add(browseButton, 1, 0);
			layout.Controls.Add(pathRow, 0, 3);
			FlowLayoutPanel actions = new FlowLayoutPanel();
			actions.Dock = DockStyle.Fill;
			actions.FlowDirection = FlowDirection.RightToLeft;
			actions.WrapContents = false;
			Button installButton = new Button();
			installButton.Text = IsYikeInstallation(initialRoot) ? "安装 / 更新" : "安装";
			installButton.Width = 112;
			installButton.Height = 34;
			Button cancelButton = new Button();
			cancelButton.Text = "取消";
			cancelButton.Width = 88;
			cancelButton.Height = 34;
			cancelButton.DialogResult = DialogResult.Cancel;
			actions.Controls.Add(installButton);
			actions.Controls.Add(cancelButton);
			layout.Controls.Add(actions, 0, 4);
			form.Controls.Add(layout);
			form.AcceptButton = installButton;
			form.CancelButton = cancelButton;
			browseButton.Click += delegate
			{
				using (FolderBrowserDialog browser = new FolderBrowserDialog())
				{
					browser.Description = "选择 Yike 的安装文件夹";
					browser.ShowNewFolderButton = true;
					browser.SelectedPath = ExistingDirectory(pathBox.Text);
					if (browser.ShowDialog(form) == DialogResult.OK) pathBox.Text = browser.SelectedPath;
				}
			};
			string chosen = null;
			installButton.Click += delegate
			{
				try
				{
					chosen = NormalizeInstallRoot(pathBox.Text);
					ValidateInstallDestination(chosen, registeredRoot);
					form.DialogResult = DialogResult.OK;
					form.Close();
				}
				catch (Exception ex)
				{
					MessageBox.Show(form, ex.Message, "无法使用此安装位置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			};
			if (form.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(chosen)) return false;
			selectedRoot = chosen;
			return true;
		}
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

	private static bool IsCurrentInstallation(string root)
	{
		string text = root;
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

	private static void Install(string root, string previousRoot)
	{
		string text = root;
		string installedExe = Path.Combine(text, "Yike.exe");
		string legacyRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WindowsTranslator");
		if (!IsCurrentInstallation(text)) ReplacePayload(text, installedExe, legacyRoot);
		string exePath = Path.Combine(text, "Yike.exe");
		string iconPath = Path.Combine(text, "app-rounded.ico");
		ConfigureInstallation(text, exePath, iconPath);
		if (!string.IsNullOrWhiteSpace(previousRoot) && !SamePath(text, previousRoot)) RemovePreviousInstallation(previousRoot);
	}

	private static void RemovePreviousInstallation(string root)
	{
		if (!IsYikeInstallation(root)) return;
		try
		{
			string fullRoot = NormalizeInstallRoot(root);
			StopInstalledProcess("Yike", Path.Combine(fullRoot, "Yike.exe"));
			StopInstalledProcess("whisper-stream", Path.Combine(fullRoot, "whisper-runtime", "Release", "whisper-stream.exe"));
			HashSet<string> ownedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			using (Stream stream = OpenPayload())
			using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
			{
				foreach (ZipArchiveEntry entry in archive.Entries)
				{
					if (entry.Name.Length == 0) continue;
					ownedFiles.Add(PayloadDestination(fullRoot, entry.FullName));
				}
			}
			if (Directory.GetFiles(fullRoot, "*", SearchOption.AllDirectories).Any((string path) => !ownedFiles.Contains(Path.GetFullPath(path)))) return;
			foreach (string installedFile in ownedFiles)
			{
				if (File.Exists(installedFile)) File.Delete(installedFile);
			}
			foreach (string directory in Directory.GetDirectories(fullRoot, "*", SearchOption.AllDirectories).OrderByDescending((string path) => path.Length))
			{
				if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
			}
			if (Directory.Exists(fullRoot) && !Directory.EnumerateFileSystemEntries(fullRoot).Any()) Directory.Delete(fullRoot);
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
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
			if (File.Exists(installedExe)) {
				Version currentVersion, payloadVersion;
				if (Version.TryParse(FileVersionInfo.GetVersionInfo(installedExe).FileVersion, out currentVersion) &&
					Version.TryParse(FileVersionInfo.GetVersionInfo(Path.Combine(text, "Yike.exe")).FileVersion, out payloadVersion) && currentVersion > payloadVersion) return;
			}
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
		string[] array = new string[16]
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
			Path.Combine("whisper-runtime", "ggml-small-q8_0.bin"),
			Path.Combine("whisper-runtime", "Release", "whisper-stream.exe"),
			Path.Combine("whisper-runtime", "Release", "whisper-cli.exe"),
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
			Version installedVersion;
			string fileVersion = FileVersionInfo.GetVersionInfo(exePath).FileVersion;
			if (!Version.TryParse(fileVersion, out installedVersion)) throw new InvalidDataException("无法读取已安装程序的版本。");
			registryKey.SetValue("DisplayVersion", installedVersion.Build <= 0 ? installedVersion.ToString(2) : installedVersion.ToString(3));
			registryKey.SetValue("Publisher", "Yike");
			registryKey.SetValue("InstallLocation", root);
			registryKey.SetValue("DisplayIcon", (File.Exists(iconPath) ? iconPath : exePath) + ",0");
			registryKey.SetValue("UninstallString", "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + text + "\"");
			registryKey.SetValue("QuietUninstallString", "powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File \"" + text + "\"");
			long installedBytes = 0L;
			foreach (string installedFile in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) installedBytes += new FileInfo(installedFile).Length;
			registryKey.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, Math.Max(1L, installedBytes / 1024L)), RegistryValueKind.DWord);
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
