using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FnShare
{
    // 设计规范 Tokens（全程序统一）：现代 Fluent 2 / 现代极简风格
    // 纯视觉层重构，不影响任何业务逻辑、通讯协议或存储结构。
    public static class Theme
    {
        // ---- 核心色彩体系 ----
        public static readonly Color Primary = Color.FromArgb(0x00, 0x66, 0xFF);          // 品牌主色：现代明快蓝
        public static readonly Color PrimaryDark = Color.FromArgb(0x00, 0x52, 0xCC);      // 主色按压/悬浮深蓝
        public static readonly Color PrimarySubtle = Color.FromArgb(0xEB, 0xF3, 0xFF);    // 主色浅底：激活胶囊/选中行
        public static readonly Color PrimaryBorder = Color.FromArgb(0xBF, 0xDB, 0xFE);    // 激活边框

        public static readonly Color Header = Color.FromArgb(0xFF, 0xFF, 0xFF);         // 顶栏白底（与现代界面融合）
        public static readonly Color Page = Color.FromArgb(0xF8, 0xFA, 0xFC);           // 页面统一浅灰底 (Slate 50)
        public static readonly Color Card = Color.FromArgb(0xFF, 0xFF, 0xFF);           // 白色卡片底色
        public static readonly Color Border = Color.FromArgb(0xE2, 0xE8, 0xF0);         // 细腻分割边框 (Slate 200)
        public static readonly Color BorderDark = Color.FromArgb(0xCB, 0xD5, 0xE1);     // 输入框稍深边框 (Slate 300)

        public static readonly Color Ink = Color.FromArgb(0x1E, 0x29, 0x3B);            // 主要文字色：深炭墨 (Slate 800)
        public static readonly Color Secondary = Color.FromArgb(0x64, 0x74, 0x8B);      // 次要/提示文字色 (Slate 500)
        public static readonly Color Muted = Color.FromArgb(0x94, 0xA3, 0xB8);          // 占位/禁用文字色 (Slate 400)
        public static readonly Color ReadonlyBg = Color.FromArgb(0xF1, 0xF5, 0xF9);      // 只读框背景色 (Slate 100)
        public static readonly Color HoverBg = Color.FromArgb(0xF1, 0xF5, 0xF9);         // 列表悬浮色

        // 状态标签色彩
        public static readonly Color Success = Color.FromArgb(0x16, 0xA3, 0x4A);         // 成功/有效：绿色 (Green 600)
        public static readonly Color SuccessBg = Color.FromArgb(0xDC, 0xFC, 0xE7);       // 成功标签底色 (Green 100)
        public static readonly Color Warning = Color.FromArgb(0xD9, 0x77, 0x06);         // 警告色 (Amber 600)
        public static readonly Color WarningBg = Color.FromArgb(0xFE, 0xF3, 0xC7);       // 警告标签底色
        public static readonly Color ExpiredBg = Color.FromArgb(0xF1, 0xF5, 0xF9);       // 失效/过期标签底色
        public static readonly Color ExpiredText = Color.FromArgb(0x64, 0x74, 0x8B);     // 失效文字色

        // ---- 字体（统一 Microsoft YaHei UI 规避宋体回退） ----
        public static readonly Font FontBody = new Font("Microsoft YaHei UI", 9.5F);
        public static readonly Font FontBodyBold = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
        public static readonly Font FontHint = new Font("Microsoft YaHei UI", 8.5F);
        public static readonly Font FontSection = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
        public static readonly Font FontTitle = new Font("Microsoft YaHei UI", 12.5F, FontStyle.Bold);
        public static readonly Font FontHeader = new Font("Microsoft YaHei UI", 13.5F, FontStyle.Bold);

        // ---- 布局尺寸与间距规范 (8px 栅格) ----
        public const int InputHeight = 32;
        public const int ButtonHeight = 32;
        public const int Margin = 16;
        public const int SectionGap = 16;
        public const int RowGap = 10;
        public const int LabelWidth = 100;
        public const int ButtonGap = 8;
        public const int CardCornerRadius = 6;

        // ---- 按钮样式生成 ----
        public static void StyleButton(Button b, bool primary)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.Font = FontBody;
            b.Height = ButtonHeight;
            b.Cursor = Cursors.Hand;
            if (primary)
            {
                b.BackColor = Primary;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = PrimaryDark;
                b.FlatAppearance.MouseDownBackColor = PrimaryDark;
            }
            else
            {
                b.BackColor = Color.White;
                b.ForeColor = Ink;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = Border;
                b.FlatAppearance.MouseOverBackColor = HoverBg;
                b.FlatAppearance.MouseDownBackColor = Border;
            }
        }

        public static Button PrimaryButton(string text, int width)
        {
            var b = new Button { Text = text, Size = new Size(width, ButtonHeight) };
            StyleButton(b, true);
            return b;
        }

        public static Button SecondaryButton(string text, int width)
        {
            var b = new Button { Text = text, Size = new Size(width, ButtonHeight) };
            StyleButton(b, false);
            return b;
        }

        // ---- 输入框与标签 ----
        public static TextBox TextInput(int width)
        {
            return new TextBox
            {
                Size = new Size(width, InputHeight),
                AutoSize = false,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = Ink,
                Font = FontBody,
            };
        }

        public static TextBox ReadonlyInput(int width)
        {
            var t = TextInput(width);
            t.ReadOnly = true;
            t.BackColor = ReadonlyBg;
            return t;
        }

        public static Label BodyLabel(string text)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = Ink, Font = FontBody };
        }

        public static Label HintLabel(string text)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = Secondary, Font = FontHint };
        }

        public static Label SectionLabel(string text)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = Ink, Font = FontSection };
        }

        // 现代化卡片面板：带细腻浅灰边框与轻微圆角
        public static Panel CardPanel(int width, int height)
        {
            return new RoundedPanel
            {
                Size = new Size(width, height),
                BackColor = Card,
                BorderColor = Border,
                CornerRadius = CardCornerRadius,
            };
        }

        // 辅助绘制圆角矩形路径
        public static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // 自绘柔和边框的卡片容器
    public class RoundedPanel : Panel
    {
        public Color BorderColor { get; set; } = Theme.Border;
        public int CornerRadius { get; set; } = Theme.CardCornerRadius;

        public RoundedPanel()
        {
            DoubleBuffered = true;
            BorderStyle = BorderStyle.None;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.CreateRoundedRectangle(rect, CornerRadius))
            {
                using (var brush = new SolidBrush(BackColor))
                {
                    e.Graphics.FillPath(brush, path);
                }
                using (var pen = new Pen(BorderColor, 1f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }
    }
}
