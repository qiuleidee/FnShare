using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FnShare
{
    // 创建分享窗口：右键 --share 入口直接打开，预填对象。
    // 采用现代卡片设计与优雅水平对齐规范。
    public class CreateShareForm : Form
    {
        private readonly AppConfig _config;
        private readonly string _localPath;
        private MapResult _map;

        private readonly TextBox _txtObject = new TextBox();
        private readonly TextBox _txtNasPath = new TextBox();
        private readonly TextBox _txtName = new TextBox();
        private readonly ComboBox _cmbType = new ComboBox();

        // 分享期限单选组（置于独立面板以隔离单选互斥）
        private readonly RadioButton _rdoDays7 = new RadioButton();
        private readonly RadioButton _rdoDaysNever = new RadioButton();
        private readonly RadioButton _rdoDaysCustom = new RadioButton();
        private readonly NumericUpDown _numDays = new NumericUpDown();

        // 访问次数单选组（置于独立面板以隔离单选互斥）
        private readonly RadioButton _rdoCountUnlimited = new RadioButton();
        private readonly RadioButton _rdoCountCustom = new RadioButton();
        private readonly NumericUpDown _numCount = new NumericUpDown();

        private readonly CheckBox _chkPassword = new CheckBox();
        private readonly TextBox _txtSharePassword = new TextBox();
        private readonly TextBox _txtRemark = new TextBox();
        private readonly Button _btnCreate = new Button();
        private readonly Button _btnCancel = new Button();
        private readonly Label _lblStatus = new Label();

        public CreateShareForm(AppConfig config, string localPath)
        {
            _config = config;
            _localPath = localPath;
            Text = "创建分享链接";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBody;
            BackColor = Theme.Page;
            Icon = AppIcon.Get();
            ClientSize = new Size(580, 640);

            // ---- 窗口顶部精致小 Banner ----
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Theme.Card,
            };
            pnlHeader.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
                }
            };
            var lblTitle = new Label
            {
                Text = "创建外链分享",
                Font = Theme.FontTitle,
                ForeColor = Theme.Ink,
                AutoSize = true,
                Location = new Point(20, 10),
            };
            var lblSub = new Label
            {
                Text = "确认分享对象及 NAS 路径，并配置生效策略与访问权限",
                Font = Theme.FontHint,
                ForeColor = Theme.Secondary,
                AutoSize = true,
                Location = new Point(20, 34),
            };
            pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblSub });

            // ---- 内容容器 ----
            var pnlBody = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Page,
                Padding = new Padding(16, 12, 16, 12),
            };

            // ---- 卡片 1：分享对象 ----
            var cardObject = Theme.CardPanel(548, 206);
            cardObject.Location = new Point(16, 12);
            var lblObjectTitle = Theme.SectionLabel("分享对象");
            lblObjectTitle.Location = new Point(18, 12);
            cardObject.Controls.Add(lblObjectTitle);

            AddCardRow(cardObject, "分享对象：", _txtObject, 40);
            _txtObject.BackColor = Theme.ReadonlyBg;
            _txtObject.ReadOnly = true;

            AddCardRow(cardObject, "NAS 路径：", _txtNasPath, 80);
            _txtNasPath.BackColor = Theme.ReadonlyBg;
            _txtNasPath.ReadOnly = true;

            AddCardRow(cardObject, "分享名称：", _txtName, 120);
            _txtName.BackColor = Color.White;

            var lblType = new Label
            {
                Text = "分享类型：",
                Size = new Size(96, 28),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(16, 160),
                ForeColor = Theme.Ink,
                Font = Theme.FontBody,
            };
            _cmbType.Location = new Point(122, 160);
            _cmbType.Size = new Size(160, 28);
            _cmbType.FlatStyle = FlatStyle.Standard;
            _cmbType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbType.Font = Theme.FontBody;
            _cmbType.Items.AddRange(new object[] { "内部", "外部" });
            _cmbType.SelectedItem = "外部";
            cardObject.Controls.AddRange(new Control[] { lblType, _cmbType });

            // ---- 卡片 2：分享策略 ----
            var cardPolicy = Theme.CardPanel(548, 136);
            cardPolicy.Location = new Point(16, 228);
            var lblPolicyTitle = Theme.SectionLabel("分享策略");
            lblPolicyTitle.Location = new Point(18, 12);
            cardPolicy.Controls.Add(lblPolicyTitle);

            // 第一组：有效期限单选组（通过 pnlDaysGroup 独立容器隔离单选互斥作用域）
            var pnlDaysGroup = new Panel
            {
                Location = new Point(20, 42),
                Size = new Size(510, 36),
                BackColor = Color.Transparent,
            };

            _rdoDays7.Text = "7 天";
            _rdoDays7.Location = new Point(4, 6);
            _rdoDays7.AutoSize = true;
            _rdoDays7.ForeColor = Theme.Ink;
            _rdoDays7.Font = Theme.FontBody;
            _rdoDays7.Checked = true;

            _rdoDaysNever.Text = "永久有效";
            _rdoDaysNever.Location = new Point(80, 6);
            _rdoDaysNever.AutoSize = true;
            _rdoDaysNever.ForeColor = Theme.Ink;
            _rdoDaysNever.Font = Theme.FontBody;

            _rdoDaysCustom.Text = "自定义";
            _rdoDaysCustom.Location = new Point(184, 6);
            _rdoDaysCustom.AutoSize = true;
            _rdoDaysCustom.ForeColor = Theme.Ink;
            _rdoDaysCustom.Font = Theme.FontBody;

            // x=272 确保与“自定义”文本间留出充足间距，绝不遮挡文字
            _numDays.Location = new Point(272, 4);
            _numDays.Size = new Size(76, 26);
            _numDays.BorderStyle = BorderStyle.FixedSingle;
            _numDays.Font = Theme.FontBody;
            _numDays.Minimum = 1;
            _numDays.Maximum = 3650;
            _numDays.Value = 30;
            _numDays.Enabled = false;

            var lblDays = new Label
            {
                Text = "天",
                AutoSize = true,
                Location = new Point(354, 7),
                ForeColor = Theme.Ink,
                Font = Theme.FontBody,
            };

            _rdoDaysCustom.CheckedChanged += (s, e) => _numDays.Enabled = _rdoDaysCustom.Checked;
            pnlDaysGroup.Controls.AddRange(new Control[] { _rdoDays7, _rdoDaysNever, _rdoDaysCustom, _numDays, lblDays });

            // 第二组：访问次数单选组（通过 pnlCountGroup 独立容器隔离单选互斥作用域）
            var pnlCountGroup = new Panel
            {
                Location = new Point(20, 84),
                Size = new Size(510, 36),
                BackColor = Color.Transparent,
            };

            _rdoCountUnlimited.Text = "不限次数";
            _rdoCountUnlimited.Location = new Point(4, 6);
            _rdoCountUnlimited.AutoSize = true;
            _rdoCountUnlimited.ForeColor = Theme.Ink;
            _rdoCountUnlimited.Font = Theme.FontBody;
            _rdoCountUnlimited.Checked = true;

            _rdoCountCustom.Text = "自定义";
            _rdoCountCustom.Location = new Point(116, 6);
            _rdoCountCustom.AutoSize = true;
            _rdoCountCustom.ForeColor = Theme.Ink;
            _rdoCountCustom.Font = Theme.FontBody;

            // x=204 确保与“自定义”文本间留出充足间距，绝不遮挡文字
            _numCount.Location = new Point(204, 4);
            _numCount.Size = new Size(76, 26);
            _numCount.BorderStyle = BorderStyle.FixedSingle;
            _numCount.Font = Theme.FontBody;
            _numCount.Minimum = 1;
            _numCount.Maximum = 1000000;
            _numCount.Value = 10;
            _numCount.Enabled = false;

            var lblCount = new Label
            {
                Text = "次",
                AutoSize = true,
                Location = new Point(286, 7),
                ForeColor = Theme.Ink,
                Font = Theme.FontBody,
            };

            _rdoCountCustom.CheckedChanged += (s, e) => _numCount.Enabled = _rdoCountCustom.Checked;
            pnlCountGroup.Controls.AddRange(new Control[] { _rdoCountUnlimited, _rdoCountCustom, _numCount, lblCount });

            cardPolicy.Controls.AddRange(new Control[] { pnlDaysGroup, pnlCountGroup });

            // ---- 卡片 3：安全与备注 ----
            var cardSecurity = Theme.CardPanel(548, 134);
            cardSecurity.Location = new Point(16, 374);
            var lblSecurityTitle = Theme.SectionLabel("安全与备注");
            lblSecurityTitle.Location = new Point(18, 12);
            cardSecurity.Controls.Add(lblSecurityTitle);

            _chkPassword.Text = "设置访问密码";
            _chkPassword.Location = new Point(24, 46);
            _chkPassword.AutoSize = true;
            _chkPassword.ForeColor = Theme.Ink;
            _chkPassword.Font = Theme.FontBody;

            // x=160 彻底避开“设置访问密码”复选框文字（最宽到 149px），右侧 368px 与下方备注框对齐
            _txtSharePassword.Location = new Point(160, 44);
            _txtSharePassword.Size = new Size(368, 26);
            _txtSharePassword.BorderStyle = BorderStyle.FixedSingle;
            _txtSharePassword.BackColor = Color.White;
            _txtSharePassword.Font = Theme.FontBody;
            _txtSharePassword.UseSystemPasswordChar = true;
            _txtSharePassword.Enabled = false;

            _chkPassword.CheckedChanged += (s, e) =>
            {
                _txtSharePassword.Enabled = _chkPassword.Checked;
                if (_chkPassword.Checked) _txtSharePassword.Focus();
            };

            cardSecurity.Controls.AddRange(new Control[] { _chkPassword, _txtSharePassword });

            AddCardRow(cardSecurity, "分享备注：", _txtRemark, 84);
            _txtRemark.BackColor = Color.White;

            pnlBody.Controls.AddRange(new Control[] { cardObject, cardPolicy, cardSecurity });

            // ---- 底部独立操作栏容器 ----
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Theme.Card,
            };
            pnlBottom.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
                }
            };

            _lblStatus.Location = new Point(20, 18);
            _lblStatus.Size = new Size(330, 22);
            _lblStatus.Font = Theme.FontHint;
            _lblStatus.ForeColor = Theme.Secondary;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;

            _btnCreate.Text = "创建分享";
            _btnCreate.Location = new Point(366, 11);
            _btnCreate.Size = new Size(100, 34);
            Theme.StyleButton(_btnCreate, true);
            _btnCreate.Click += BtnCreate_Click;

            _btnCancel.Text = "取消";
            _btnCancel.Location = new Point(476, 11);
            _btnCancel.Size = new Size(88, 34);
            Theme.StyleButton(_btnCancel, false);
            _btnCancel.Click += (s, e) => Close();

            pnlBottom.Controls.AddRange(new Control[] { _lblStatus, _btnCreate, _btnCancel });

            Controls.Add(pnlBody);
            Controls.Add(pnlHeader);
            Controls.Add(pnlBottom);

            AcceptButton = _btnCreate;
            CancelButton = _btnCancel;
        }

        private static void AddCardRow(Panel card, string label, TextBox box, int y)
        {
            var lbl = new Label
            {
                Text = label,
                Size = new Size(96, 26),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(16, y),
                ForeColor = Theme.Ink,
                Font = Theme.FontBody,
            };
            box.Location = new Point(122, y);
            box.Size = new Size(406, 26);
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Font = Theme.FontBody;
            box.ForeColor = Theme.Ink;
            card.Controls.Add(lbl);
            card.Controls.Add(box);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!_config.IsNasConfigured())
            {
                MessageBox.Show(this, "请先在主窗口完成 NAS 连接配置，再创建分享。",
                    "配置不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
                return;
            }
            _map = PathMapper.Map(_localPath, _config.Mappings);
            if (!_map.Ok)
            {
                MessageBox.Show(this, _map.Error, "无法分享",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
                return;
            }
            _txtObject.Text = _localPath;
            _txtNasPath.Text = _map.NasPath + (_map.IsDir ? "（文件夹）" : "（文件）");
            _txtName.Text = _map.Name;

            // 根据配置载入默认天数
            if (_config.DefaultDays <= 0)
            {
                _rdoDaysNever.Checked = true;
            }
            else if (_config.DefaultDays == 7)
            {
                _rdoDays7.Checked = true;
            }
            else
            {
                _rdoDaysCustom.Checked = true;
                _numDays.Value = Math.Min(Math.Max(_config.DefaultDays, 1), 3650);
                _numDays.Enabled = true;
            }

            // 根据配置载入默认访问次数
            if (_config.DefaultMaxAccess <= 0)
            {
                _rdoCountUnlimited.Checked = true;
            }
            else
            {
                _rdoCountCustom.Checked = true;
                _numCount.Value = Math.Min(Math.Max(_config.DefaultMaxAccess, 1), 1000000);
                _numCount.Enabled = true;
            }
        }

        private Task<string> EnsurePasswordAsync()
        {
            string pwd = _config.ResolvePassword();
            if (!string.IsNullOrEmpty(pwd)) return Task.FromResult(pwd);
            using (var dlg = new PasswordPromptForm())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _config.SessionPassword = dlg.Password;
                    return Task.FromResult(dlg.Password);
                }
            }
            return Task.FromResult<string>(null);
        }

        private void SetBusy(bool busy, string status = "")
        {
            _btnCreate.Enabled = !busy;
            _btnCancel.Enabled = !busy;
            _lblStatus.Text = status;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private async void BtnCreate_Click(object sender, EventArgs e)
        {
            string name = _txtName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(this, "请填写分享名称。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int days;
            if (_rdoDaysNever.Checked) days = 0;
            else if (_rdoDaysCustom.Checked) days = (int)_numDays.Value;
            else days = 7;
            int maxAccess = _rdoCountCustom.Checked ? (int)_numCount.Value : 0;
            bool usePwd = _chkPassword.Checked;
            string sharePwd = usePwd ? _txtSharePassword.Text : "";
            if (usePwd && string.IsNullOrEmpty(sharePwd))
            {
                MessageBox.Show(this, "已勾选密码，请输入分享密码。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string typeLabel = _cmbType.SelectedItem != null ? _cmbType.SelectedItem.ToString() : "外部";
            string remark = _txtRemark.Text.Trim();

            SetBusy(true, "正在创建分享…");
            try
            {
                string password = await EnsurePasswordAsync();
                if (password == null)
                {
                    SetBusy(false, "已取消。");
                    return;
                }
                using (FnClient client = await FnClient.ConnectAsync(_config, password))
                {
                    string defaultLink = await client.GetDefaultLinkAsync();
                    long? validTo = days > 0
                        ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)days * 86400
                        : (long?)null;
                    ShareRecord rec = await client.CreateShareAsync(
                        name, _map.NasPath, maxAccess, validTo, usePwd ? sharePwd : null);
                    string link = ShareLinkBuilder.Build(defaultLink, _config.ShareDomainOverride, rec.Id);

                    var extras = ConfigStore.LoadExtras();
                    extras[rec.Id] = new LocalExtra
                    {
                        LocalPath = _localPath,
                        TypeLabel = typeLabel,
                        Remark = remark,
                        PasswordEnabled = usePwd,
                        PasswordProtected = usePwd ? PasswordStore.Protect(sharePwd) : "",
                        SavedAt = DateTime.Now,
                    };
                    ConfigStore.SaveExtras(extras);

                    var vm = new ResultViewModel
                    {
                        Record = rec,
                        Link = link,
                        TypeLabel = typeLabel,
                        PasswordText = usePwd ? sharePwd : "无",
                        Remark = remark,
                    };
                    using (var rf = new ResultForm(vm))
                    {
                        rf.ShowDialog(this);
                    }
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            catch (FnApiException ex)
            {
                MessageBox.Show(this, "创建分享失败：" + ex.Message, "失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show(this, "操作超时，请检查 NAS 地址与网络。", "超时",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "创建分享失败：" + ex.Message, "失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }
    }
}
