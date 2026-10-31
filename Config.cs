using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FnShare
{
    public class SyncMapping
    {
        public string LocalDir { get; set; } = "";
        public string RemoteRoot { get; set; } = "";

        public override string ToString()
        {
            return LocalDir + "  =>  " + RemoteRoot;
        }
    }

    public class AppConfig
    {
        public string NasName { get; set; } = "";
        public string InternalBase { get; set; } = "";
        public string ExternalBase { get; set; } = "";
        public string ShareDomainOverride { get; set; } = "";
        public string Username { get; set; } = "";

        // DPAPI 加密后的 NAS 账号密码（base64），空表示未保存
        public string PasswordProtected { get; set; } = "";
        public bool SavePassword { get; set; } = false;
        public bool IgnoreTlsErrors { get; set; } = false;
        public int DefaultDays { get; set; } = 7;
        public int DefaultMaxAccess { get; set; } = 0;
        public List<SyncMapping> Mappings { get; set; } = new List<SyncMapping>();

        // 会话内密码（不保存时用；进程退出即丢），不参与序列化
        [JsonIgnore]
        public string SessionPassword { get; set; } = "";

        public bool IsNasConfigured()
        {
            return !string.IsNullOrWhiteSpace(InternalBase) && !string.IsNullOrWhiteSpace(Username);
        }

        // 优先取已保存的密码，否则取会话内密码；都没有则返回空
        public string ResolvePassword()
        {
            if (!string.IsNullOrEmpty(PasswordProtected))
            {
                string p = PasswordStore.Unprotect(PasswordProtected);
                if (!string.IsNullOrEmpty(p)) return p;
            }
            return SessionPassword ?? "";
        }

        public void SetPassword(string password, bool save)
        {
            SessionPassword = password ?? "";
            SavePassword = save;
            PasswordProtected = save ? PasswordStore.Protect(password ?? "") : "";
        }
    }

    // DPAPI（CurrentUser）：仅本机当前用户可解。
    // 任何异常（包括非 Windows 平台）均视为不可用并返回空，
    // 调用方不得因此改存明文。
    public static class PasswordStore
    {
        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            try
            {
                byte[] data = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(data);
            }
            catch
            {
                return "";
            }
        }

        public static string Unprotect(string blob)
        {
            if (string.IsNullOrEmpty(blob)) return "";
            try
            {
                byte[] data = ProtectedData.Unprotect(
                    Convert.FromBase64String(blob), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return "";
            }
        }
    }

    public static class ConfigStore
    {
        public static string Dir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FnShare");
            }
        }

        public static string ConfigPath => Path.Combine(Dir, "config.json");
        public static string SharesPath => Path.Combine(Dir, "shares.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    AppConfig cfg = JsonSerializer.Deserialize<AppConfig>(
                        File.ReadAllText(ConfigPath), JsonOptions);
                    if (cfg != null)
                    {
                        if (cfg.Mappings == null) cfg.Mappings = new List<SyncMapping>();
                        return cfg;
                    }
                }
            }
            catch
            {
                // 配置损坏时返回空配置，不抛错
            }
            return new AppConfig();
        }

        public static void Save(AppConfig cfg)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, JsonOptions));
        }

        public static Dictionary<string, LocalExtra> LoadExtras()
        {
            try
            {
                if (File.Exists(SharesPath))
                {
                    var d = JsonSerializer.Deserialize<Dictionary<string, LocalExtra>>(
                        File.ReadAllText(SharesPath), JsonOptions);
                    return d ?? new Dictionary<string, LocalExtra>();
                }
            }
            catch
            {
            }
            return new Dictionary<string, LocalExtra>();
        }

        public static void SaveExtras(Dictionary<string, LocalExtra> extras)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(SharesPath, JsonSerializer.Serialize(extras, JsonOptions));
        }
    }
}
