using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FnShare
{
    // 同步目录映射编辑：本地目录（可浏览）＋ NAS 远程根
    public class MappingForm : Form
    {
        private readonly TextBox _txtLocal = new TextBox();
        private readonly TextBox _txtRemote = new TextBox();

        public string LocalDir => _txtLocal.Text.Trim();
        public string RemoteRoot => _txtRemote.Text.Trim();

        public MappingForm(SyncMapping existing)
        {
            Text = existing == null ? "添加同步目录映射" : "编辑同步目录映射";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBody;
            BackColor = Theme.Page;
            Icon = AppIcon.Get();
            ClientSize = new Size(540, 186);

            var lblLocal = new Label
            {
                Text = "本地同步目录：",
                Size = new Size(110, 32),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(16, 20),
                ForeColor = Theme.Ink,
                Font = Theme.FontBody,
            };
            _txtLocal.Location = new Point(130, 20);
            _txtLocal.Size = new Size(306, 32);
            _txtLocal.AutoSize = false;
            _txtLocal.BorderStyle = BorderStyle.FixedSingle;
            _txtLocal.BackColor = Color.White;
            _txtLocal.Font = Theme.FontBody;

            var btnBrowse = Theme.SecondaryButton("浏览…", 76);
            btnBrowse.Location = new Point(444, 20);
            btnBrowse.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog { Description = "选择本地同步目录" })
                {
                    if (Directory.Exists(_txtLocal.Text)) dlg.SelectedPath = _txtLocal.Text;
                    if (dlg.ShowDialog(this) == DialogResult.OK) _txtLocal.Text = dlg.SelectedPath;
                }
            };

            var lblRemote = new Label
            {
                Text = "NAS 远程根：",
                Size = new Size(110, 32),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(16, 64),
                ForeColor = Theme.Ink,
                Font = Theme.FontBody,
            };
            _txtRemote.Location = new Point(130, 64);
            _txtRemote.Size = new Size(306, 32);
            _txtRemote.AutoSize = false;
            _txtRemote.BorderStyle = BorderStyle.FixedSingle;
            _txtRemote.BackColor = Color.White;
            _txtRemote.Font = Theme.FontBody;

            var lblHint = Theme.HintLabel("说明：远程根为该本地目录在 NAS 侧对应的绝对路径，以 / 开头。");
            lblHint.Location = new Point(130, 102);

            var btnOk = Theme.PrimaryButton("确定", 84);
            btnOk.DialogResult = DialogResult.OK;
            btnOk.Location = new Point(348, 136);

            var btnCancel = Theme.SecondaryButton("取消", 84);
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Location = new Point(440, 136);

            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtLocal.Text) || !Directory.Exists(_txtLocal.Text))
                {
                    MessageBox.Show(this, "本地同步目录不存在，请重新选择。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                if (string.IsNullOrWhiteSpace(_txtRemote.Text) || !_txtRemote.Text.Trim().StartsWith("/"))
                {
                    MessageBox.Show(this, "NAS 远程根不能为空，且须以 / 开头。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
            };

            Controls.AddRange(new Control[] { lblLocal, _txtLocal, btnBrowse, lblRemote, _txtRemote, lblHint, btnOk, btnCancel });
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            if (existing != null)
            {
                _txtLocal.Text = existing.LocalDir;
                _txtRemote.Text = existing.RemoteRoot;
            }
        }
    }

    // NAS 账号密码输入（未保存密码时，操作前弹出；仅会话内使用）
    public class PasswordPromptForm : Form
    {
        private readonly TextBox _txt = new TextBox();

        public string Password => _txt.Text;

        public PasswordPromptForm()
        {
            Text = "输入 NAS 账号密码";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBody;
            BackColor = Theme.Page;
            Icon = AppIcon.Get();
            ClientSize = new Size(420, 150);

            var lbl = new Label
            {
                Text = "NAS 账号密码：",
                Size = new Size(110, 32),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(16, 24),
                ForeColor = Theme.Ink,
                Font = Theme.FontBody,
            };
            _txt.Location = new Point(130, 24);
            _txt.Size = new Size(260, 32);
            _txt.AutoSize = false;
            _txt.BorderStyle = BorderStyle.FixedSingle;
            _txt.BackColor = Color.White;
            _txt.Font = Theme.FontBody;
            _txt.UseSystemPasswordChar = true;

            var lblHint = Theme.HintLabel("说明：密码仅用于本次建立安全连接，不会明文持久化保存。");
            lblHint.Location = new Point(130, 62);

            var btnOk = Theme.PrimaryButton("确定", 84);
            btnOk.DialogResult = DialogResult.OK;
            btnOk.Location = new Point(222, 102);

            var btnCancel = Theme.SecondaryButton("取消", 84);
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Location = new Point(314, 102);

            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrEmpty(_txt.Text))
                {
                    MessageBox.Show(this, "请输入密码。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                }
            };
            Controls.AddRange(new Control[] { lbl, _txt, lblHint, btnOk, btnCancel });
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
