using System;
using System.Collections.Generic;
using System.IO;

namespace FnShare
{
    public class MapResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; } = "";
        public string NasPath { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsDir { get; set; }
    }

    // 本地同步目录 → NAS 远程根的多组映射，最长匹配，目录外一律拒绝
    public static class PathMapper
    {
        public static MapResult Map(string localPath, List<SyncMapping> mappings)
        {
            var r = new MapResult();
            if (string.IsNullOrWhiteSpace(localPath))
            {
                r.Error = "未选择分享对象。";
                return r;
            }

            string full;
            try
            {
                full = Path.GetFullPath(localPath);
            }
            catch
            {
                r.Error = "路径无效：" + localPath;
                return r;
            }

            bool isDir = Directory.Exists(full);
            bool isFile = File.Exists(full);
            if (!isDir && !isFile)
            {
                r.Error = "文件或文件夹不存在：" + full;
                return r;
            }
            r.IsDir = isDir;

            SyncMapping best = null;
            string bestDir = "";
            foreach (SyncMapping m in mappings ?? new List<SyncMapping>())
            {
                if (string.IsNullOrWhiteSpace(m.LocalDir)) continue;
                string dir;
                try
                {
                    dir = Path.GetFullPath(m.LocalDir)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
                catch
                {
                    continue;
                }
                if (full.Equals(dir, StringComparison.OrdinalIgnoreCase)
                    || full.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || full.StartsWith(dir + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    if (dir.Length > bestDir.Length)
                    {
                        best = m;
                        bestDir = dir;
                    }
                }
            }

            if (best == null)
            {
                r.Error = "该对象不在已配置的同步目录内，无法分享。请先在配置中添加对应的同步目录映射。";
                return r;
            }

            string remoteRoot = (best.RemoteRoot ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(remoteRoot))
            {
                r.Error = "该映射的远程根目录为空，请在配置中填写。";
                return r;
            }

            string rel = full.Substring(bestDir.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string relSlash = rel.Replace('\\', '/');
            r.NasPath = string.IsNullOrEmpty(relSlash) ? remoteRoot : remoteRoot + "/" + relSlash;
            string trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            r.Name = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(r.Name)) r.Name = full;
            r.Ok = true;
            return r;
        }
    }
}
