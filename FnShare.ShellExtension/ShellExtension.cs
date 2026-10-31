using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

[assembly: ComVisible(false)]

namespace FnShare.ShellExtension
{
    // FnShare 右键扩展：同时实现 IShellExtInit 与 IContextMenu。
    // 行为总则：运行期实时判断映射范围——只有当全部选中路径都在已保存的
    // 映射目录内时才插入菜单，否则不显示、不报错；任何异常都不抛进资源管理器。
    // 本类绝不显示界面：点击菜单项只启动主程序（FnShare.exe），界面由主程序进程展示。
    // 注意：本程序集由 FnShare.ShellExtension.comhost.dll 承载，随主 exe 同目录发行；
    // 主程序 ShellRegistration.cs 里写注册表时用的 CLSID 必须与此处 [Guid] 完全一致。
    [ComVisible(true)]
    [Guid("22999126-7190-4616-B7B5-46E53435D0D4")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("FnShare.ShellExtension")]
    public sealed class ShellExtension : IShellExtInit, IContextMenu
    {
        private const int CF_HDROP = 15;
        private const uint DVASPECT_CONTENT = 1;
        private const uint TYMED_HGLOBAL = 1;
        private const uint CMF_DEFAULTONLY = 0x00000001;
        private const uint MF_STRING = 0x00000000;
        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_POPUP = 0x00000010;
        private const uint MF_BYPOSITION = 0x00000400;
        private const uint GCS_HELPTEXTA = 1;
        private const uint GCS_HELPTEXTW = 5;
        private const int S_OK = 0;
        private const int E_FAIL = unchecked((int)0x80004005);
        private const int E_INVALIDARG = unchecked((int)0x80070057);

        // 子项顺序固定：创建分享链接第一个
        private static readonly string[] ItemTexts = new string[]
        {
            "创建分享链接",
            "查看分享记录",
            "分享设置",
            "打开 NAS 文件管理",
        };

        private static readonly string[] HelpTexts = new string[]
        {
            "为选中的文件或文件夹创建飞牛分享链接",
            "查看飞牛分享记录",
            "打开飞牛分享设置",
            "打开 NAS 文件管理",
        };

        private List<string> _selectedFiles = new List<string>();

        // 本轮菜单的位图句柄：菜单显示期间绝不销毁；下一轮 QueryContextMenu
        // 开始时（上一轮菜单已消失）或实例析构时统一释放。
        private readonly List<IntPtr> _menuBitmaps = new List<IntPtr>();

        // 菜单图标资源名（扩展工程内嵌的 16×16 PNG，顺序与 ItemTexts 一致）。
        private static readonly string[] MenuIconResources = new string[]
        {
            "FnShare.ShellExtension.menu-share.png",
            "FnShare.ShellExtension.menu-list.png",
            "FnShare.ShellExtension.menu-gear.png",
            "FnShare.ShellExtension.menu-folder.png",
        };

        // 加载全部菜单位图；任一失败返回 null（调用方整单放弃，不出无图标菜单）。
        private static List<IntPtr> LoadMenuBitmaps()
        {
            var list = new List<IntPtr>();
            try
            {
                foreach (string name in MenuIconResources)
                {
                    IntPtr hbm = LoadMenuBitmap(name);
                    if (hbm == IntPtr.Zero)
                    {
                        ReleaseBitmapList(list);
                        return null;
                    }
                    list.Add(hbm);
                }
                return list;
            }
            catch
            {
                ReleaseBitmapList(list);
                return null;
            }
        }

