using System;
using System.Windows.Forms;

namespace FnShare
{
    internal static class Program
    {
        // 用法：
        //   FnShare.exe                    打开主窗口（分享记录＋配置）
        //   FnShare.exe --share "<路径>"   直接打开创建窗口并预填对象
        //   FnShare.exe --config           打开主窗口并定位到配置页
        //   FnShare.exe --open-nas         在浏览器打开 NAS 文件管理
        [STAThread]
        private static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();

            AppConfig config = ConfigStore.Load();
            string sharePath = null;
            bool openConfig = false;
            bool openNas = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--share" && i + 1 < args.Length) sharePath = args[++i];
                else if (args[i] == "--config") openConfig = true;
                else if (args[i] == "--open-nas") openNas = true;
            }

            if (openNas)
            {
                string url = !string.IsNullOrWhiteSpace(config.ExternalBase)
                    ? config.ExternalBase : config.InternalBase;
                if (!string.IsNullOrWhiteSpace(url))
                {
                    ShellRegistration.OpenUrl(url);
                    return;
                }
                Application.Run(new MainForm(config, selectConfigTab: true,
                    notice: "请先配置 NAS 地址，再使用“打开 NAS 文件管理”。"));
                return;
            }

            if (sharePath != null)
            {
                if (!config.IsNasConfigured())
                {
                    Application.Run(new MainForm(config, selectConfigTab: true,
                        notice: "请先完成 NAS 连接配置与同步目录映射，再创建分享。"));
                    return;
                }
                Application.Run(new CreateShareForm(config, sharePath));
                return;
            }

            Application.Run(new MainForm(config, selectConfigTab: openConfig));
        }
    }
}
