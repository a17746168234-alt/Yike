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
internal sealed class AccountStore
{
	private const int PasswordIterations = 150000;

	private static readonly byte[] Entropy = Encoding.UTF8.GetBytes ("Yike.LocalAccounts.v1");

	private readonly object gate = new object ();

	private readonly string path;

	private readonly Func<byte[], byte[]> protect;

	private readonly Func<byte[], byte[]> unprotect;

	private AccountVaultData data;

	private string loadError;

	public string LoadError {
		get {
			lock (gate) {
				return loadError;
			}
		}
	}

	public AccountProfile Current {
		get {
			lock (gate) {
				return Profile (FindById (data.ActiveId));
			}
		}
	}

	public IList<AccountProfile> Profiles {
		get {
			lock (gate) {
				return data.Accounts.OrderByDescending ((LocalAccountRecord x) => x.LastLoginAt).Select (Profile).ToList ();
			}
		}
	}

	public AccountStore ()
		: this (System.IO.Path.Combine (Store.Root, "accounts.bin"), ProtectForCurrentUser, UnprotectForCurrentUser)
	{
	}

	internal AccountStore (string path, Func<byte[], byte[]> protect, Func<byte[], byte[]> unprotect)
	{
		this.path = path;
		this.protect = protect;
		this.unprotect = unprotect;
		Load ();
	}