        // 内嵌 PNG → Bitmap → HBITMAP（GetHbitmap 复制出新的 GDI 位图，
        // 因此 Bitmap 用完即可释放，不影响已生成的句柄）。
        private static IntPtr LoadMenuBitmap(string resourceName)
        {
            try
            {
                using (Stream s = typeof(ShellExtension).Assembly.GetManifestResourceStream(resourceName))
                {
                    if (s == null) return IntPtr.Zero;
                    using (Bitmap bmp = new Bitmap(s))
                    {
                        return bmp.GetHbitmap();
                    }
                }
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        private static void ReleaseBitmapList(List<IntPtr> list)
        {
            if (list == null) return;
            foreach (IntPtr h in list)
            {
                try { if (h != IntPtr.Zero) NativeMethods.DeleteObject(h); } catch { }
            }
            list.Clear();
        }

        // 释放上一轮菜单的位图（下一轮开始时调用，此时上一轮菜单已消失）。
        private void ReleaseMenuBitmaps()
        {
            ReleaseBitmapList(_menuBitmaps);
        }

        // 实例析构时兜底释放最后一批位图（宿主丢弃实例后不再有下一轮调用）。
        ~ShellExtension()
        {
            try { ReleaseMenuBitmaps(); } catch { }
        }

        // ---------- IShellExtInit ----------

        public void Initialize(IntPtr pidlFolder, IDataObject pDataObj, IntPtr hKeyProgID)
        {
            _selectedFiles = new List<string>();
            try
            {
                if (pDataObj == null) return;
                var format = new FORMATETC
                {
                    cfFormat = CF_HDROP,
                    ptd = IntPtr.Zero,
                    dwAspect = DVASPECT_CONTENT,
                    lindex = -1,
                    tymed = TYMED_HGLOBAL,
                };
                STGMEDIUM medium;
                try
                {
                    pDataObj.GetData(ref format, out medium);
                }
                catch
                {
                    return; // 取不到 CF_HDROP：无选中文件可处理，直接不显示
                }
                try
                {
                    if (medium.tymed != TYMED_HGLOBAL || medium.unionmember == IntPtr.Zero) return;
                    IntPtr hDrop = medium.unionmember;
                    uint count = NativeMethods.DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
                    for (uint i = 0; i < count; i++)
                    {
                        int len = (int)NativeMethods.DragQueryFile(hDrop, i, null, 0);
                        if (len <= 0) continue;
                        var sb = new StringBuilder(len + 1);
                        if (NativeMethods.DragQueryFile(hDrop, i, sb, sb.Capacity) > 0)
                            _selectedFiles.Add(sb.ToString());
                    }
                    // 注意：HDROP 的释放只能由 ReleaseStgMedium 做（见 finally）。
                    // 绝不能再调 DragFinish：它也会释放同一块 HDROP 内存，
                    // 两次释放会破坏堆，导致资源管理器崩溃——这是 v4 每次右键
                    // 都崩溃的原因。DragFinish 只用于 WM_DROPFILES 场景。
                }
                finally
                {
                    try { NativeMethods.ReleaseStgMedium(ref medium); } catch { }
                }
            }
            catch
            {
                // 任何异常都不抛进资源管理器：置空选中列表，菜单自然不显示
                _selectedFiles = new List<string>();
            }
        }

        // ---------- IContextMenu ----------

        public int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags)
        {
            try
            {
                // 上一轮菜单的位图在本轮开始时释放：上一轮菜单显示已经结束，
                // 此时销毁是安全的；绝不在菜单显示期间销毁。
                ReleaseMenuBitmaps();
                if ((uFlags & CMF_DEFAULTONLY) != 0) return 0; // 仅默认项时不插菜单
                if (hmenu == IntPtr.Zero) return 0;
                if (_selectedFiles == null || _selectedFiles.Count == 0) return 0;
                // 4 个连续 id，idCmdLast 必须容纳；不够则不插入
                if (idCmdLast < idCmdFirst || idCmdLast - idCmdFirst < 3) return 0;
                List<string> mappings = LoadMappingDirs();
                if (mappings.Count == 0) return 0; // 无映射：处处不显示
                foreach (string path in _selectedFiles)
                {
                    if (!IsInMappings(path, mappings)) return 0; // 全部在映射内才显示
                }
                IntPtr hSub = NativeMethods.CreatePopupMenu();
                if (hSub == IntPtr.Zero) return 0;
                List<IntPtr> bitmaps = null;
                bool inserted = false;
                try
                {
                    for (int i = 0; i < ItemTexts.Length; i++)
                    {
                        if (!NativeMethods.AppendMenu(hSub, MF_STRING, (UIntPtr)(idCmdFirst + i), ItemTexts[i]))
                        {
                            NativeMethods.DestroyMenu(hSub);
                            return 0;
                        }
                    }
                    // 菜单图标：子项按命令 id、父项按位置设置。
                    // 任一图标失败即整单放弃——没有图标的菜单项不许出现。
                    bitmaps = LoadMenuBitmaps();
                    if (bitmaps == null)
                    {
                        NativeMethods.DestroyMenu(hSub);
                        return 0;
                    }
                    for (int i = 0; i < ItemTexts.Length; i++)
                    {
                        if (!NativeMethods.SetMenuItemBitmaps(hSub, idCmdFirst + (uint)i, MF_BYCOMMAND, bitmaps[i], bitmaps[i]))
                        {
                            NativeMethods.DestroyMenu(hSub);
                            ReleaseBitmapList(bitmaps);
                            return 0;
                        }
                    }
                    if (!NativeMethods.InsertMenu(hmenu, indexMenu, MF_POPUP | MF_BYPOSITION, (UIntPtr)hSub, "使用飞牛分享链接"))
                    {
                        NativeMethods.DestroyMenu(hSub);
                        ReleaseBitmapList(bitmaps);
                        return 0;
                    }
                    inserted = true;
                    if (!NativeMethods.SetMenuItemBitmaps(hmenu, indexMenu, MF_BYPOSITION, bitmaps[0], bitmaps[0]))
                    {
                        // 已插入父菜单的项不能直接 DestroyMenu：先从父菜单摘除，
                        // 再销毁子菜单，否则父菜单里会残留野句柄导致崩溃。
                        NativeMethods.RemoveMenu(hmenu, indexMenu, MF_BYPOSITION);
                        NativeMethods.DestroyMenu(hSub);
                        ReleaseBitmapList(bitmaps);
                        return 0;
                    }
                    // 全部成功：位图交由实例持有，下一轮 QueryContextMenu 开始时
                    // 或实例析构时释放；菜单显示期间绝不销毁。
                    _menuBitmaps.AddRange(bitmaps);
                    bitmaps = null;
                }
                catch
                {
                    try
                    {
                        if (inserted) NativeMethods.RemoveMenu(hmenu, indexMenu, MF_BYPOSITION);
                        NativeMethods.DestroyMenu(hSub);
                    }
                    catch { }
                    if (bitmaps != null) ReleaseBitmapList(bitmaps);
                    return 0;
                }
                return ItemTexts.Length; // HRESULT 的 code 部分 = 占用的 id 数（4）
            }
            catch
            {
                return 0; // 任何异常都不破坏资源管理器菜单：直接不显示
            }
        }

        public int InvokeCommand(IntPtr pici)
        {
            try
            {
                if (pici == IntPtr.Zero) return E_INVALIDARG;
                CMINVOKECOMMANDINFO info = Marshal.PtrToStructure<CMINVOKECOMMANDINFO>(pici);
                long verb = info.lpVerb.ToInt64();
                if ((verb >> 16) != 0) return E_INVALIDARG; // 字符串动词不支持
                int offset = (int)(verb & 0xFFFF);
                if (offset < 0 || offset >= ItemTexts.Length) return E_INVALIDARG;
                string exe = GetMainExePath();
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return E_FAIL;
                switch (offset)
                {
                    case 0:
                        // 每个选中文件各启动一次主程序 --share
                        foreach (string f in _selectedFiles)
                            Launch(exe, "--share \"" + f + "\"");
                        break;
                    case 1:
                        Launch(exe, "");
                        break;
                    case 2:
                        Launch(exe, "--config");
                        break;
                    case 3:
                        Launch(exe, "--open-nas");
                        break;
                }
                return S_OK;
            }
            catch
            {
                return E_FAIL;
            }
        }

        public int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax)
        {
            try
            {
                ulong offset = idCmd.ToUInt64();
                if (offset >= (ulong)HelpTexts.Length) return E_INVALIDARG;
                if (pszName == IntPtr.Zero || cchMax == 0) return E_INVALIDARG;
                string text = HelpTexts[(int)offset];
                if (uType == GCS_HELPTEXTW)
                {
                    // 最多写 cchMax-1 个字符再补终止符，避免截断后无终止符
                    // 导致资源管理器越界读内存。
                    int maxCopy = Math.Max(0, (int)cchMax - 1);
                    int n = Math.Min(text.Length, maxCopy);
                    char[] chars = text.Substring(0, n).ToCharArray();
                    Marshal.Copy(chars, 0, pszName, n);
                    Marshal.WriteInt16(pszName, n * 2, (short)0);
                    return S_OK;
                }
                if (uType == GCS_HELPTEXTA)
                {
                    byte[] bytes = Encoding.Default.GetBytes(text);
                    int maxCopy = Math.Max(0, (int)cchMax - 1);
                    int n = Math.Min(bytes.Length, maxCopy);
                    Marshal.Copy(bytes, 0, pszName, n);
                    Marshal.WriteByte(pszName, n, (byte)0);
                    return S_OK;
                }
                return E_INVALIDARG;
            }
            catch
            {
                return E_INVALIDARG;
            }
        }

