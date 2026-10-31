using System;
using System.Drawing;
using System.Windows.Forms;

namespace FnShare
{
    public class ResultViewModel
    {
        public ShareRecord Record { get; set; }
        public string Link { get; set; } = "";
        public string TypeLabel { get; set; } = "外部";
        public string PasswordText { get; set; } = "无";
        public string Remark { get; set; } = "";
    }

    // 分享结果窗口：创建成功后展示，也用于记录管理中的“再次查看”。
    public class ResultForm : Form
    {
        private readonly ResultViewModel _vm;
        private readonly TextBox _txtLink = new TextBox();
        private readonly TextBox _txtPassword = new TextBox();

        public ResultForm(ResultViewModel vm)
        {
            _vm = vm;
            Text = "分享成功";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBody;
            BackColor = Theme.Page;
            Icon = AppIcon.Get();
            ClientSize = new Size(600, 440);

            // 顶部成功区：绿色圆角胶囊徽标＋“分享成功”现代标题
            var pnlSuccess = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Theme.Card,
            };
            pnlSuccess.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(pen, 0, pnlSuccess.Height - 1, pnlSuccess.Width, pnlSuccess.Height - 1);
                }
            };

            var lblCheck = new Label
            {
                Text = "✓",
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                ForeColor = Theme.Success,
                BackColor = Theme.SuccessBg,
                Size = new Size(36, 36),
                Location = new Point(24, 16),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            var lblTitle = new Label
            {
                Text = "分享链接已就绪",
                Font = Theme.FontTitle,
                ForeColor = Theme.Ink,
                AutoSize = true,
                Location = new Point(68, 14),
            };
            var lblSub = new Label
            {
                Text = "链接与访问凭证已生成，可直接复制发送给访问方",
                Font = Theme.FontHint,
                ForeColor = Theme.Secondary,
                AutoSize = true,
                Location = new Point(70, 38),
            };
            pnlSuccess.Controls.AddRange(new Control[] { lblCheck, lblTitle, lblSub });

            // 内容网格区
            var pnlCenter = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Page,
                Padding = new Padding(16, 12, 16, 12),
            };

            var cardDetail = Theme.CardPanel(568, 280);
            cardDetail.Dock = DockStyle.Fill;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(16, 12, 16, 12),
                BackColor = Color.Transparent,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            int[] rowHeights = new int[] { 28, 28, 28, 36, 36, 28, 28, 28 };
            for (int i = 0; i < rowHeights.Length; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeights[i]));

            AddRow(layout, 0, "分享对象：", vm.Record.Name);
            AddRow(layout, 1, "对象类型：", ShareRules.KindText(vm.Record));
            AddRow(layout, 2, "分享类型：", vm.TypeLabel);

            _txtLink.ReadOnly = true;
            _txtLink.Text = vm.Link;
            _txtLink.AutoSize = false;
            _txtLink.BorderStyle = BorderStyle.FixedSingle;
            _txtLink.BackColor = Theme.ReadonlyBg;
            _txtLink.Font = Theme.FontBody;
            _txtLink.ForeColor = Theme.Ink;

            var pnlLink = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            var btnCopyLinkRow = Theme.SecondaryButton("复制", 60);
            btnCopyLinkRow.Dock = DockStyle.Right;
            btnCopyLinkRow.Click += (s, e) => Clipboard.SetText(vm.Link);
            _txtLink.Dock = DockStyle.Fill;

            pnlLink.Controls.Add(_txtLink);
            pnlLink.Controls.Add(btnCopyLinkRow);
            AddControlRow(layout, 3, "分享链接：", pnlLink);

            _txtPassword.ReadOnly = true;
            _txtPassword.Text = vm.PasswordText;
            _txtPassword.AutoSize = false;
            _txtPassword.BorderStyle = BorderStyle.FixedSingle;
            _txtPassword.BackColor = Theme.ReadonlyBg;
            _txtPassword.Font = Theme.FontBody;
            _txtPassword.ForeColor = Theme.Ink;
            AddControlRow(layout, 4, "提取密码：", _txtPassword);

            AddRow(layout, 5, "有效期：", ShareRules.ValidToText(vm.Record.ValidTo));
            AddRow(layout, 6, "访问次数：", ShareRules.AccessText(vm.Record.Access, vm.Record.MaxAccess));
            AddRow(layout, 7, "创建时间：", ShareRules.CreatedTimeText(vm.Record.CreatedTime));

            cardDetail.Controls.Add(layout);
            pnlCenter.Controls.Add(cardDetail);

            // 底部操作按钮栏
            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(16, 11, 16, 11),
                BackColor = Theme.Card,
            };
            pnlButtons.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(pen, 0, 0, pnlButtons.Width, 0);
                }
            };

            var btnClose = Theme.SecondaryButton("关闭", 80);
            btnClose.DialogResult = DialogResult.OK;

            var btnOpen = Theme.SecondaryButton("打开链接", 88);
            btnOpen.Click += (s, e) => ShellRegistration.OpenUrl(vm.Link);

            var btnCopyAll = Theme.SecondaryButton("复制完整信息", 110);
            btnCopyAll.Click += (s, e) => Clipboard.SetText(BuildFullInfo());

            var btnCopyPwd = Theme.SecondaryButton("复制密码", 88);
            btnCopyPwd.Click += (s, e) => Clipboard.SetText(vm.PasswordText);

            var btnCopyLink = Theme.PrimaryButton("复制链接", 96);
            btnCopyLink.Click += (s, e) => Clipboard.SetText(vm.Link);

            pnlButtons.Controls.AddRange(new Control[] { btnClose, btnOpen, btnCopyAll, btnCopyPwd, btnCopyLink });

            Controls.Add(pnlCenter);
            Controls.Add(pnlSuccess);
            Controls.Add(pnlButtons);

            AcceptButton = btnClose;
            CancelButton = btnClose;
        }

        private string BuildFullInfo()
        {
            return "分享名称：" + _vm.Record.Name + "\r\n"
                + "分享链接：" + _vm.Link + "\r\n"
                + "提取密码：" + _vm.PasswordText + "\r\n"
                + "有效期：" + ShareRules.ValidToText(_vm.Record.ValidTo) + "\r\n"
                + "访问次数：" + ShareRules.AccessText(_vm.Record.Access, _vm.Record.MaxAccess) + "\r\n"
                + "创建时间：" + ShareRules.CreatedTimeText(_vm.Record.CreatedTime);
        }

        private static void AddRow(TableLayoutPanel layout, int row, string label, string value)
        {
            var lbl = new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                ForeColor = Theme.Secondary,
                Font = Theme.FontBody,
            };
            var val = new Label
            {
                Text = value,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = Theme.Ink,
                Font = Theme.FontBody,
            };
            layout.Controls.Add(lbl, 0, row);
            layout.Controls.Add(val, 1, row);
        }

        private static void AddControlRow(TableLayoutPanel layout, int row, string label, Control control)
        {
            var lbl = new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                ForeColor = Theme.Secondary,
                Font = Theme.FontBody,
            };
            control.Dock = DockStyle.Fill;
            layout.Controls.Add(lbl, 0, row);
            layout.Controls.Add(control, 1, row);
        }
    }
}
