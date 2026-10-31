using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace FnShare
{
    // 应用图标：优先使用嵌入的 app.ico；加载失败时退回系统默认图标。
    // 嵌入资源名 = 根命名空间 + 文件名：FnShare.app.ico（见 csproj 的 EmbeddedResource）。
    // Icon 实例依赖的流保持打开（静态持有），避免流关闭后图标失效。
    public static class AppIcon
    {
        private static Icon _cached;
        private static MemoryStream _iconStream;

        public static Icon Get()
        {
            if (_cached != null) return _cached;
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("FnShare.app.ico"))
                {
                    if (s != null)
                    {
                        _iconStream = new MemoryStream();
                        s.CopyTo(_iconStream);
                        _iconStream.Position = 0;
                        _cached = new Icon(_iconStream);
                    }
                }
            }
            catch
            {
            }
            if (_cached == null) _cached = SystemIcons.Application;
            return _cached;
        }
    }
}
