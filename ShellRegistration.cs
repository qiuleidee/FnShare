using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FnShare
{
    // 右键菜单：COM 扩展（IContextMenu），运行期实时判断映射范围。
    //  - AppliesTo 方案已放弃：精确＋通配、contains 两轮在 Windows 上均未生效
    //    （子目录不显示、二级菜单打不开），不再用静态注册表动词做显隐过滤。
    //  - 扩展类为 FnShare.ShellExtension.ShellExtension（GUID 见下），
    //    由 FnShare.ShellExtension.comhost.dll 承载，随主 exe 同目录发行。
    //  - 仅 HKCU，无需管理员权限。Windows 10 / 11 经典菜单均可显示；
    //    Win11 新菜单中位于“显示更多选项”内。Win11 原生新菜单为二期，不做。
    //  - 不添加任何“添加映射”引导流程：扩展只在全部选中路径都在映射内时显示菜单，
    //    否则不显示、不报错；主程序侧的 PathMapper 拒绝逻辑仍保留作最后一道校验。
    public static class ShellRegistration
    {
        // 必须与扩展类上的 [Guid] 完全一致
        public const string ExtensionClsid = "22999126-7190-4616-B7B5-46E53435D0D4";
        private const string ExtensionClsidBraced = "{22999126-7190-4616-B7B5-46E53435D0D4}";
        private const string ComhostFileName = "FnShare.ShellExtension.comhost.dll";
        private const string HandlerName = "FnShare";

        // 旧版静态动词键（注册与注销时一并清理，避免残留菜单）。
        // 以下路径均为相对 HKCU 根的完整路径（Software\Classes\...）。
        private static readonly string[] LegacyVerbPaths = new string[]
        {
            @"Software\Classes\*\shell\FnShare.Create",
            @"Software\Classes\Directory\shell\FnShare.Create",
            @"Software\Classes\*\shell\FnShare.Records",
            @"Software\Classes\Directory\shell\FnShare.Records",
            @"Software\Classes\Directory\Background\shell\FnShare.Records",
            @"Software\Classes\*\shell\FnShare.Config",
            @"Software\Classes\Directory\shell\FnShare.Config",
            @"Software\Classes\*\shell\FnShare.OpenNas",
            @"Software\Classes\Directory\shell\FnShare.OpenNas",
        };

        // 旧版级联父级键（同上，注册与注销时一并清理）
        private static readonly string[] ParentPaths = new string[]
        {
            @"Software\Classes\*\shell\FnShare",
            @"Software\Classes\Directory\shell\FnShare",
        };

        private static readonly string[] ShellexPaths = new string[]
        {
            @"Software\Classes\*\shellex\ContextMenuHandlers\" + HandlerName,
            @"Software\Classes\Directory\shellex\ContextMenuHandlers\" + HandlerName,
        };

        private static string ExePath => Application.ExecutablePath;
        private static string ExeDir => Path.GetDirectoryName(ExePath) ?? "";

        public static void Register()
        {
            // 先清旧静态键（扁平＋级联父级），避免残留菜单
            foreach (string p in LegacyVerbPaths) DeleteKey(p);
            foreach (string p in ParentPaths) DeleteKey(p);

            string comhost = Path.Combine(ExeDir, ComhostFileName);
            if (!File.Exists(comhost))
                throw new InvalidOperationException(
                    "未找到扩展组件 " + ComhostFileName + "，请将扩展包解压到 FnShare.exe 所在目录后再注册。");

            using (RegistryKey clsidKey = Registry.CurrentUser.CreateSubKey(
                @"Software\Classes\CLSID\" + ExtensionClsidBraced))
            {
                using (RegistryKey inproc = clsidKey.CreateSubKey("InprocServer32"))
                {
                    inproc.SetValue("", comhost);
                    inproc.SetValue("ThreadingModel", "Apartment");
                }
            }

            foreach (string p in ShellexPaths)
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(p))
                {
                    k.SetValue("", ExtensionClsidBraced);
                }
            }

            // 写入主程序实际路径，供扩展定位：exe 文件名可能带版本号，
            // 扩展不能写死 "FnShare.exe"，须以注册时记录的实际路径为准。
            using (RegistryKey appKey = Registry.CurrentUser.CreateSubKey(@"Software\FnShare"))
            {
                appKey.SetValue("ExePath", ExePath);
            }
        }

        public static void Unregister()
        {
            DeleteKey(@"Software\Classes\CLSID\" + ExtensionClsidBraced);
            DeleteKey(@"Software\FnShare");
            foreach (string p in ShellexPaths) DeleteKey(p);
            foreach (string p in ParentPaths) DeleteKey(p);
            foreach (string p in LegacyVerbPaths) DeleteKey(p);
        }

        public static bool IsRegistered()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Software\Classes\*\shellex\ContextMenuHandlers\" + HandlerName))
            {
                return key != null;
            }
        }

        private static void DeleteKey(string subKeyPath)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(subKeyPath, false);
            }
            catch
            {
            }
        }

        public static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}
