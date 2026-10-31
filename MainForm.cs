using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FnShare
{
    // 主窗口：分享记录页 ＋ 配置页。无参启动默认打开本窗口。
    public class MainForm : Form
    {
        private readonly AppConfig _config;
        private Dictionary<string, LocalExtra> _extras = new Dictionary<string, LocalExtra>();
        private string _defaultLink = "";
        private bool _busy;

        internal readonly TabControl _tabs = new TabControl();
        internal readonly TabPage _tabRecords = new TabPage("分享记录");
        internal readonly TabPage _tabConfig = new TabPage("配置");

        private readonly ListView _lv = new ListView();
        private readonly Label _lblStatus = new Label();
        private readonly Button _btnRefresh = new Button();
        private readonly Button _btnDetail = new Button();
        private readonly Button _btnCopyLink = new Button();
        private readonly Button _btnOpenLink = new Button();

        private readonly TextBox _txtNasName = new TextBox();
        private readonly TextBox _txtInternalBase = new TextBox();
        private readonly TextBox _txtExternalBase = new TextBox();
        private readonly TextBox _txtShareDomain = new TextBox();
        private readonly TextBox _txtUsername = new TextBox();
        private readonly TextBox _txtPassword = new TextBox();
        private readonly CheckBox _chkSavePassword = new CheckBox();
        private readonly CheckBox _chkIgnoreTls = new CheckBox();
        private readonly NumericUpDown _numDefaultDays = new NumericUpDown();
        private readonly NumericUpDown _numDefaultMaxAccess = new NumericUpDown();
        private readonly ListBox _lstMappings = new ListBox();
        private readonly Label _lblRegStatus = new Label();
        private readonly Label _lblConfigStatus = new Label();

        // 配置页外层滚动面板与滚轮消息过滤器
        internal Panel _panelConfig;
        private ConfigMouseWheelFilter _wheelFilter;

        // 左侧导航（视觉层）：圆角胶囊 Pill 导航
        private Panel _navRecordsPanel;
        private Label _navRecordsLabel;
        private Panel _navConfigPanel;
        private Label _navConfigLabel;
        private readonly Label _lblRegDot = new Label();

        public MainForm(AppConfig config, bool selectConfigTab = false, string notice = "")
        {
            _config = config;
            Text = "飞牛分享（FnShare）";
            StartPosition = FormStartPosition.CenterScreen;
            Font = Theme.FontBody;
            BackColor = Theme.Page;
            Icon = AppIcon.Get();
            ClientSize = new Size(1220, 840);
            MinimumSize = new Size(920, 580);

            BuildRecordsTab();
            BuildConfigTab();
            _tabRecords.BackColor = Theme.Page;
            _tabRecords.UseVisualStyleBackColor = false;
            _tabConfig.BackColor = Theme.Page;
            _tabConfig.UseVisualStyleBackColor = false;

            // 隐藏表头：页签条只留 1px 高，切换只走左侧导航
            _tabs.Appearance = TabAppearance.FlatButtons;
            _tabs.SizeMode = TabSizeMode.Fixed;
            _tabs.ItemSize = new Size(0, 1);
            _tabs.TabPages.Add(_tabRecords);
            _tabs.TabPages.Add(_tabConfig);

            // 顶栏：现代化明亮设计（高 66px 确保标题与副标题呼吸充裕、绝不遮挡）
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = Theme.Header };
            pnlHeader.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
                }
            };

            var picIcon = new PictureBox
            {
                Location = new Point(20, 16),
                Size = new Size(34, 34),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = AppIcon.Get().ToBitmap(),
            };
            var lblTitle = new Label
            {
                Text = "飞牛分享",
                Font = Theme.FontHeader,
                ForeColor = Theme.Ink,
                AutoSize = true,
                Location = new Point(64, 11),
            };
            var lblSubtitle = new Label
            {
                Text = "fnOS 文件外链分享助手",
                Font = Theme.FontHint,
                ForeColor = Theme.Secondary,
                AutoSize = true,
                Location = new Point(66, 38),
            };
            pnlHeader.Controls.AddRange(new Control[] { picIcon, lblTitle, lblSubtitle });

            // 内容区：左侧导航（176px 浅白底侧边栏）＋ 右侧 TabControl
            var pnlContent = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Page };
            var pnlNav = new Panel { Dock = DockStyle.Left, Width = 176, BackColor = Theme.Card };
            pnlNav.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(pen, pnlNav.Width - 1, 0, pnlNav.Width - 1, pnlNav.Height);
                }
            };

            var navContainer = new Panel { Dock = DockStyle.Top, Height = 130, Padding = new Padding(12, 12, 12, 0), BackColor = Color.Transparent };
            _navRecordsPanel = BuildNavItem("分享记录", true);
            _navConfigPanel = BuildNavItem("系统配置", false);

            navContainer.Controls.Add(_navConfigPanel);
            navContainer.Controls.Add(_navRecordsPanel);
            pnlNav.Controls.Add(navContainer);

            _tabs.Dock = DockStyle.Fill;
            pnlContent.Controls.Add(_tabs);
            pnlContent.Controls.Add(pnlNav);

            Controls.Add(pnlContent);
            Controls.Add(pnlHeader);

            // 注册配置页全域滚轮事件捕获过滤器
            _wheelFilter = new ConfigMouseWheelFilter(this);
            Application.AddMessageFilter(_wheelFilter);

            LoadConfigToUi();
            RefreshRegStatus();
            if (!string.IsNullOrEmpty(notice)) _lblStatus.Text = notice;
            SelectNav(selectConfigTab ? _tabConfig : _tabRecords);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_wheelFilter != null)
            {
                Application.RemoveMessageFilter(_wheelFilter);
                _wheelFilter = null;
            }
            base.OnFormClosed(e);
        }

        // 左侧导航项：微圆角胶囊 Pill 按钮，文字清爽专业
        private Panel BuildNavItem(string text, bool isRecords)
        {
            var panel = new RoundedPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                CornerRadius = 6,
                BorderColor = Color.Transparent,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 8),
            };
            var lbl = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0),
                Font = Theme.FontBody,
                ForeColor = Theme.Ink,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };
            panel.Controls.Add(lbl);

            EventHandler click = (s, e) => SelectNav(isRecords ? _tabRecords : _tabConfig);
            panel.Click += click;
            lbl.Click += click;

            lbl.MouseEnter += (s, e) =>
            {
                bool isCur = (_tabs.SelectedTab == (isRecords ? _tabRecords : _tabConfig));
                if (!isCur) panel.BackColor = Theme.HoverBg;
            };
            lbl.MouseLeave += (s, e) =>
            {
                bool isCur = (_tabs.SelectedTab == (isRecords ? _tabRecords : _tabConfig));
                if (!isCur) panel.BackColor = Color.Transparent;
            };

            if (isRecords)
            {
                _navRecordsLabel = lbl;
            }
            else
            {
                _navConfigLabel = lbl;
            }
            return panel;
        }

        // 切换右侧页签并同步导航选中态
        private void SelectNav(TabPage tab)
        {
            _tabs.SelectedTab = tab;
            bool records = tab == _tabRecords;
            UpdateNavItemVisual(_navRecordsPanel, _navRecordsLabel, records);
            UpdateNavItemVisual(_navConfigPanel, _navConfigLabel, !records);

            // 切换到配置页时，重置滚动位置到顶部
            if (!records && _panelConfig != null)
            {
                _panelConfig.AutoScrollPosition = new Point(0, 0);
            }
        }

        private void UpdateNavItemVisual(Panel panel, Label label, bool isSelected)
        {
            if (panel is RoundedPanel rp)
            {
                rp.BackColor = isSelected ? Theme.PrimarySubtle : Color.Transparent;
                rp.BorderColor = isSelected ? Theme.PrimaryBorder : Color.Transparent;
            }
            label.ForeColor = isSelected ? Theme.Primary : Theme.Ink;
            label.Font = isSelected ? Theme.FontBodyBold : Theme.FontBody;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_tabs.SelectedTab == _tabRecords && _config.IsNasConfigured())
            {
                _ = RefreshRecordsAsync();
            }
        }

        // ---------- 分享记录页 ----------

        private void BuildRecordsTab()
        {
            // 工具栏：统一高度与现代化圆角按钮
            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Theme.Page,
                Padding = new Padding(16, 10, 16, 10),
            };
            _btnRefresh.Text = "刷新";
            _btnRefresh.Location = new Point(16, 10);
            _btnRefresh.Size = new Size(84, 32);
            Theme.StyleButton(_btnRefresh, true);
            _btnRefresh.Click += async (s, e) => await RefreshRecordsAsync();

            _btnDetail.Text = "查看详情";
            _btnDetail.Location = new Point(108, 10);
            _btnDetail.Size = new Size(92, 32);
            Theme.StyleButton(_btnDetail, false);
            _btnDetail.Click += BtnDetail_Click;

            _btnCopyLink.Text = "复制链接";
            _btnCopyLink.Location = new Point(208, 10);
            _btnCopyLink.Size = new Size(92, 32);
            Theme.StyleButton(_btnCopyLink, false);
            _btnCopyLink.Click += BtnCopyLink_Click;

            _btnOpenLink.Text = "打开链接";
            _btnOpenLink.Location = new Point(308, 10);
            _btnOpenLink.Size = new Size(92, 32);
            Theme.StyleButton(_btnOpenLink, false);
            _btnOpenLink.Click += BtnOpenLink_Click;

            pnlTop.Controls.AddRange(new Control[] { _btnRefresh, _btnDetail, _btnCopyLink, _btnOpenLink });

            // 列表卡片：包裹在白色圆角卡片面板内
            var pnlList = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Page, Padding = new Padding(16, 0, 16, 10) };
            var cardList = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                BorderColor = Theme.Border,
                CornerRadius = 6,
                Padding = new Padding(1),
            };

            _lv.Dock = DockStyle.Fill;
            _lv.View = View.Details;
            _lv.FullRowSelect = true;
            _lv.GridLines = false;
            _lv.MultiSelect = false;
            _lv.BackColor = Theme.Card;
            _lv.BorderStyle = BorderStyle.None;
            _lv.Font = Theme.FontBody;
            _lv.OwnerDraw = true;

            // 行高设置为 38px，留出舒适的垂直排版呼吸感
            _lv.SmallImageList = new ImageList { ImageSize = new Size(1, 38), ColorDepth = ColorDepth.Depth32Bit };

            // 科学设定列宽，消除不必要的横向滚动条
            _lv.Columns.Add("名称", 180);
            _lv.Columns.Add("类型", 70);
            _lv.Columns.Add("链接", 250);
            _lv.Columns.Add("创建时间", 130);
            _lv.Columns.Add("到期时间", 130);
            _lv.Columns.Add("访问次数", 95);
            _lv.Columns.Add("状态", 85);
            _lv.DoubleClick += BtnDetail_Click;

            // 现代化平坦表头自绘
            _lv.DrawColumnHeader += (s, e) =>
            {
                using (var b = new SolidBrush(Color.FromArgb(0xF8, 0xFA, 0xFC)))
                {
                    e.Graphics.FillRectangle(b, e.Bounds);
                }
                using (var pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                }
                var r = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, Theme.FontBodyBold, r, Theme.Secondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };
            _lv.DrawItem += (s, e) => { };
            _lv.DrawSubItem += Lv_DrawSubItem;

            cardList.Controls.Add(_lv);
            pnlList.Controls.Add(cardList);

            _lblStatus.Dock = DockStyle.Bottom;
            _lblStatus.Height = 28;
            _lblStatus.BackColor = Theme.Page;
            _lblStatus.ForeColor = Theme.Secondary;
            _lblStatus.Font = Theme.FontHint;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            _lblStatus.Padding = new Padding(16, 0, 0, 0);
            _lblStatus.Text = "就绪。";

            _tabRecords.Controls.Add(pnlList);
            _tabRecords.Controls.Add(pnlTop);
            _tabRecords.Controls.Add(_lblStatus);
        }

        // 自绘行与状态胶囊 Badge
        private void Lv_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color bg = e.Item.Selected ? Theme.PrimarySubtle : Theme.Card;
            using (var brush = new SolidBrush(bg))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            // 仅画行底分割线（去除了生硬的纵向网格竖线）
            using (var pen = new Pen(Theme.Border))
            {
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            // 状态列 (Index 6)：绘制圆角彩色胶囊药丸 (Status Badge)，严格居中防止遮挡
            if (e.ColumnIndex == 6)
            {
                string statusText = e.SubItem.Text;
                bool isValid = statusText == "有效";
                Color badgeBg = isValid ? Theme.SuccessBg : Theme.ExpiredBg;
                Color badgeText = isValid ? Theme.Success : Theme.ExpiredText;

                int badgeWidth = 62;
                int badgeHeight = 22;
                int badgeX = e.Bounds.X + (e.Bounds.Width - badgeWidth) / 2;
                int badgeY = e.Bounds.Y + (e.Bounds.Height - badgeHeight) / 2;
                var badgeRect = new Rectangle(badgeX, badgeY, badgeWidth, badgeHeight);

                using (var path = Theme.CreateRoundedRectangle(badgeRect, 11))
                using (var b = new SolidBrush(badgeBg))
                {
                    e.Graphics.FillPath(b, path);
                }

                TextRenderer.DrawText(e.Graphics, (isValid ? "● " : "○ ") + statusText,
                    Theme.FontHint, badgeRect, badgeText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            Color textColor = e.Item.Selected ? Theme.PrimaryDark : Theme.Ink;
            if (e.ColumnIndex == 1) textColor = Theme.Secondary;
            Rectangle textRect = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Theme.FontBody, textRect, textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _btnRefresh.Enabled = !busy;
            _btnDetail.Enabled = !busy;
            _btnCopyLink.Enabled = !busy;
            _btnOpenLink.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private ShareRecord SelectedRecord()
        {
            if (_lv.SelectedItems.Count == 0) return null;
            return _lv.SelectedItems[0].Tag as ShareRecord;
        }

        private void AddRecordRow(ShareRecord rec)
        {
            string link = ShareLinkBuilder.Build(_defaultLink, _config.ShareDomainOverride, rec.Id);
            var item = new ListViewItem(rec.Name);
            item.SubItems.Add(ShareRules.KindText(rec));
            item.SubItems.Add(link);
            item.SubItems.Add(ShareRules.CreatedTimeText(rec.CreatedTime));
            item.SubItems.Add(ShareRules.ValidToText(rec.ValidTo));
            item.SubItems.Add(ShareRules.AccessText(rec.Access, rec.MaxAccess));
            item.SubItems.Add(ShareRules.StatusText(ShareRules.Judge(rec)));
            item.Tag = rec;
            _lv.Items.Add(item);
        }

        private async Task RefreshRecordsAsync()
        {
            if (_busy) return;
            if (!_config.IsNasConfigured())
            {
                _lblStatus.Text = "请先在“配置”页完成 NAS 地址与账号配置。";
                return;
            }
            SetBusy(true);
            _lblStatus.Text = "正在连接 NAS 获取分享记录…";
            _lv.Items.Clear();
            _extras.Clear();
            try
            {
                string password = await EnsurePasswordAsync();
                if (password == null)
                {
                    _lblStatus.Text = "已取消获取。";
                    return;
                }
                Tuple<int, List<ShareRecord>> res;
                using (FnClient client = await FnClient.ConnectAsync(_config, password))
                {
                    _defaultLink = await client.GetDefaultLinkAsync();
                    res = await client.ListSharesAsync();
                }

                List<ShareRecord> list = res.Item2;
                _extras = ConfigStore.LoadExtras();
                foreach (ShareRecord r in list) AddRecordRow(r);

                _lblStatus.Text = "就绪。共 " + list.Count + " 条分享。";
                _ = BackfillDetailsAsync(list, password);
            }
            catch (FnApiException ex)
            {
                _lblStatus.Text = "获取分享记录失败：" + ex.Message;
            }
            catch (OperationCanceledException)
            {
                _lblStatus.Text = "连接超时，请检查 NAS 地址与网络。";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "获取分享记录失败：" + ex.Message;
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task BackfillDetailsAsync(List<ShareRecord> records, string password)
        {
            if (records == null || records.Count == 0) return;
            try
            {
                _lblStatus.Text = "正在拉取详情（用于显示路径与文件/文件夹类型）…";
                using (FnClient client = await FnClient.ConnectAsync(_config, password))
                {
                    for (int i = 0; i < records.Count; i++)
                    {
                        ShareRecord r = records[i];
                        try
                        {
                            ShareRecord full = await client.GetShareAsync(r.Id);
                            if (full != null && full.Files != null)
                            {
                                r.Files = full.Files;
                                if (i < _lv.Items.Count && _lv.Items[i].Tag == r)
                                {
                                    _lv.Items[i].SubItems[1].Text = ShareRules.KindText(r);
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                _lblStatus.Text = "就绪。共 " + records.Count + " 条分享。";
            }
            catch
            {
                _lblStatus.Text = "就绪。部分记录详情未拉取。";
            }
        }

        private async Task<string> GetLinkAsync(ShareRecord rec)
        {
            if (!string.IsNullOrEmpty(_defaultLink))
                return ShareLinkBuilder.Build(_defaultLink, _config.ShareDomainOverride, rec.Id);

            string password = await EnsurePasswordAsync();
            if (password != null)
            {
                using (FnClient client = await FnClient.ConnectAsync(_config, password))
                {
                    _defaultLink = await client.GetDefaultLinkAsync();
                }
            }
            return ShareLinkBuilder.Build(_defaultLink, _config.ShareDomainOverride, rec.Id);
        }

        private async void BtnDetail_Click(object sender, EventArgs e)
        {
            ShareRecord rec = SelectedRecord();
            if (rec == null)
            {
                _lblStatus.Text = "请先选择一条记录。";
                return;
            }
            try
            {
                string link = await GetLinkAsync(rec);
                if (string.IsNullOrEmpty(link)) return;
                LocalExtra extra = null;
                _extras.TryGetValue(rec.Id, out extra);
                string pwdText;
                if (extra != null && extra.PasswordEnabled)
                {
                    string p = PasswordStore.Unprotect(extra.PasswordProtected);
                    pwdText = string.IsNullOrEmpty(p) ? "已启用（未保存）" : p;
                }
                else
                {
                    pwdText = "无";
                }
                var vm = new ResultViewModel
                {
                    Record = rec,
                    Link = link,
                    TypeLabel = extra != null ? extra.TypeLabel : "外部",
                    PasswordText = pwdText,
                    Remark = extra != null ? extra.Remark : "",
                };
                using (var f = new ResultForm(vm))
                {
                    f.ShowDialog(this);
                }
            }
            catch (FnApiException ex)
            {
                _lblStatus.Text = "查看详情失败：" + ex.Message;
            }
            catch (OperationCanceledException)
            {
                _lblStatus.Text = "操作超时，请检查 NAS 地址与网络。";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "查看详情失败：" + ex.Message;
            }
        }

        private async void BtnCopyLink_Click(object sender, EventArgs e)
        {
            ShareRecord rec = SelectedRecord();
            if (rec == null)
            {
                _lblStatus.Text = "请先选择一条记录。";
                return;
            }
            try
            {
                string link = await GetLinkAsync(rec);
                if (string.IsNullOrEmpty(link)) return;
                Clipboard.SetText(link);
                _lblStatus.Text = "链接已复制。";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "复制失败：" + ex.Message;
            }
        }

        private async void BtnOpenLink_Click(object sender, EventArgs e)
        {
            ShareRecord rec = SelectedRecord();
            if (rec == null)
            {
                _lblStatus.Text = "请先选择一条记录。";
                return;
            }
            try
            {
                string link = await GetLinkAsync(rec);
                if (string.IsNullOrEmpty(link)) return;
                ShellRegistration.OpenUrl(link);
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "打开失败：" + ex.Message;
            }
        }

        // ---------- 配置页 ----------

        private void BuildConfigTab()
        {
            _panelConfig = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Theme.Page,
                Padding = new Padding(24, 16, 24, 24),
            };

            // ---- 卡片 1：NAS 连接 ----
            var cardNas = Theme.CardPanel(0, 412);
            cardNas.Dock = DockStyle.Top;
            var lblNasTitle = Theme.SectionLabel("NAS 连接");
            lblNasTitle.Location = new Point(20, 14);
            cardNas.Controls.Add(lblNasTitle);

            AddConfigRow(cardNas, "NAS 名称：", _txtNasName, 46);
            AddConfigRow(cardNas, "内网地址：", _txtInternalBase, 88);
            AddConfigRow(cardNas, "外网地址：", _txtExternalBase, 130);
            AddConfigRow(cardNas, "外链域名覆盖：", _txtShareDomain, 172);
            AddConfigRow(cardNas, "用户名：", _txtUsername, 214);
            AddConfigRow(cardNas, "密码：", _txtPassword, 256);
            _txtPassword.UseSystemPasswordChar = true;

            _chkSavePassword.Text = "保存密码（DPAPI 加密，仅本机当前登录用户可用）";
            _chkSavePassword.Location = new Point(144, 300);
            _chkSavePassword.AutoSize = true;
            _chkSavePassword.ForeColor = Theme.Ink;
            cardNas.Controls.Add(_chkSavePassword);

            _chkIgnoreTls.Text = "忽略证书错误（适用于自签名或局域网 HTTPS 证书）";
            _chkIgnoreTls.Location = new Point(144, 332);
            _chkIgnoreTls.AutoSize = true;
            _chkIgnoreTls.ForeColor = Theme.Ink;
            cardNas.Controls.Add(_chkIgnoreTls);

            var lblNasHint = Theme.HintLabel("说明：密码留空表示不改动；外链域名覆盖留空表示使用服务器返回的 defaultLink。");
            lblNasHint.Location = new Point(144, 368);
            cardNas.Controls.Add(lblNasHint);
            var sp1 = SpacerPanel();

            // ---- 卡片 2：默认分享策略 ----
            var cardPolicy = Theme.CardPanel(0, 136);
            cardPolicy.Dock = DockStyle.Top;
            var lblPolicyTitle = Theme.SectionLabel("默认分享策略");
            lblPolicyTitle.Location = new Point(20, 14);
            cardPolicy.Controls.Add(lblPolicyTitle);

            var lblDays = new Label
            {
                Text = "默认有效期：",
                Size = new Size(120, 32),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(16, 48),
                ForeColor = Theme.Ink,
            };
            _numDefaultDays.Location = new Point(144, 48);
            _numDefaultDays.Size = new Size(110, 32);
            _numDefaultDays.BorderStyle = BorderStyle.FixedSingle;
            _numDefaultDays.Font = Theme.FontBody;
            _numDefaultDays.Minimum = 0;
            _numDefaultDays.Maximum = 3650;
            var lblDaysHint = Theme.HintLabel("天（0 表示永久有效）");
            lblDaysHint.Location = new Point(264, 54);
            cardPolicy.Controls.AddRange(new Control[] { lblDays, _numDefaultDays, lblDaysHint });

            var lblMax = new Label
            {
                Text = "默认次数上限：",
                Size = new Size(120, 32),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(16, 88),
                ForeColor = Theme.Ink,
            };
            _numDefaultMaxAccess.Location = new Point(144, 88);
            _numDefaultMaxAccess.Size = new Size(110, 32);
            _numDefaultMaxAccess.BorderStyle = BorderStyle.FixedSingle;
            _numDefaultMaxAccess.Font = Theme.FontBody;
            _numDefaultMaxAccess.Minimum = 0;
            _numDefaultMaxAccess.Maximum = 1000000;
            var lblMaxHint = Theme.HintLabel("次（0 表示不限访问次数）");
            lblMaxHint.Location = new Point(264, 94);
            cardPolicy.Controls.AddRange(new Control[] { lblMax, _numDefaultMaxAccess, lblMaxHint });
            var sp2 = SpacerPanel();

            // ---- 卡片 3：同步目录映射 ----
            var cardMap = Theme.CardPanel(0, 276);
            cardMap.Dock = DockStyle.Top;
            var lblMapTitle = Theme.SectionLabel("同步目录映射");
            lblMapTitle.Location = new Point(20, 14);
            cardMap.Controls.Add(lblMapTitle);

            var lblMapHint = Theme.HintLabel("映射规则：本地同步目录  =>  NAS 远程根目录");
            lblMapHint.Location = new Point(20, 38);
            cardMap.Controls.Add(lblMapHint);

            _lstMappings.Location = new Point(20, 64);
            _lstMappings.Size = new Size(1028, 144);
            _lstMappings.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _lstMappings.BorderStyle = BorderStyle.FixedSingle;
            _lstMappings.BackColor = Color.White;
            _lstMappings.Font = Theme.FontBody;
            _lstMappings.ItemHeight = 30;
            _lstMappings.DrawMode = DrawMode.OwnerDrawFixed;
            _lstMappings.DrawItem += (s, e) =>
            {
                if (e.Index < 0 || e.Index >= _lstMappings.Items.Count) return;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                using (var b = new SolidBrush(selected ? Theme.PrimarySubtle : Color.White))
                {
                    e.Graphics.FillRectangle(b, e.Bounds);
                }
                using (var pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                }
                string text = _lstMappings.Items[e.Index].ToString();
                var r = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, text, Theme.FontBody, r, selected ? Theme.PrimaryDark : Theme.Ink,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };
            cardMap.Controls.Add(_lstMappings);

            var btnAddMap = Theme.SecondaryButton("添加映射", 88);
            btnAddMap.Location = new Point(764, 222);
            btnAddMap.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAddMap.Click += BtnAddMap_Click;

            var btnEditMap = Theme.SecondaryButton("编辑映射", 88);
            btnEditMap.Location = new Point(860, 222);
            btnEditMap.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnEditMap.Click += BtnEditMap_Click;

            var btnDelMap = Theme.SecondaryButton("删除映射", 88);
            btnDelMap.Location = new Point(956, 222);
            btnDelMap.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnDelMap.Click += BtnDelMap_Click;

            cardMap.Controls.AddRange(new Control[] { btnAddMap, btnEditMap, btnDelMap });
            var sp3 = SpacerPanel();

            // ---- 卡片 4：右键菜单 ----
            var cardReg = Theme.CardPanel(0, 130);
            cardReg.Dock = DockStyle.Top;
            var lblRegTitle = Theme.SectionLabel("Windows 资源管理器右键扩展");
            lblRegTitle.Location = new Point(20, 14);
            cardReg.Controls.Add(lblRegTitle);

            var btnRegister = Theme.SecondaryButton("注册右键菜单", 120);
            btnRegister.Location = new Point(20, 44);
            btnRegister.Click += (s, e) =>
            {
                try
                {
                    ShellRegistration.Register();
                    RefreshRegStatus();
                    _lblConfigStatus.Text = "右键菜单已成功注册。";
                }
                catch (Exception ex)
                {
                    _lblConfigStatus.Text = "注册失败：" + ex.Message;
                }
            };

            var btnUnregister = Theme.SecondaryButton("取消注册", 96);
            btnUnregister.Location = new Point(148, 44);
            btnUnregister.Click += (s, e) =>
            {
                try
                {
                    ShellRegistration.Unregister();
                    RefreshRegStatus();
                    _lblConfigStatus.Text = "右键菜单已取消注册。";
                }
                catch (Exception ex)
                {
                    _lblConfigStatus.Text = "取消注册失败：" + ex.Message;
                }
            };

            _lblRegDot.Text = "●";
            _lblRegDot.AutoSize = true;
            _lblRegDot.Location = new Point(260, 50);
            _lblRegDot.ForeColor = Theme.Success;

            _lblRegStatus.Location = new Point(280, 50);
            _lblRegStatus.AutoSize = true;
            _lblRegStatus.ForeColor = Theme.Ink;

            cardReg.Controls.AddRange(new Control[] { btnRegister, btnUnregister, _lblRegDot, _lblRegStatus });
            var lblRegHint = Theme.HintLabel("说明：右键扩展仅对已映射目录/文件生效；修改映射后实时生效，无需重新注册。");
            lblRegHint.Location = new Point(20, 94);
            cardReg.Controls.Add(lblRegHint);
            var sp4 = SpacerPanel();

            // 保存行与状态行
            var pnlSave = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.Page };
            var btnSave = Theme.PrimaryButton("保存配置", 100);
            btnSave.Location = new Point(0, 6);
            btnSave.Click += BtnSave_Click;

            var btnTest = Theme.SecondaryButton("连接测试", 100);
            btnTest.Location = new Point(112, 6);
            btnTest.Click += BtnTest_Click;

            pnlSave.Controls.AddRange(new Control[] { btnSave, btnTest });

            _lblConfigStatus.Dock = DockStyle.Top;
            _lblConfigStatus.Height = 32;
            _lblConfigStatus.ForeColor = Theme.Secondary;
            _lblConfigStatus.Font = Theme.FontHint;
            _lblConfigStatus.Padding = new Padding(0, 6, 0, 0);

            _panelConfig.Controls.Add(_lblConfigStatus);
            _panelConfig.Controls.Add(pnlSave);
            _panelConfig.Controls.Add(sp4);
            _panelConfig.Controls.Add(cardReg);
            _panelConfig.Controls.Add(sp3);
            _panelConfig.Controls.Add(cardMap);
            _panelConfig.Controls.Add(sp2);
            _panelConfig.Controls.Add(cardPolicy);
            _panelConfig.Controls.Add(sp1);
            _panelConfig.Controls.Add(cardNas);

            _tabConfig.Controls.Add(_panelConfig);
        }

        private static Panel SpacerPanel()
        {
            return new Panel { Dock = DockStyle.Top, Height = Theme.SectionGap, BackColor = Theme.Page };
        }

        private static void AddConfigRow(Panel card, string label, TextBox box, int y)
        {
            var lbl = new Label
            {
                Text = label,
                Size = new Size(120, 32),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(16, y),
                ForeColor = Theme.Ink,
                Font = Theme.FontBody,
            };
            box.AutoSize = false;
            box.Location = new Point(144, y);
            box.Size = new Size(580, 32);
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = Color.White;
            box.ForeColor = Theme.Ink;
            box.Font = Theme.FontBody;
            card.Controls.Add(lbl);
            card.Controls.Add(box);
        }

        private void LoadConfigToUi()
        {
            _txtNasName.Text = _config.NasName;
            _txtInternalBase.Text = _config.InternalBase;
            _txtExternalBase.Text = _config.ExternalBase;
            _txtShareDomain.Text = _config.ShareDomainOverride;
            _txtUsername.Text = _config.Username;
            _chkSavePassword.Checked = _config.SavePassword;
            _chkIgnoreTls.Checked = _config.IgnoreTlsErrors;
            _numDefaultDays.Value = Math.Min(Math.Max(_config.DefaultDays, 0), 3650);
            _numDefaultMaxAccess.Value = Math.Min(Math.Max(_config.DefaultMaxAccess, 0), 1000000);
            RefreshMappingList();
        }

        private void RefreshMappingList()
        {
            _lstMappings.Items.Clear();
            foreach (SyncMapping m in _config.Mappings) _lstMappings.Items.Add(m);
        }

        private void RefreshRegStatus()
        {
            try
            {
                bool reg = ShellRegistration.IsRegistered();
                _lblRegStatus.Text = reg ? "右键菜单：已注册" : "右键菜单：未注册";
                _lblRegDot.Visible = reg;
            }
            catch
            {
                _lblRegStatus.Text = "右键菜单：状态未知";
                _lblRegDot.Visible = false;
            }
        }

        private void ReadUiToConfig()
        {
            _config.NasName = _txtNasName.Text.Trim();
            _config.InternalBase = _txtInternalBase.Text.Trim().TrimEnd('/');
            _config.ExternalBase = _txtExternalBase.Text.Trim().TrimEnd('/');
            _config.ShareDomainOverride = _txtShareDomain.Text.Trim().TrimEnd('/');
            _config.Username = _txtUsername.Text.Trim();
            _config.IgnoreTlsErrors = _chkIgnoreTls.Checked;
            _config.DefaultDays = (int)_numDefaultDays.Value;
            _config.DefaultMaxAccess = (int)_numDefaultMaxAccess.Value;
            if (!string.IsNullOrEmpty(_txtPassword.Text))
            {
                _config.SetPassword(_txtPassword.Text, _chkSavePassword.Checked);
            }
            else if (!_chkSavePassword.Checked)
            {
                _config.SetPassword(_config.SessionPassword, false);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            ReadUiToConfig();
            ConfigStore.Save(_config);
            _lblConfigStatus.Text = "配置已保存。";
        }

        private Task<string> EnsurePasswordAsync()
        {
            if (!string.IsNullOrEmpty(_txtPassword.Text)) return Task.FromResult(_txtPassword.Text);
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

        private async void BtnTest_Click(object sender, EventArgs e)
        {
            ReadUiToConfig();
            _lblConfigStatus.Text = "正在连接测试（握手＋登录＋link.default）…";
            try
            {
                string password = await EnsurePasswordAsync();
                if (password == null)
                {
                    _lblConfigStatus.Text = "已取消。";
                    return;
                }
                using (FnClient client = await FnClient.ConnectAsync(_config, password))
                {
                    string defaultLink = await client.GetDefaultLinkAsync();
                    string msg = "连接成功。服务器返回的外链基地址：" + defaultLink;
                    if (!string.IsNullOrWhiteSpace(_config.ShareDomainOverride))
                        msg += "（已配置域名覆盖，实际链接将使用覆盖值）";
                    _lblConfigStatus.Text = msg;
                    MessageBox.Show(this, msg, "连接测试", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (FnApiException ex)
            {
                _lblConfigStatus.Text = "连接测试失败：" + ex.Message;
                MessageBox.Show(this, "连接测试失败：" + ex.Message, "失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (OperationCanceledException)
            {
                _lblConfigStatus.Text = "连接测试超时，请检查 NAS 地址与网络。";
            }
            catch (Exception ex)
            {
                _lblConfigStatus.Text = "连接测试失败：" + ex.Message;
                MessageBox.Show(this, "连接测试失败：" + ex.Message, "失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAddMap_Click(object sender, EventArgs e)
        {
            using (var dlg = new MappingForm(null))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _config.Mappings.Add(new SyncMapping { LocalDir = dlg.LocalDir, RemoteRoot = dlg.RemoteRoot });
                    RefreshMappingList();
                    ConfigStore.Save(_config);
                    _lblConfigStatus.Text = "映射已保存。";
                }
            }
        }

        private void BtnEditMap_Click(object sender, EventArgs e)
        {
            if (_lstMappings.SelectedItem == null)
            {
                _lblConfigStatus.Text = "请先选择一条映射。";
                return;
            }
            var existing = (SyncMapping)_lstMappings.SelectedItem;
            using (var dlg = new MappingForm(existing))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    existing.LocalDir = dlg.LocalDir;
                    existing.RemoteRoot = dlg.RemoteRoot;
                    RefreshMappingList();
                    ConfigStore.Save(_config);
                    _lblConfigStatus.Text = "映射已保存。";
                }
            }
        }

        private void BtnDelMap_Click(object sender, EventArgs e)
        {
            if (_lstMappings.SelectedItem == null)
            {
                _lblConfigStatus.Text = "请先选择一条映射。";
                return;
            }
            _config.Mappings.Remove((SyncMapping)_lstMappings.SelectedItem);
            RefreshMappingList();
            ConfigStore.Save(_config);
            _lblConfigStatus.Text = "映射已删除并保存。";
        }
    }

    // 全局滚轮过滤器：确保配置页任意子控件悬浮时滚轮均能流畅滑动
    internal class ConfigMouseWheelFilter : IMessageFilter
    {
        private readonly MainForm _form;
        public ConfigMouseWheelFilter(MainForm form) { _form = form; }

        public bool PreFilterMessage(ref Message m)
        {
            const int WM_MOUSEWHEEL = 0x020A;
            if (m.Msg == WM_MOUSEWHEEL)
            {
                if (_form._tabs.SelectedTab == _form._tabConfig && _form._panelConfig != null && _form._panelConfig.Visible)
                {
                    Point screenPt = Cursor.Position;
                    Rectangle screenRect = _form._panelConfig.RectangleToScreen(_form._panelConfig.ClientRectangle);
                    if (screenRect.Contains(screenPt))
                    {
                        int delta = unchecked((short)((long)m.WParam >> 16));
                        int current = -_form._panelConfig.AutoScrollPosition.Y;
                        int step = 48; // 每次滚动的像素步长
                        int target = current - (delta > 0 ? step : -step);
                        if (target < 0) target = 0;
                        _form._panelConfig.AutoScrollPosition = new Point(0, target);
                        return true; // 拦截处理，避免子控件吞掉滚轮消息
                    }
                }
            }
            return false;
        }
    }
}