	public AccountResult Register (string email, string password, string confirmation)
	{
		lock (gate) {
			if (loadError != null) {
				return AccountResult.Fail (loadError);
			}
			string normalized;
			string text = ValidateEmail (email, out normalized);
			if (text != null) {
				return AccountResult.Fail (text);
			}
			text = ValidatePassword (password);
			if (text != null) {
				return AccountResult.Fail (text);
			}
			if (password != confirmation) {
				return AccountResult.Fail ("两次输入的密码不一致");
			}
			if (data.Accounts.Any ((LocalAccountRecord x) => string.Equals (x.Email, normalized, StringComparison.OrdinalIgnoreCase))) {
				return AccountResult.Fail ("该邮箱已在此电脑注册，请直接登录");
			}
			byte[] array = new byte[24];
			using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create ()) {
				randomNumberGenerator.GetBytes (array);
			}
			DateTime now = DateTime.Now;
			LocalAccountRecord localAccountRecord = new LocalAccountRecord ();
			localAccountRecord.Id = Guid.NewGuid ().ToString ("N");
			localAccountRecord.Email = normalized;
			localAccountRecord.DisplayName = DefaultName (normalized);
			localAccountRecord.Salt = Convert.ToBase64String (array);
			localAccountRecord.PasswordHash = Convert.ToBase64String (Hash (password, array, 150000));
			localAccountRecord.Iterations = 150000;
			localAccountRecord.CreatedAt = now;
			localAccountRecord.LastLoginAt = now;
			LocalAccountRecord localAccountRecord2 = localAccountRecord;
			string activeId = data.ActiveId;
			data.Accounts.Add (localAccountRecord2);
			data.ActiveId = localAccountRecord2.Id;
			string text2 = Save ();
			if (text2 != null) {
				data.Accounts.Remove (localAccountRecord2);
				data.ActiveId = activeId;
				return AccountResult.Fail (text2);
			}
			return AccountResult.Ok ("账号已创建并登录", Profile (localAccountRecord2));
		}
	}

	public AccountResult Login (string email, string password)
	{
		lock (gate) {
			if (loadError != null) {
				return AccountResult.Fail (loadError);
			}
			string normalized;
			string text = ValidateEmail (email, out normalized);
			if (text != null) {
				return AccountResult.Fail (text);
			}
			if (string.IsNullOrEmpty (password) || password.Length > 256) {
				return AccountResult.Fail ("邮箱或密码不正确");
			}
			LocalAccountRecord localAccountRecord = data.Accounts.FirstOrDefault ((LocalAccountRecord x) => string.Equals (x.Email, normalized, StringComparison.OrdinalIgnoreCase));
			if (localAccountRecord == null || !PasswordMatches (localAccountRecord, password)) {
				return AccountResult.Fail ("邮箱或密码不正确");
			}
			string activeId = data.ActiveId;
			DateTime lastLoginAt = localAccountRecord.LastLoginAt;
			data.ActiveId = localAccountRecord.Id;
			localAccountRecord.LastLoginAt = DateTime.Now;
			string text2 = Save ();
			if (text2 != null) {
				data.ActiveId = activeId;
				localAccountRecord.LastLoginAt = lastLoginAt;
				return AccountResult.Fail (text2);
			}
			return AccountResult.Ok ("登录成功", Profile (localAccountRecord));
		}
	}

	public AccountResult Logout ()
	{
		lock (gate) {
			if (loadError != null) {
				return AccountResult.Fail (loadError);
			}
			if (string.IsNullOrEmpty (data.ActiveId)) {
				return AccountResult.Ok ("当前未登录");
			}
			string activeId = data.ActiveId;
			data.ActiveId = null;
			string text = Save ();
			if (text != null) {
				data.ActiveId = activeId;
				return AccountResult.Fail (text);
			}
			return AccountResult.Ok ("已安全退出登录");
		}
	}

	public AccountResult ChangePassword (string oldPassword, string newPassword, string confirmation)
	{
		lock (gate) {
			if (loadError != null) {
				return AccountResult.Fail (loadError);
			}
			LocalAccountRecord localAccountRecord = FindById (data.ActiveId);
			if (localAccountRecord == null) {
				return AccountResult.Fail ("请先登录账号");
			}
			if (!PasswordMatches (localAccountRecord, oldPassword)) {
				return AccountResult.Fail ("当前密码不正确");
			}
			string text = ValidatePassword (newPassword);
			if (text != null) {
				return AccountResult.Fail (text);
			}
			if (newPassword != confirmation) {
				return AccountResult.Fail ("两次输入的新密码不一致");
			}
			if (oldPassword == newPassword) {
				return AccountResult.Fail ("新密码不能与当前密码相同");
			}
			string salt = localAccountRecord.Salt;
			string passwordHash = localAccountRecord.PasswordHash;
			int iterations = localAccountRecord.Iterations;
			byte[] array = new byte[24];
			using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create ()) {
				randomNumberGenerator.GetBytes (array);
			}
			localAccountRecord.Salt = Convert.ToBase64String (array);
			localAccountRecord.PasswordHash = Convert.ToBase64String (Hash (newPassword, array, 150000));
			localAccountRecord.Iterations = 150000;
			string text2 = Save ();
			if (text2 != null) {
				localAccountRecord.Salt = salt;
				localAccountRecord.PasswordHash = passwordHash;
				localAccountRecord.Iterations = iterations;
				return AccountResult.Fail (text2);
			}
			return AccountResult.Ok ("密码已更新", Profile (localAccountRecord));
		}
	}

	public AccountResult UpdateDisplayName (string displayName)
	{
		lock (gate) {
			if (loadError != null) {
				return AccountResult.Fail (loadError);
			}
			LocalAccountRecord localAccountRecord = FindById (data.ActiveId);
			if (localAccountRecord == null) {
				return AccountResult.Fail ("请先登录账号");
			}
			string text = (displayName ?? "").Trim ();
			if (text.Length < 1 || text.Length > 40) {
				return AccountResult.Fail ("昵称需为 1 至 40 个字符");
			}
			if (text == localAccountRecord.DisplayName) {
				return AccountResult.Ok ("昵称未变化", Profile (localAccountRecord));
			}
			string displayName2 = localAccountRecord.DisplayName;
			localAccountRecord.DisplayName = text;
			string text2 = Save ();
			if (text2 != null) {
				localAccountRecord.DisplayName = displayName2;
				return AccountResult.Fail (text2);
			}
			return AccountResult.Ok ("昵称已保存", Profile (localAccountRecord));
		}
	}

	public AccountResult UpdateAvatar (byte[] png)
	{
		lock (gate) {
			if (loadError != null) {
				return AccountResult.Fail (loadError);
			}
			LocalAccountRecord localAccountRecord = FindById (data.ActiveId);
			if (localAccountRecord == null) {
				return AccountResult.Fail ("请先登录账号");
			}
			string text = ValidateAvatar (png);
			if (text != null) {
				return AccountResult.Fail (text);
			}
			string avatarPngBase = localAccountRecord.AvatarPngBase64;
			localAccountRecord.AvatarPngBase64 = Convert.ToBase64String (png);
			string text2 = Save ();
			if (text2 != null) {
				localAccountRecord.AvatarPngBase64 = avatarPngBase;
				return AccountResult.Fail (text2);
			}
			return AccountResult.Ok ("头像已保存", Profile (localAccountRecord));
		}
	}

	public AccountResult RemoveAvatar ()
	{
		lock (gate) {
			if (loadError != null) {
				return AccountResult.Fail (loadError);
			}
			LocalAccountRecord localAccountRecord = FindById (data.ActiveId);
			if (localAccountRecord == null) {
				return AccountResult.Fail ("请先登录账号");
			}
			if (string.IsNullOrEmpty (localAccountRecord.AvatarPngBase64)) {
				return AccountResult.Ok ("当前未设置头像", Profile (localAccountRecord));
			}
			string avatarPngBase = localAccountRecord.AvatarPngBase64;
			localAccountRecord.AvatarPngBase64 = null;
			string text = Save ();
			if (text != null) {
				localAccountRecord.AvatarPngBase64 = avatarPngBase;
				return AccountResult.Fail (text);
			}
			return AccountResult.Ok ("已恢复默认头像", Profile (localAccountRecord));
		}
	}

	private void Load ()
	{
		data = new AccountVaultData {
			Version = 1,
			Accounts = new List<LocalAccountRecord> ()
		};
		if (!File.Exists (path)) {
			return;
		}
		try {
			byte[] bytes = unprotect (File.ReadAllBytes (path));
			AccountVaultData accountVaultData = Store.Json.Deserialize<AccountVaultData> (Encoding.UTF8.GetString (bytes));
			if (accountVaultData == null || accountVaultData.Version != 1 || accountVaultData.Accounts == null || accountVaultData.Accounts.Count > 100) {
				throw new InvalidDataException ("Unsupported account data");
			}
			HashSet<string> hashSet = new HashSet<string> (StringComparer.Ordinal);
			foreach (LocalAccountRecord account in accountVaultData.Accounts) {
				string normalized;
				if (account == null || string.IsNullOrWhiteSpace (account.Id) || !hashSet.Add (account.Id) || ValidateEmail (account.Email, out normalized) != null || normalized != account.Email || string.IsNullOrWhiteSpace (account.DisplayName) || account.DisplayName.Length > 40 || account.Iterations < 10000 || account.Iterations > 1000000) {
					throw new InvalidDataException ("Invalid account record");
				}
				if (!string.IsNullOrEmpty (account.AvatarPngBase64)) {
					if (account.AvatarPngBase64.Length > 700000) {
						throw new InvalidDataException ("Invalid account avatar");
					}
					byte[] png = Convert.FromBase64String (account.AvatarPngBase64);
					if (ValidateAvatar (png) != null) {
						throw new InvalidDataException ("Invalid account avatar");
					}
				}
				byte[] array = Convert.FromBase64String (account.Salt);
				byte[] array2 = Convert.FromBase64String (account.PasswordHash);
				if (array.Length < 16 || array.Length > 64 || array2.Length != 32) {
					throw new InvalidDataException ("Invalid account credential");
				}
			}
			if (!string.IsNullOrEmpty (accountVaultData.ActiveId) && !hashSet.Contains (accountVaultData.ActiveId)) {
				throw new InvalidDataException ("Invalid active account");
			}
			data = accountVaultData;
		} catch {
			loadError = "本机账号数据无法读取。请勿覆盖该文件，可重新安装或联系维护者处理。";
		}
	}

	private string Save ()
	{
		string sourceFileName = path + "." + Guid.NewGuid ().ToString ("N") + ".tmp";
		try {
			string directoryName = System.IO.Path.GetDirectoryName (path);
			if (!string.IsNullOrEmpty (directoryName)) {
				Directory.CreateDirectory (directoryName);
			}
			byte[] bytes = Encoding.UTF8.GetBytes (Store.Json.Serialize (data));
			File.WriteAllBytes (sourceFileName, protect (bytes));
			if (File.Exists (path)) {
				File.Replace (sourceFileName, path, null);
			} else {
				File.Move (sourceFileName, path);
			}
			return null;
		} catch (Exception ex) {
			return "无法保存账号数据：" + ex.Message;
		} finally {
			try {
				if (File.Exists (sourceFileName)) {
					File.Delete (sourceFileName);
				}
			} catch {
			}
		}
	}

	private LocalAccountRecord FindById (string id)
	{
		if (!string.IsNullOrEmpty (id)) {
			return data.Accounts.FirstOrDefault ((LocalAccountRecord x) => x.Id == id);
		}
		return null;
	}

	private static AccountProfile Profile (LocalAccountRecord value)
	{
		if (value != null) {
			AccountProfile accountProfile = new AccountProfile ();
			accountProfile.Id = value.Id;
			accountProfile.Email = value.Email;
			accountProfile.DisplayName = value.DisplayName;
			accountProfile.AvatarPngBase64 = value.AvatarPngBase64;
			accountProfile.CreatedAt = value.CreatedAt;
			accountProfile.LastLoginAt = value.LastLoginAt;
			return accountProfile;
		}
		return null;
	}

	private static string ValidateAvatar (byte[] png)
	{
		if (png == null || png.Length < 24) {
			return "请选择有效的 PNG、JPG、JPEG 或 BMP 图片";
		}
		if (png.Length > 524288) {
			return "头像处理后仍超过 512 KB，请选择更小的图片";
		}
		byte[] array = new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 };
		for (int i = 0; i < array.Length; i++) {
			if (png [i] != array [i]) {
				return "头像必须转换为 PNG 格式";
			}
		}
		return null;
	}

	private static string ValidateEmail (string value, out string normalized)
	{
		normalized = null;
		string text = (value ?? "").Trim ();
		if (text.Length < 3 || text.Length > 254) {
			return "请输入有效的邮箱地址";
		}
		try {
			MailAddress mailAddress = new MailAddress (text);
			if (!string.Equals (mailAddress.Address, text, StringComparison.OrdinalIgnoreCase)) {
				return "请输入有效的邮箱地址";
			}
			normalized = mailAddress.Address.ToLowerInvariant ();
			return null;
		} catch {
			return "请输入有效的邮箱地址";
		}
	}

	private static string ValidatePassword (string value)
	{
		if (string.IsNullOrEmpty (value) || value.Length < 10) {
			return "密码至少需要 10 位";
		}
		if (value.Length > 256) {
			return "密码不能超过 256 位";
		}
		if (!value.Any (char.IsLetter) || !value.Any (char.IsDigit)) {
			return "密码需同时包含字母和数字";
		}
		return null;
	}

	private static string DefaultName (string email)
	{
		string text = email.Split ('@') [0].Trim ();
		if (!string.IsNullOrEmpty (text)) {
			if (text.Length <= 40) {
				return text;
			}
			return text.Substring (0, 40);
		}
		return "Yike 用户";
	}

	private static byte[] Hash (string password, byte[] salt, int iterations)
	{
		using (Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes (password, salt, iterations, HashAlgorithmName.SHA256)) {
			return rfc2898DeriveBytes.GetBytes (32);
		}
	}

	private static bool PasswordMatches (LocalAccountRecord account, string password)
	{
		try {
			byte[] array = Convert.FromBase64String (account.PasswordHash);
			byte[] array2 = Hash (password, Convert.FromBase64String (account.Salt), account.Iterations);
			if (array.Length != array2.Length) {
				return false;
			}
			int num = 0;
			for (int i = 0; i < array.Length; i++) {
				num |= array [i] ^ array2 [i];
			}
			return num == 0;
		} catch {
			return false;
		}
	}

	private static byte[] ProtectForCurrentUser (byte[] value)
	{
		return ProtectedData.Protect (value, Entropy, DataProtectionScope.CurrentUser);
	}

	private static byte[] UnprotectForCurrentUser (byte[] value)
	{
		return ProtectedData.Unprotect (value, Entropy, DataProtectionScope.CurrentUser);
	}
}

}
