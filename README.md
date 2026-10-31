# FnShare - 飞牛私有云（fnOS）Windows 外链分享助手与右键扩展

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0--windows-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D6?logo=windows)](https://microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**FnShare** 是一款专为**飞牛私有云（fnOS）**打造的 Windows 桌面辅助工具与资源管理器（Explorer）右键 COM 扩展。

通过建立本地同步目录与 NAS 远程路径的自动映射，您可以在 Windows 本地文件夹中**直接右键任意文件或目录**，一秒生成 NAS 外链分享，免去反复打开浏览器登录 Web 端后台查找和分享的繁琐操作。

---

## 🌟 核心特性

- 🖱️ **原生右键集成（COM 扩展）**：深度集成至 Windows 资源管理器经典与更多选项菜单，带独立高清菜单图标，无需管理员权限即可注册。
- 🎯 **智能映射过滤**：仅在选中的文件或目录属于已配置的 NAS 同步路径时才动态展示右键菜单，不在映射内静默隐藏，不破坏系统的右键菜单体验。
- ⚡ **一键快捷分享**：支持在弹窗中快速配置有效天数（7天/永久/自定义）、访问次数上限、访问提取密码与本地备注。
- 📋 **历史记录与链接管理**：内置现代卡片式主界面，集中管理所有已生成的分享链接，支持一键复制链接、复制密码、查看详情及打开链接。
- 🔒 **工业级安全设计**：
  - **凭据保护**：NAS 账号密码与分享提取码均使用 Windows 原生 **DPAPI**（`DataProtectionScope.CurrentUser`）加密存储，仅当前登录用户可解密，绝不明文存储。
  - **安全通信**：完整移植飞牛 fnOS 官方 WebSocket 通信协议，涵盖明文 RSA 公钥交换、AES-256-CBC 封包登录、HMAC-SHA256 签名校验以及递增 `reqid` 机制。
- 🎨 **现代 Fluent 2 视觉质感**：基于 8px 栅格规范、微圆角卡片分层、呼吸感排版与状态彩色药丸 Badge 设计，告别老旧 WinForms 界面生硬感。

---

## 🖥️ 界面预览

* **主界面 - 分享记录管理**：支持查看所有分享对象、文件/文件夹类型、访问统计、剩余时限与状态药丸，提供快捷操作工具栏。
* **主界面 - 系统配置页**：支持 NAS 内外网地址、外链域名覆盖、凭据记忆、自签名证书忽略、多组本地到 NAS 路径映射及一键注册/注销右键菜单。
* **创建分享弹窗**：支持预填路径、多周期选择（7天/永久/自定义）与密码/备注设定。
* **分享结果弹窗**：生成成功后支持一键复制链接、复制提取码或一键复制包含有效期的完整卡片信息。

---

## 🛠️ 技术架构

```
Windows 资源管理器 (Explorer)
       │
       ▼ (右键选中本地同步文件/文件夹)
FnShare.ShellExtension (COM 组件: IShellExtInit + IContextMenu)
       │  读取本地 config.json 校验路径映射
       ├─ 在映射范围内 ──> 展开级联菜单 (创建分享 / 查看记录 / 设置 / 打开 NAS)
       │                    └─ 唤起主程序: FnShare.exe --share "<本地路径>"
       └─ 不在映射范围内 ──> 静默不加载 (零干扰)
```

| 模块 | 说明 |
| :--- | :--- |
| **FnShare.exe** | 客户端主进程（.NET 8 WinForms），负责所有 UI 交互、配置管理、本地 DPAPI 加密与业务调度 |
| **FnShare.ShellExtension** | COM 宿主扩展（`comhost.dll`），负责 Windows 资源管理器右键菜单的注入与动态路径拦截 |
| **FnClient** | 飞牛私有云 fnOS WebSocket 协议层，实现安全握手、登录及 `appcgi.sharesvr.share.link.*` 接口交互 |
| **PathMapper** | 智能路径映射计算器，采用最长前缀匹配算法将本地文件路径转换为对应的 NAS 相对与绝对路径 |

---

## 📦 环境要求

- **操作系统**：Windows 10 / Windows 11 (x64)
- **运行环境**：[.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows x64)

---

## 🚀 快速开始

### 1. 编译构建
```bash
# 克隆仓库
git clone https://github.com/qiuleidee/FnShare.git
cd FnShare

# 编译整个解决方案
dotnet build FnShare.sln -c Release
```

### 2. 配置与使用
1. 打开编译产物目录中的 `FnShare.exe`。
2. 进入 **系统配置** 页面：
   - 填写 **NAS 内网地址**（例如 `http://192.168.1.100:5666`）与 **NAS 用户名/密码**。
   - 若配置了外网访问或 DDNS，可填入 **外网地址** 或 **外链域名覆盖**。
   - 在 **同步目录映射** 中点击“添加映射”，选择您在本地由飞牛客户端或其他工具同步的文件夹，并填写对应的 NAS 远程根路径（以 `/` 开头）。
   - 点击 **注册右键菜单**。
3. 在本地任意已映射的同步文件夹内，**右键任意文件或目录**，点击 **飞牛分享 ➜ 创建分享链接** 即可极速创建外链！

---

## ❓ 常见问题 (FAQ)

**Q：为什么右键文件时没有出现“飞牛分享”菜单？**  
A：
1. 请确保已在“系统配置”中成功点击了“注册右键菜单”；
2. 检查所选文件是否在您配置的“同步目录映射”路径之内。FnShare 设计为仅在已映射的目录内展示，未映射路径自动隐藏，防止污染日常右键菜单；
3. Windows 11 用户若处于新版精简右键菜单中，请在菜单底部点击“显示更多选项”即可看到。

**Q：NAS 账号密码保存在本地安全吗？**  
A：非常安全。密码通过 Windows 操作系统提供的 DPAPI（数据保护 API）加密，密钥与当前 Windows 用户身份深度绑定，其他登录用户或拷贝配置文件均无法解密。

---

## 📄 开源协议

本项目采用 [MIT License](LICENSE) 开源协议。

