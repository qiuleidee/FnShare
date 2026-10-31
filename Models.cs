using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FnShare
{
    // 服务器返回的分享文件条目（仅 link.get 返回）
    public class ShareFile
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        [JsonPropertyName("file")]
        public string File { get; set; } = "";

        // 1 = 文件夹，0 = 文件。文件与文件夹的区分只靠它。
        [JsonPropertyName("isDir")]
        public int IsDir { get; set; }
    }

    // 服务器返回的分享记录。字段名与服务器一致。
    // 注意：产品不读取、不展示服务器回显的 passwd 字段（设计文档 8.2），
    // 故本模型不含该字段；创建时展示的密码一律取用户当次输入或本地加密保存。
    public class ShareRecord
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("maxAccess")]
        public int MaxAccess { get; set; }

        [JsonPropertyName("access")]
        public int Access { get; set; }

        // 秒级时间戳；0 = 永久有效
        [JsonPropertyName("validTo")]
        public long ValidTo { get; set; }

        [JsonPropertyName("uid")]
        public int Uid { get; set; }

        // 恒为 1，无区分意义
        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("accessStatus")]
        public string AccessStatus { get; set; } = "";

        // 毫秒级时间戳（与 validTo 的秒级单位不同）
        [JsonPropertyName("createdTime")]
        public long CreatedTime { get; set; }

        [JsonPropertyName("files")]
        public List<ShareFile> Files { get; set; } = new List<ShareFile>();
    }

    // 本地扩展信息：仅存本机，不发送给 NAS
    public class LocalExtra
    {
        public string LocalPath { get; set; } = "";
        public string TypeLabel { get; set; } = "外部";
        public string Remark { get; set; } = "";
        public bool PasswordEnabled { get; set; }
        // DPAPI 加密后的分享密码（仅本机当前用户可解），空表示未保存
        public string PasswordProtected { get; set; } = "";
        public DateTime SavedAt { get; set; } = DateTime.Now;
    }

    public enum ShareStatus
    {
        Valid,
        Expired,
        LimitReached,
    }

    public static class ShareLinkBuilder
    {
        // 链接基地址默认用 link.default 返回的 defaultLink；
        // 用户配置的外链域名仅作手动覆盖，空则不覆盖。
        public static string Build(string defaultLink, string domainOverride, string id)
        {
            string baseUrl = !string.IsNullOrWhiteSpace(domainOverride)
                ? domainOverride.Trim().TrimEnd('/')
                : (defaultLink ?? "").Trim().TrimEnd('/');
            return baseUrl + "/s/" + id;
        }
    }

    public static class ShareRules
    {
        // 状态本地判定，不依赖恒为 valid 的 status 字段
        public static ShareStatus Judge(ShareRecord r)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (r.ValidTo != 0 && r.ValidTo < now) return ShareStatus.Expired;
            if (r.MaxAccess != 0 && r.Access >= r.MaxAccess) return ShareStatus.LimitReached;
            return ShareStatus.Valid;
        }

        public static string StatusText(ShareStatus s)
        {
            switch (s)
            {
                case ShareStatus.Expired: return "已过期";
                case ShareStatus.LimitReached: return "已达次数上限";
                default: return "有效";
            }
        }

        // validTo：秒级时间戳，0 = 永久有效
        public static string ValidToText(long validTo)
        {
            if (validTo == 0) return "永久有效";
            return DateTimeOffset.FromUnixTimeSeconds(validTo).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
        }

        // createdTime：毫秒级时间戳（与 validTo 单位不同）
        public static string CreatedTimeText(long createdMs)
        {
            if (createdMs <= 0) return "-";
            return DateTimeOffset.FromUnixTimeMilliseconds(createdMs).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
        }

        public static string AccessText(int access, int maxAccess)
        {
            return maxAccess == 0 ? access + " / 不限" : access + " / " + maxAccess;
        }

        public static string DisplayPath(ShareRecord r)
        {
            if (r.Files == null || r.Files.Count == 0) return r.Name;
            ShareFile f = r.Files[0];
            string p = (f.Path ?? "").TrimEnd('/');
            return string.IsNullOrEmpty(p) ? f.File : p + "/" + f.File;
        }

        public static string KindText(ShareRecord r)
        {
            // type 恒为 1 无区分意义；只靠 files[].isDir
            if (r.Files != null && r.Files.Count > 0) return r.Files[0].IsDir == 1 ? "文件夹" : "文件";
            return "文件";
        }
    }
}