        // ---------- 映射判定与配置读取 ----------

        // 与主程序 PathMapper 一致的规则：路径等于映射目录、
        // 或以映射目录加分隔符开头（不区分大小写）
        private static bool IsInMappings(string path, List<string> mappings)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string full = NormalizePath(path);
            if (string.IsNullOrEmpty(full)) return false;
            foreach (string m in mappings)
            {
                string dir = NormalizePath(m);
                if (string.IsNullOrEmpty(dir)) continue;
                if (full.Equals(dir, StringComparison.OrdinalIgnoreCase)) return true;
                if (full.StartsWith(dir + "\\", StringComparison.OrdinalIgnoreCase)) return true;
                if (full.StartsWith(dir + "/", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string NormalizePath(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return "";
            string t = p.Trim().TrimEnd('\\', '/');
            if (t.Length == 0) return "";
            if (t.Length == 2 && t[1] == ':') return t + "\\"; // 根目录如 D:\ 保留尾部反斜杠
            return t;
        }

        // 只读配置里的映射 LocalDir：最小 JSON 解析，不碰密码字段，
        // 不引用主程序的 ConfigStore（扩展运行在资源管理器进程内，必须自包含）。
        // 配置文件：%AppData%\FnShare\config.json，Mappings 数组元素的 LocalDir 字段。
        private static List<string> LoadMappingDirs()
        {
            var result = new List<string>();
            try
            {
                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FnShare", "config.json");
                if (!File.Exists(configPath)) return result;
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(configPath)))
                {
                    JsonElement root = doc.RootElement;
                    if (!TryGetPropertyIgnoreCase(root, "Mappings", out JsonElement mappings)) return result;
                    if (mappings.ValueKind != JsonValueKind.Array) return result;
                    foreach (JsonElement el in mappings.EnumerateArray())
                    {
                        if (el.ValueKind != JsonValueKind.Object) continue;
                        if (!TryGetPropertyIgnoreCase(el, "LocalDir", out JsonElement dirEl)) continue;
                        if (dirEl.ValueKind != JsonValueKind.String) continue;
                        string dir = dirEl.GetString();
                        if (!string.IsNullOrWhiteSpace(dir)) result.Add(dir);
                    }
                }
            }
            catch
            {
                return new List<string>(); // 配置缺失或损坏：不显示菜单，不报错
            }
            return result;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
        {
            foreach (JsonProperty prop in obj.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
            value = default(JsonElement);
            return false;
        }

        private static string GetMainExePath()
        {
            try
            {
                // 优先读注册时记录的主程序实际路径：exe 文件名可能带版本号，
                // 不能写死 "FnShare.exe"，否则改名后点击菜单会找错程序。
                try
                {
                    using (Microsoft.Win32.RegistryKey appKey =
                        Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\FnShare"))
                    {
                        string regExe = appKey?.GetValue("ExePath") as string;
                        if (!string.IsNullOrEmpty(regExe) && File.Exists(regExe))
                            return regExe;
                    }
                }
                catch { }
                string dir = "";
                try { dir = Path.GetDirectoryName(typeof(ShellExtension).Assembly.Location); } catch { }
                if (string.IsNullOrEmpty(dir)) dir = AppContext.BaseDirectory;
                return Path.Combine(dir, "FnShare.exe");
            }
            catch
            {
                return "";
            }
        }

        private static void Launch(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args) { UseShellExecute = true };
            Process.Start(psi);
        }
    }
}
