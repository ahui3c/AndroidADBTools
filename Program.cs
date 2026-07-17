/*
 * Android ADB Quick Tools
 * Copyright (C) 2026 Liao Ah-Hui (廖阿輝)
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License version 3.
 * This program is distributed WITHOUT ANY WARRANTY; see LICENSE for details.
 * SPDX-License-Identifier: AGPL-3.0-only
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("Android ADB 快速工具")]
[assembly: AssemblyDescription("Android ADB 連線確認與 APK 快速安裝工具")]
[assembly: AssemblyCompany("AndroidADBTools")]
[assembly: AssemblyProduct("Android ADB 快速工具")]
[assembly: AssemblyCopyright("Copyright © 2026 廖阿輝")]
[assembly: AssemblyVersion("1.16.0.0")]
[assembly: AssemblyFileVersion("1.16.0.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace AndroidADBTools
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4.
                // This must happen before any WinForms handle is created.
                if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware();
            }
            catch (EntryPointNotFoundException)
            {
                SetProcessDPIAware();
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public sealed class AppSettings
    {
        public string AdbPath { get; set; }
        public bool AllowDowngrade { get; set; }
        public List<ApkGroup> Groups { get; set; }
        public List<string> GroupOrder { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public bool WindowMaximized { get; set; }
        public string DownloadFolder { get; set; }
        public bool SkipLargeDownloadFiles { get; set; }
        public decimal MaxDownloadFileSizeGb { get; set; }

        public AppSettings()
        {
            AdbPath = "";
            Groups = new List<ApkGroup>();
            GroupOrder = new List<string>();
            DownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Android手機資料下載");
            SkipLargeDownloadFiles = true;
            MaxDownloadFileSizeGb = 2M;
        }
    }

    public sealed class ApkGroup
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<ApkEntry> Apks { get; set; }
        public bool IsFolderGroup { get; set; }
        public string FolderPath { get; set; }

        public ApkGroup()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "新的安裝組合";
            Apks = new List<ApkEntry>();
            FolderPath = "";
        }

        public override string ToString()
        {
            return Name + "  (" + (Apks == null ? 0 : Apks.Count) + ")";
        }
    }

    public sealed class ApkEntry
    {
        public string Path { get; set; }
        public ApkEntry() { Path = ""; }
        public ApkEntry(string path) { Path = path; }
    }

    public sealed class AdbResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public bool Started { get; set; }
    }

    public sealed class DeviceInfo
    {
        public string Serial { get; set; }
        public string State { get; set; }
        public string Model { get; set; }
        public string Product { get; set; }
    }

    public sealed class RemoteFileInfo
    {
        public string Path { get; set; }
        public long Size { get; set; }
    }

    public sealed class ModernTabControl : TabControl
    {
        public ModernTabControl()
        {
            DrawMode = TabDrawMode.OwnerDrawFixed;
            SizeMode = TabSizeMode.Fixed;
            ItemSize = new Size(174, 46);
            Padding = new Point(0, 0);
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(Color.FromArgb(18, 22, 29));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(18, 22, 29));
            for (int i = 0; i < TabPages.Count; i++)
            {
                DrawItemState state = SelectedIndex == i ? DrawItemState.Selected : DrawItemState.Default;
                OnDrawItem(new DrawItemEventArgs(e.Graphics, Font, GetTabRect(i), i, state));
            }
            Rectangle pageBorder = DisplayRectangle;
            pageBorder.Inflate(1, 1);
            using (Pen pen = new Pen(Color.FromArgb(51, 62, 78))) e.Graphics.DrawRectangle(pen, pageBorder);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            Rectangle rect = GetTabRect(e.Index);
            rect = new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 6);
            bool selected = SelectedIndex == e.Index;
            Color accent = TabPages[e.Index].Tag is Color ? (Color)TabPages[e.Index].Tag : Color.FromArgb(81, 155, 255);
            Color fill = selected ? accent : Blend(Color.FromArgb(28, 34, 44), accent, 0.18F);
            Color border = selected ? accent : Blend(Color.FromArgb(58, 69, 86), accent, 0.35F);
            using (GraphicsPath path = RoundedPath(rect, 10))
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(border, selected ? 2F : 1F))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
            using (SolidBrush textBrush = new SolidBrush(selected ? Color.White : Color.FromArgb(190, 201, 218)))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                e.Graphics.DrawString(TabPages[e.Index].Text.Replace("&", ""), Font, textBrush, rect, format);
        }

        private static GraphicsPath RoundedPath(Rectangle rect, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color Blend(Color baseColor, Color tint, float amount)
        {
            return Color.FromArgb(
                (int)(baseColor.R * (1F - amount) + tint.R * amount),
                (int)(baseColor.G * (1F - amount) + tint.G * amount),
                (int)(baseColor.B * (1F - amount) + tint.B * amount));
        }
    }

    public sealed class MainForm : Form, IMessageFilter
    {
        private sealed class DpiMetric
        {
            public Rectangle Bounds;
            public Padding Padding;
            public Padding Margin;
            public Size MinimumSize;
            public Size MaximumSize;
            public DockStyle Dock;
            public bool AutoSize;
            public Size TabItemSize;
            public int ListBoxItemHeight;
            public int[] ListViewColumnWidths;
            public float[] TableRowHeights;
            public float[] TableColumnWidths;
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr handle, string subAppName, string subIdList);
        private readonly Color Bg = Color.FromArgb(18, 22, 29);
        private readonly Color Card = Color.FromArgb(27, 33, 43);
        private readonly Color Card2 = Color.FromArgb(35, 42, 54);
        private readonly Color Accent = Color.FromArgb(81, 155, 255);
        private readonly Color Green = Color.FromArgb(65, 201, 138);
        private readonly Color Red = Color.FromArgb(255, 105, 120);
        private readonly Color Muted = Color.FromArgb(158, 169, 188);
        private readonly Color TextColor = Color.FromArgb(238, 242, 248);

        private AppSettings settings;
        private readonly string settingsFile;
        private List<DeviceInfo> devices = new List<DeviceInfo>();
        private bool busy;
        private bool quickInstalling;

        private Label adbStatusLabel;
        private Label deviceStatusLabel;
        private Label deviceDetailLabel;
        private Button refreshButton;
        private Button browseAdbButton;
        private Button installGroupButton;
        private Button renameGroupButton;
        private Button deleteGroupButton;
        private Button addGroupApksButton;
        private Button removeGroupApkButton;
        private Button moveGroupUpButton;
        private Button moveGroupDownButton;
        private ListBox groupList;
        private ListView apkList;
        private TextBox logBox;
        private CheckBox downgradeCheck;
        private Label groupTitle;
        private Label groupHint;
        private Panel dropPanel;
        private CheckBox autoBrightnessCheck;
        private CheckBox timeoutTenMinutesCheck;
        private CheckBox timeoutNeverCheck;
        private CheckBox stayOnWhileChargingCheck;
        private Label quickSettingsStateLabel;
        private Button applyQuickSettingsButton;
        private Button readQuickSettingsButton;
        private Button volumeMinimumButton;
        private Button volumeMaximumButton;
        private Button openUrlButton;
        private Button screenshotButton;
        private TextBox downloadFolderTextBox;
        private CheckBox skipLargeDownloadCheck;
        private NumericUpDown maxDownloadSizeNumber;
        private Button browseDownloadFolderButton;
        private Button startDownloadButton;
        private Label downloadStatusLabel;
        private ProgressBar downloadProgressBar;
        private TextBox urlTextBox;
        private bool loadingQuickSettings;
        private TrackBar brightnessTrackBar;
        private NumericUpDown brightnessNumber;
        private Label brightnessValueLabel;
        private Label brightnessStatusLabel;
        private Label brightnessRangeLabel;
        private CheckBox brightnessDisableAutoCheck;
        private Button readBrightnessButton;
        private Button applyBrightnessButton;
        private Timer brightnessUpdateTimer;
        private bool loadingBrightness;
        private bool brightnessApplying;
        private int brightnessPendingValue;
        private int brightnessLastApplied = -1;
        private int brightnessDetectedMaximum = 255;
        private bool? brightnessAutoMode;
        private ToolTip groupNameToolTip;
        private int lastGroupTooltipIndex = -1;
        private ModernTabControl mainTabs;
        private TabPage brightnessTabPage;
        private readonly Dictionary<Control, DpiMetric> dpiMetrics = new Dictionary<Control, DpiMetric>();
        private readonly List<ApkGroup> folderGroups = new List<ApkGroup>();
        private float currentDpiScale = 1F;

        public MainForm()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AndroidADBTools");
            settingsFile = Path.Combine(folder, "settings.json");
            settings = LoadSettings();

            Text = "Android ADB 快速工具";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            MinimumSize = new Size(1100, 840);
            Size = new Size(1200, 960);
            BackColor = Bg;
            ForeColor = TextColor;
            Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            DoubleBuffered = true;

            BuildUi();
            Application.AddMessageFilter(this);
            CaptureDpiMetrics(this);
            ApplySmoothTextRendering(this);
            ScanFolderGroups();
            RefreshGroups();
            downgradeCheck.Checked = settings.AllowDowngrade;
            Load += delegate
            {
                ApplyDpiLayout(DeviceDpi);
                RestoreWindowSize();
            };
            Shown += async delegate
            {
                await CheckConnectionAsync();
            };
            DpiChanged += delegate(object sender, DpiChangedEventArgs e)
            {
                int newDpi = e.DeviceDpiNew;
                BeginInvoke(new Action(delegate { ApplyDpiLayout(newDpi); }));
            };
            ResizeEnd += delegate
            {
                CaptureWindowSize();
                SaveSettings();
            };
            FormClosing += delegate
            {
                CaptureWindowSize();
                SaveSettings();
            };
            FormClosed += delegate { Application.RemoveMessageFilter(this); };
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(22);
            root.BackColor = Bg;
            root.RowCount = 3;
            root.ColumnCount = 1;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            Panel header = new Panel { Dock = DockStyle.Fill };
            Label title = new Label
            {
                Text = "Android ADB 快速工具",
                Font = new Font(Font.FontFamily, 20F, FontStyle.Bold),
                ForeColor = TextColor,
                AutoSize = true,
                Location = new Point(0, 2)
            };
            Label subtitle = new Label
            {
                Text = "連線確認、常用 APK 安裝與快速安裝",
                ForeColor = Muted,
                AutoSize = true,
                Location = new Point(2, 38)
            };
            Label versionLabel = new Label
            {
                Text = AppVersionText(),
                ForeColor = Color.FromArgb(105, 116, 134),
                Font = new Font(Font.FontFamily, 8.5F, FontStyle.Regular),
                Dock = DockStyle.Right,
                Width = 92,
                TextAlign = ContentAlignment.MiddleRight
            };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(versionLabel);
            root.Controls.Add(header, 0, 0);

            Panel deviceCard = NewCard();
            deviceCard.Dock = DockStyle.Fill;
            deviceCard.Padding = new Padding(18, 14, 18, 12);
            root.Controls.Add(deviceCard, 0, 1);

            adbStatusLabel = new Label
            {
                Text = "● 正在尋找 ADB...",
                ForeColor = Muted,
                Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(18, 16)
            };
            deviceStatusLabel = new Label
            {
                Text = "尚未檢查手機",
                ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 15F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(18, 46)
            };
            deviceDetailLabel = new Label
            {
                Text = "請開啟 USB 偵錯並連接手機",
                ForeColor = Muted,
                AutoSize = true,
                Location = new Point(20, 79)
            };
            deviceCard.Controls.Add(adbStatusLabel);
            deviceCard.Controls.Add(deviceStatusLabel);
            deviceCard.Controls.Add(deviceDetailLabel);

            FlowLayoutPanel statusActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 520,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 24, 0, 0),
                BackColor = Card
            };
            refreshButton = NewButton("重新檢查", true, 108);
            refreshButton.Click += async delegate { await CheckConnectionAsync(); };
            browseAdbButton = NewButton("選擇 adb.exe", false, 132);
            browseAdbButton.Click += BrowseAdb;
            Button helpButton = NewButton("連線教學", false, 105);
            helpButton.Click += ShowConnectionHelp;
            Button aboutButton = NewButton("關於", false, 78);
            aboutButton.Click += ShowAbout;
            statusActions.Controls.Add(refreshButton);
            statusActions.Controls.Add(browseAdbButton);
            statusActions.Controls.Add(helpButton);
            statusActions.Controls.Add(aboutButton);
            deviceCard.Controls.Add(statusActions);

            mainTabs = new ModernTabControl();
            mainTabs.Dock = DockStyle.Fill;
            mainTabs.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold);
            mainTabs.BackColor = Bg;
            mainTabs.ItemSize = new Size(150, 46);
            TabPage groupsTab = NewTab("▦  常用 APK 安裝", Color.FromArgb(53, 120, 219));
            TabPage singleTab = NewTab("⇩  快速安裝", Color.FromArgb(126, 87, 194));
            TabPage brightnessTab = NewTab("☀  亮度調整", Color.FromArgb(211, 132, 42));
            brightnessTabPage = brightnessTab;
            TabPage quickSettingsTab = NewTab("⚙  快速設定", Color.FromArgb(32, 151, 116));
            TabPage downloadTab = NewTab("↓  資料下載", Color.FromArgb(35, 156, 181));
            TabPage logTab = NewTab("≡  執行紀錄", Color.FromArgb(88, 103, 128));
            mainTabs.TabPages.Add(groupsTab);
            mainTabs.TabPages.Add(singleTab);
            mainTabs.TabPages.Add(brightnessTab);
            mainTabs.TabPages.Add(quickSettingsTab);
            mainTabs.TabPages.Add(downloadTab);
            mainTabs.TabPages.Add(logTab);
            root.Controls.Add(mainTabs, 0, 2);

            BuildGroupsTab(groupsTab);
            BuildSingleTab(singleTab);
            BuildBrightnessTab(brightnessTab);
            BuildQuickSettingsTab(quickSettingsTab);
            BuildDownloadTab(downloadTab);
            BuildLogTab(logTab);
        }

        private void BuildGroupsTab(TabPage tab)
        {
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 420,
                SplitterWidth = 10,
                BackColor = Bg,
                FixedPanel = FixedPanel.Panel1
            };
            tab.Controls.Add(split);
            split.SizeChanged += delegate
            {
                if (split.Width < ScaleValue(760, currentDpiScale)) return;
                int desired = Math.Min(ScaleValue(350, currentDpiScale),
                    Math.Max(ScaleValue(300, currentDpiScale), split.Width - ScaleValue(560, currentDpiScale)));
                if (split.SplitterDistance != desired) split.SplitterDistance = desired;
            };

            Panel left = NewCard();
            left.Dock = DockStyle.Fill;
            left.Padding = new Padding(14);
            split.Panel1.Controls.Add(left);
            Label groupsLabel = NewSectionLabel("我的組合");
            groupsLabel.Dock = DockStyle.Top;
            left.Controls.Add(groupsLabel);

            TableLayoutPanel groupButtons = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 148,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Card,
            };
            groupButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            groupButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            groupButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34F));
            groupButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            groupButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            Button addGroup = NewButton("＋ 新增", true, 90);
            renameGroupButton = NewButton("編輯名稱", false, 110);
            deleteGroupButton = NewButton("刪除組合", false, 95);
            moveGroupUpButton = NewButton("↑ 上移", false, 95);
            moveGroupDownButton = NewButton("↓ 下移", false, 95);
            addGroup.Dock = DockStyle.Fill;
            renameGroupButton.Dock = DockStyle.Fill;
            deleteGroupButton.Dock = DockStyle.Fill;
            moveGroupUpButton.Dock = DockStyle.Fill;
            moveGroupDownButton.Dock = DockStyle.Fill;
            addGroup.Click += AddGroup;
            renameGroupButton.Click += RenameGroup;
            deleteGroupButton.Click += DeleteGroup;
            moveGroupUpButton.Click += delegate { MoveSelectedGroup(-1); };
            moveGroupDownButton.Click += delegate { MoveSelectedGroup(1); };
            groupButtons.Controls.Add(addGroup, 0, 0);
            groupButtons.SetColumnSpan(addGroup, 2);
            groupButtons.Controls.Add(renameGroupButton, 0, 1);
            groupButtons.Controls.Add(deleteGroupButton, 1, 1);
            groupButtons.Controls.Add(moveGroupUpButton, 0, 2);
            groupButtons.Controls.Add(moveGroupDownButton, 1, 2);
            left.Controls.Add(groupButtons);

            groupList = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Card2,
                ForeColor = TextColor,
                BorderStyle = BorderStyle.None,
                Font = new Font(Font.FontFamily, 10.5F),
                IntegralHeight = false,
                ItemHeight = 70,
                DrawMode = DrawMode.OwnerDrawFixed,
                HorizontalScrollbar = false
            };
            groupList.HandleCreated += delegate { SetWindowTheme(groupList.Handle, "DarkMode_Explorer", null); };
            groupList.DrawItem += DrawGroupListItem;
            groupNameToolTip = new ToolTip { InitialDelay = 300, ReshowDelay = 100, AutoPopDelay = 8000, ShowAlways = true };
            groupList.MouseMove += GroupListMouseMove;
            groupList.MouseLeave += delegate { lastGroupTooltipIndex = -1; groupNameToolTip.Hide(groupList); };
            groupList.SelectedIndexChanged += delegate { ShowSelectedGroup(); };
            groupList.DoubleClick += async delegate
            {
                if (groupList.SelectedItem != null) await InstallSelectedGroupAsync();
            };
            left.Controls.Add(groupList);
            groupList.BringToFront();

            Panel right = NewCard();
            right.Dock = DockStyle.Fill;
            right.Padding = new Padding(18);
            split.Panel2.Controls.Add(right);

            groupTitle = NewSectionLabel("請選擇安裝組合");
            groupTitle.Font = new Font(Font.FontFamily, 15F, FontStyle.Bold);
            groupTitle.Dock = DockStyle.Top;
            groupTitle.Height = 32;
            groupTitle.AutoEllipsis = true;
            groupHint = new Label
            {
                Text = "雙擊左側組合也可直接全部安裝",
                ForeColor = Muted,
                Dock = DockStyle.Top,
                Height = 30
            };
            right.Controls.Add(groupHint);
            right.Controls.Add(groupTitle);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Card,
                WrapContents = false
            };
            addGroupApksButton = NewButton("加入 APK", false, 105);
            removeGroupApkButton = NewButton("移除選取", false, 105);
            installGroupButton = NewButton("全部安裝", true, 120);
            addGroupApksButton.Click += AddApksToGroup;
            removeGroupApkButton.Click += RemoveSelectedApks;
            installGroupButton.Click += async delegate { await InstallSelectedGroupAsync(); };
            downgradeCheck = new CheckBox
            {
                Text = "允許降版安裝",
                ForeColor = Muted,
                AutoSize = true,
                Padding = new Padding(10, 10, 0, 0)
            };
            downgradeCheck.CheckedChanged += delegate
            {
                settings.AllowDowngrade = downgradeCheck.Checked;
                SaveSettings();
            };
            actions.Controls.Add(addGroupApksButton);
            actions.Controls.Add(removeGroupApkButton);
            actions.Controls.Add(installGroupButton);
            actions.Controls.Add(downgradeCheck);
            right.Controls.Add(actions);

            apkList = NewApkList();
            apkList.Dock = DockStyle.Fill;
            apkList.AllowDrop = true;
            apkList.DragEnter += GroupApkDragEnter;
            apkList.DragDrop += GroupApkDragDrop;
            right.Controls.Add(apkList);
            apkList.BringToFront();
        }

        private void BuildSingleTab(TabPage tab)
        {
            dropPanel = NewCard();
            dropPanel.Dock = DockStyle.Fill;
            dropPanel.Margin = new Padding(8);
            dropPanel.AllowDrop = true;
            dropPanel.Cursor = Cursors.Hand;
            dropPanel.DragEnter += ApkDragEnter;
            dropPanel.DragDrop += ApkDragDrop;
            dropPanel.Click += ChooseSingleApks;
            dropPanel.Paint += DrawQuickInstallDropPanel;
            tab.Padding = new Padding(8);
            tab.Controls.Add(dropPanel);
        }

        private void DrawQuickInstallDropPanel(object sender, PaintEventArgs e)
        {
            Panel panel = (Panel)sender;
            Rectangle border = new Rectangle(20, 20, Math.Max(1, panel.ClientSize.Width - 41), Math.Max(1, panel.ClientSize.Height - 41));
            using (Pen pen = new Pen(quickInstalling ? Color.FromArgb(255, 190, 75) : Accent, 2F))
            {
                pen.DashStyle = DashStyle.Dash;
                e.Graphics.DrawRectangle(pen, border);
            }
            int centerY = panel.ClientSize.Height / 2;
            Rectangle titleBounds = new Rectangle(40, centerY - 70, Math.Max(1, panel.ClientSize.Width - 80), 64);
            Rectangle hintBounds = new Rectangle(40, centerY + 4, Math.Max(1, panel.ClientSize.Width - 80), 80);
            using (Font titleFont = new Font(Font.FontFamily, 20F, FontStyle.Bold))
            using (Font hintFont = new Font(Font.FontFamily, 11F, FontStyle.Regular))
            {
                DrawSmoothText(e.Graphics, quickInstalling ? "正在安裝 APK..." : "把一個或多個 APK 拖到這裡", titleFont,
                    quickInstalling ? Color.FromArgb(255, 190, 75) : TextColor, titleBounds, StringAlignment.Center, StringAlignment.Center, false);
                DrawSmoothText(e.Graphics, quickInstalling ? "請勿拔除 USB，完成後會顯示安裝結果" : "放開後立即開始安裝\n也可按一下手動選擇 APK",
                    hintFont, Muted, hintBounds, StringAlignment.Center, StringAlignment.Near, false);
            }
        }

        private void BuildLogTab(TabPage tab)
        {
            Panel card = NewCard();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(16);
            tab.Controls.Add(card);
            Button clear = NewButton("清除紀錄", false, 100);
            clear.Dock = DockStyle.Bottom;
            clear.Click += delegate { logBox.Clear(); };
            card.Controls.Add(clear);
            logBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(15, 18, 24),
                ForeColor = Color.FromArgb(201, 211, 225),
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9.5F),
                Dock = DockStyle.Fill
            };
            card.Controls.Add(logBox);
            logBox.BringToFront();
        }

        private void BuildBrightnessTab(TabPage tab)
        {
            Panel outer = NewCard();
            outer.Dock = DockStyle.Fill;
            outer.Padding = new Padding(16);
            tab.Controls.Add(outer);

            Label title = new Label
            {
                Text = "設備亮度調整",
                ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 17F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 34
            };
            Label hint = new Label
            {
                Text = "拖動滑桿、輸入數值，或使用鍵盤 − / + 與滑鼠滾輪；變更後會立即套用。",
                ForeColor = Muted,
                Dock = DockStyle.Top,
                Height = 32
            };
            outer.Controls.Add(hint);
            outer.Controls.Add(title);

            Panel brightnessViewport = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Card,
                Margin = new Padding(0)
            };
            brightnessViewport.HandleCreated += delegate { SetWindowTheme(brightnessViewport.Handle, "DarkMode_Explorer", null); };
            outer.Controls.Add(brightnessViewport);

            Panel controlCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 266,
                BackColor = Card2,
                Padding = new Padding(14)
            };
            brightnessViewport.Controls.Add(controlCard);

            brightnessValueLabel = new Label
            {
                Text = "—",
                ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 34F, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 52
            };
            controlCard.Controls.Add(brightnessValueLabel);

            brightnessTrackBar = new TrackBar
            {
                Minimum = 0,
                Maximum = 255,
                TickFrequency = 25,
                SmallChange = 1,
                LargeChange = 10,
                Value = 128,
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = Card2
            };
            brightnessTrackBar.ValueChanged += BrightnessTrackBarChanged;
            controlCard.Controls.Add(brightnessTrackBar);
            brightnessTrackBar.BringToFront();

            FlowLayoutPanel valueControls = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Card2,
                Padding = new Padding(0, 5, 0, 0)
            };
            Button minusButton = NewButton("−", false, 58);
            minusButton.Font = new Font(Font.FontFamily, 16F, FontStyle.Bold);
            minusButton.Click += delegate { ChangeBrightnessBy(-BrightnessStep()); };
            brightnessNumber = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 255,
                Value = 128,
                Width = 150,
                Height = 38,
                Font = new Font(Font.FontFamily, 14F, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                BackColor = Color.FromArgb(19, 24, 32),
                ForeColor = TextColor,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(8, 4, 8, 4)
            };
            brightnessNumber.ValueChanged += BrightnessNumberChanged;
            Button plusButton = NewButton("＋", false, 58);
            plusButton.Font = new Font(Font.FontFamily, 16F, FontStyle.Bold);
            plusButton.Click += delegate { ChangeBrightnessBy(BrightnessStep()); };
            brightnessRangeLabel = new Label
            {
                Text = "目前範圍 0–255，每次按鍵調整 1",
                ForeColor = Muted,
                AutoSize = true,
                Padding = new Padding(14, 12, 0, 0)
            };
            valueControls.Controls.Add(minusButton);
            valueControls.Controls.Add(brightnessNumber);
            valueControls.Controls.Add(plusButton);
            valueControls.Controls.Add(brightnessRangeLabel);
            controlCard.Controls.Add(valueControls);
            valueControls.BringToFront();

            brightnessDisableAutoCheck = new CheckBox
            {
                Text = "調整時自動關閉「自動亮度」（建議）",
                Checked = true,
                ForeColor = TextColor,
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(4, 6, 0, 0)
            };
            controlCard.Controls.Add(brightnessDisableAutoCheck);
            brightnessDisableAutoCheck.BringToFront();

            brightnessStatusLabel = new Label
            {
                Text = "尚未讀取設備亮度",
                ForeColor = Muted,
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(4, 4, 0, 0)
            };
            controlCard.Controls.Add(brightnessStatusLabel);
            brightnessStatusLabel.BringToFront();

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Card
            };
            readBrightnessButton = NewButton("讀取目前亮度", false, 142);
            readBrightnessButton.Click += async delegate { await ReadBrightnessAsync(); };
            applyBrightnessButton = NewButton("立即套用", true, 112);
            applyBrightnessButton.Click += async delegate
            {
                brightnessUpdateTimer.Stop();
                brightnessPendingValue = (int)brightnessNumber.Value;
                await ApplyBrightnessAsync();
            };
            actions.Controls.Add(readBrightnessButton);
            actions.Controls.Add(applyBrightnessButton);
            outer.Controls.Add(actions);

            brightnessUpdateTimer = new Timer();
            brightnessUpdateTimer.Interval = 180;
            brightnessUpdateTimer.Tick += async delegate
            {
                brightnessUpdateTimer.Stop();
                await ApplyBrightnessAsync();
            };
        }

        public bool PreFilterMessage(ref Message message)
        {
            const int WmKeyDown = 0x0100;
            const int WmMouseWheel = 0x020A;
            if (Form.ActiveForm != this || mainTabs == null || brightnessTabPage == null ||
                mainTabs.SelectedTab != brightnessTabPage || brightnessNumber == null) return false;

            if (message.Msg == WmKeyDown)
            {
                Keys modifiers = Control.ModifierKeys;
                if ((modifiers & (Keys.Control | Keys.Alt)) != Keys.None) return false;
                Keys key = (Keys)message.WParam.ToInt32();
                bool increase = key == Keys.Add || (key == Keys.Oemplus && (modifiers & Keys.Shift) == Keys.Shift);
                bool decrease = key == Keys.Subtract || (key == Keys.OemMinus && (modifiers & Keys.Shift) == Keys.None);
                if (!increase && !decrease) return false;
                ChangeBrightnessBy(increase ? BrightnessStep() : -BrightnessStep());
                return true;
            }

            if (message.Msg == WmMouseWheel)
            {
                Rectangle pageBounds = brightnessTabPage.RectangleToScreen(brightnessTabPage.ClientRectangle);
                if (!pageBounds.Contains(Control.MousePosition)) return false;
                int delta = (short)((message.WParam.ToInt64() >> 16) & 0xFFFF);
                if (delta == 0) return false;
                int notches = Math.Max(1, Math.Abs(delta) / SystemInformation.MouseWheelScrollDelta);
                ChangeBrightnessBy((delta > 0 ? 1 : -1) * BrightnessStep() * notches);
                return true;
            }
            return false;
        }

        private void BrightnessTrackBarChanged(object sender, EventArgs e)
        {
            if (loadingBrightness) return;
            SetBrightnessControls(brightnessTrackBar.Value, true);
        }

        private void BrightnessNumberChanged(object sender, EventArgs e)
        {
            if (loadingBrightness) return;
            SetBrightnessControls((int)brightnessNumber.Value, true);
        }

        private void ChangeBrightnessBy(int amount)
        {
            int value = Math.Max(0, Math.Min(brightnessDetectedMaximum, (int)brightnessNumber.Value + amount));
            SetBrightnessControls(value, true);
        }

        private int BrightnessStep()
        {
            return 1;
        }

        private void SetBrightnessMaximum(int maximum)
        {
            maximum = Math.Max(1, Math.Min(1000000, maximum));
            int current = brightnessNumber == null ? 0 : (int)brightnessNumber.Value;
            if (maximum < current) maximum = current;
            brightnessDetectedMaximum = maximum;
            loadingBrightness = true;
            brightnessTrackBar.Maximum = maximum;
            brightnessTrackBar.TickFrequency = Math.Max(1, maximum / 10);
            brightnessTrackBar.LargeChange = Math.Max(1, maximum / 20);
            brightnessNumber.Maximum = maximum;
            loadingBrightness = false;
            brightnessRangeLabel.Text = "目前範圍 0–" + maximum + "，每次按鍵調整 " + BrightnessStep();
        }

        private void SetBrightnessControls(int value, bool scheduleApply)
        {
            value = Math.Max(0, Math.Min(brightnessDetectedMaximum, value));
            loadingBrightness = true;
            brightnessTrackBar.Value = value;
            brightnessNumber.Value = value;
            brightnessValueLabel.Text = value.ToString();
            loadingBrightness = false;
            brightnessPendingValue = value;
            if (scheduleApply)
            {
                brightnessStatusLabel.Text = "準備套用亮度 " + value + "...";
                brightnessStatusLabel.ForeColor = Color.FromArgb(255, 190, 75);
                brightnessUpdateTimer.Stop();
                brightnessUpdateTimer.Start();
            }
        }

        private void BuildQuickSettingsTab(TabPage tab)
        {
            Panel outer = NewCard();
            outer.Dock = DockStyle.Fill;
            outer.Padding = new Padding(18);
            tab.Controls.Add(outer);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Card,
                ColumnCount = 1,
                RowCount = 5
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            outer.Controls.Add(root);

            Label title = new Label
            {
                Text = "快速功能設定",
                ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 17F, FontStyle.Bold),
                Dock = DockStyle.Fill
            };
            Label hint = new Label
            {
                Text = "套用常用設定，或直接執行音量、網址與截圖工具。",
                ForeColor = Muted,
                Dock = DockStyle.Fill
            };
            root.Controls.Add(title, 0, 0);
            root.Controls.Add(hint, 0, 1);

            TableLayoutPanel content = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 300,
                BackColor = Card,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 6, 0, 6)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Panel contentViewport = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Card,
                AutoScroll = true,
                Margin = new Padding(0)
            };
            contentViewport.HandleCreated += delegate { SetWindowTheme(contentViewport.Handle, "DarkMode_Explorer", null); };
            contentViewport.Controls.Add(content);
            root.Controls.Add(contentViewport, 0, 2);

            TableLayoutPanel settingsColumn = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Card,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 6, 0)
            };
            settingsColumn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            settingsColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            settingsColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 196));
            content.Controls.Add(settingsColumn, 0, 0);

            Panel brightnessCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Card2,
                Padding = new Padding(18),
                Margin = new Padding(0, 0, 0, 8)
            };
            autoBrightnessCheck = new CheckBox
            {
                Text = "開啟螢幕自動亮度調整",
                ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(18, 16)
            };
            Label brightnessHint = new Label
            {
                Text = "取消勾選後套用，即關閉自動亮度並保留目前亮度。",
                ForeColor = Muted,
                AutoSize = true,
                Location = new Point(42, 52)
            };
            brightnessCard.Controls.Add(autoBrightnessCheck);
            brightnessCard.Controls.Add(brightnessHint);
            settingsColumn.Controls.Add(brightnessCard, 0, 0);

            Panel timeoutCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Card2,
                Padding = new Padding(18),
                Margin = new Padding(0)
            };
            Label timeoutTitle = new Label
            {
                Text = "螢幕自動關閉時間（擇一）",
                ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(18, 14)
            };
            timeoutTenMinutesCheck = new CheckBox
            {
                Text = "10 分鐘後關閉螢幕",
                ForeColor = TextColor,
                AutoSize = true,
                Location = new Point(22, 52)
            };
            timeoutNeverCheck = new CheckBox
            {
                Text = "不自動關閉螢幕",
                ForeColor = TextColor,
                AutoSize = true,
                Location = new Point(22, 82)
            };
            Label neverHint = new Label
            {
                Text = "設為系統可接受的最長逾時時間。",
                ForeColor = Muted,
                AutoSize = true,
                Location = new Point(43, 111)
            };
            stayOnWhileChargingCheck = new CheckBox
            {
                Text = "充電時保持螢幕不關閉",
                ForeColor = TextColor,
                AutoSize = true,
                Location = new Point(22, 140)
            };
            timeoutTenMinutesCheck.CheckedChanged += delegate
            {
                if (!loadingQuickSettings && timeoutTenMinutesCheck.Checked)
                {
                    loadingQuickSettings = true;
                    timeoutNeverCheck.Checked = false;
                    loadingQuickSettings = false;
                }
            };
            timeoutNeverCheck.CheckedChanged += delegate
            {
                if (!loadingQuickSettings && timeoutNeverCheck.Checked)
                {
                    loadingQuickSettings = true;
                    timeoutTenMinutesCheck.Checked = false;
                    loadingQuickSettings = false;
                }
            };
            timeoutCard.Controls.Add(timeoutTitle);
            timeoutCard.Controls.Add(timeoutTenMinutesCheck);
            timeoutCard.Controls.Add(timeoutNeverCheck);
            timeoutCard.Controls.Add(neverHint);
            timeoutCard.Controls.Add(stayOnWhileChargingCheck);
            settingsColumn.Controls.Add(timeoutCard, 0, 1);

            TableLayoutPanel toolsColumn = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Card,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(6, 0, 0, 0)
            };
            toolsColumn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            toolsColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            toolsColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 98));
            toolsColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            content.Controls.Add(toolsColumn, 1, 0);

            Panel volumeCard = new Panel { Dock = DockStyle.Fill, BackColor = Card2, Margin = new Padding(0, 0, 0, 8) };
            TableLayoutPanel volumeLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
                Padding = new Padding(10, 6, 10, 6), BackColor = Card2
            };
            volumeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
            volumeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
            volumeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
            volumeLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label volumeTitle = new Label
            {
                Text = "媒體音量", ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 11.5F, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            };
            volumeMinimumButton = NewButton("調到最低", true, 112);
            volumeMinimumButton.MinimumSize = new Size(112, 36);
            volumeMinimumButton.Anchor = AnchorStyles.None;
            volumeMinimumButton.Click += async delegate { await SetMediaVolumeExtremeAsync(false); };
            volumeMaximumButton = NewButton("調到最高", true, 112);
            volumeMaximumButton.MinimumSize = new Size(112, 36);
            volumeMaximumButton.Anchor = AnchorStyles.None;
            volumeMaximumButton.Click += async delegate { await SetMediaVolumeExtremeAsync(true); };
            volumeLayout.Controls.Add(volumeTitle, 0, 0);
            volumeLayout.Controls.Add(volumeMinimumButton, 1, 0);
            volumeLayout.Controls.Add(volumeMaximumButton, 2, 0);
            volumeCard.Controls.Add(volumeLayout);
            toolsColumn.Controls.Add(volumeCard, 0, 0);

            Panel urlCard = new Panel { Dock = DockStyle.Fill, BackColor = Card2, Margin = new Padding(0, 0, 0, 8) };
            TableLayoutPanel urlLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                Padding = new Padding(10, 5, 10, 5), BackColor = Card2
            };
            urlLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            urlLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            urlLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            Label urlTitle = new Label
            {
                Text = "在手機開啟指定網址", ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 11.5F, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            };
            TableLayoutPanel urlActions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                Margin = new Padding(0), BackColor = Card2
            };
            urlActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            urlActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            urlActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            urlTextBox = new TextBox
            {
                Text = "https://", BackColor = Card, ForeColor = TextColor,
                BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill,
                Margin = new Padding(0, 7, 6, 7)
            };
            openUrlButton = NewButton("開啟網址", true, 100);
            openUrlButton.MinimumSize = new Size(100, 36);
            openUrlButton.Anchor = AnchorStyles.None;
            openUrlButton.Click += async delegate { await OpenUrlOnDeviceAsync(); };
            urlTextBox.KeyDown += async delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await OpenUrlOnDeviceAsync();
                }
            };
            urlActions.Controls.Add(urlTextBox, 0, 0);
            urlActions.Controls.Add(openUrlButton, 1, 0);
            urlLayout.Controls.Add(urlTitle, 0, 0);
            urlLayout.Controls.Add(urlActions, 0, 1);
            urlCard.Controls.Add(urlLayout);
            toolsColumn.Controls.Add(urlCard, 0, 1);

            Panel screenshotCard = new Panel { Dock = DockStyle.Fill, BackColor = Card2, Margin = new Padding(0) };
            TableLayoutPanel screenshotLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                Padding = new Padding(10, 6, 10, 6), BackColor = Card2
            };
            screenshotLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            screenshotLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            screenshotLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            TableLayoutPanel screenshotText = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                Margin = new Padding(0), BackColor = Card2
            };
            screenshotText.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            screenshotText.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
            screenshotText.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
            Label screenshotTitle = new Label
            {
                Text = "手機畫面截圖", ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 11.5F, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft
            };
            Label screenshotHint = new Label
            {
                Text = "擷取目前畫面並存成 PNG", ForeColor = Muted,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft
            };
            screenshotButton = NewButton("截圖並儲存", true, 126);
            screenshotButton.MinimumSize = new Size(126, 36);
            screenshotButton.Anchor = AnchorStyles.None;
            screenshotButton.Click += async delegate { await CaptureScreenshotAsync(); };
            screenshotText.Controls.Add(screenshotTitle, 0, 0);
            screenshotText.Controls.Add(screenshotHint, 0, 1);
            screenshotLayout.Controls.Add(screenshotText, 0, 0);
            screenshotLayout.Controls.Add(screenshotButton, 1, 0);
            screenshotCard.Controls.Add(screenshotLayout);
            toolsColumn.Controls.Add(screenshotCard, 0, 2);

            quickSettingsStateLabel = new Label
            {
                Text = "尚未讀取手機目前設定",
                ForeColor = Muted,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(quickSettingsStateLabel, 0, 3);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Card,
                WrapContents = false
            };
            readQuickSettingsButton = NewButton("讀取目前設定", false, 132);
            readQuickSettingsButton.Click += async delegate { await ReadQuickSettingsAsync(); };
            applyQuickSettingsButton = NewButton("套用勾選設定", true, 132);
            applyQuickSettingsButton.Click += async delegate { await ApplyQuickSettingsAsync(); };
            actions.Controls.Add(readQuickSettingsButton);
            actions.Controls.Add(applyQuickSettingsButton);
            root.Controls.Add(actions, 0, 4);
        }

        private void BuildDownloadTab(TabPage tab)
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(28, 24, 28, 24),
                BackColor = Card,
                ColumnCount = 1,
                RowCount = 4
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tab.Controls.Add(root);

            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = Card };
            Label title = new Label
            {
                Text = "快速下載手機資料",
                ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(0, 0)
            };
            Label hint = new Label
            {
                Text = "下載 DCIM、Pictures 與 Picture 內的所有檔案，保留資料夾結構並壓縮成 ZIP。",
                ForeColor = Muted,
                AutoSize = true,
                Location = new Point(2, 42)
            };
            header.Controls.Add(title);
            header.Controls.Add(hint);
            root.Controls.Add(header, 0, 0);

            Panel destinationCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Card2,
                Padding = new Padding(18, 12, 18, 14),
                Margin = new Padding(0, 0, 0, 12)
            };
            Label destinationLabel = new Label
            {
                Text = "電腦儲存位置",
                Dock = DockStyle.Top,
                Height = 31,
                ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold)
            };
            TableLayoutPanel destinationRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            destinationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            destinationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
            downloadFolderTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Text = settings.DownloadFolder ?? "",
                BackColor = Bg,
                ForeColor = TextColor,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 4, 10, 4)
            };
            browseDownloadFolderButton = NewButton("選擇位置", false, 132);
            browseDownloadFolderButton.Dock = DockStyle.Fill;
            browseDownloadFolderButton.Margin = new Padding(0, 2, 0, 2);
            browseDownloadFolderButton.Click += BrowseDownloadFolder;
            destinationRow.Controls.Add(downloadFolderTextBox, 0, 0);
            destinationRow.Controls.Add(browseDownloadFolderButton, 1, 0);
            destinationCard.Controls.Add(destinationRow);
            destinationCard.Controls.Add(destinationLabel);
            root.Controls.Add(destinationCard, 0, 1);

            Panel limitCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Card2,
                Padding = new Padding(18, 14, 18, 14),
                Margin = new Padding(0, 0, 0, 12)
            };
            TableLayoutPanel limitLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                Margin = new Padding(0)
            };
            limitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            limitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            limitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
            limitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            limitLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            limitLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            skipLargeDownloadCheck = new CheckBox
            {
                Text = "略過超過指定大小的單一檔案",
                Checked = settings.SkipLargeDownloadFiles,
                ForeColor = TextColor,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            Label sizeLabel = new Label
            {
                Text = "檔案大小過濾設定",
                ForeColor = TextColor,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            maxDownloadSizeNumber = new NumericUpDown
            {
                Minimum = 0.1M,
                Maximum = 1024M,
                DecimalPlaces = 1,
                Increment = 0.5M,
                Value = Math.Max(0.1M, Math.Min(1024M, settings.MaxDownloadFileSizeGb <= 0 ? 2M : settings.MaxDownloadFileSizeGb)),
                BackColor = Bg,
                ForeColor = TextColor,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Right,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 8, 8),
                Enabled = settings.SkipLargeDownloadFiles
            };
            Label unitLabel = new Label
            {
                Text = "GB",
                ForeColor = Muted,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            Label limitHint = new Label
            {
                Text = "程式會先讀取手機端檔案大小；超過上限的檔案不會傳輸，也不會放入壓縮檔。",
                ForeColor = Muted,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            limitLayout.Controls.Add(skipLargeDownloadCheck, 0, 0);
            limitLayout.Controls.Add(sizeLabel, 1, 0);
            limitLayout.Controls.Add(maxDownloadSizeNumber, 2, 0);
            limitLayout.Controls.Add(unitLabel, 3, 0);
            limitLayout.Controls.Add(limitHint, 0, 1);
            limitLayout.SetColumnSpan(limitHint, 4);
            limitCard.Controls.Add(limitLayout);
            root.Controls.Add(limitCard, 0, 2);

            Panel actionCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Card2,
                Padding = new Padding(18),
                Margin = new Padding(0)
            };
            TableLayoutPanel actionLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            actionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            downloadStatusLabel = new Label
            {
                Text = "準備就緒",
                ForeColor = Muted,
                Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            downloadProgressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous,
                Margin = new Padding(0, 4, 0, 4)
            };
            Label actionHint = new Label
            {
                Text = "壓縮檔名稱會使用手機型號與日期時間，例如：Pixel_9_20260716-153000.zip",
                ForeColor = Muted,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            FlowLayoutPanel actionButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Card2,
                Padding = new Padding(0, 8, 0, 0)
            };
            startDownloadButton = NewButton("開始下載並打包", true, 180);
            startDownloadButton.Click += async delegate { await DownloadPhoneDataAsync(); };
            actionButtons.Controls.Add(startDownloadButton);
            actionLayout.Controls.Add(downloadStatusLabel, 0, 0);
            actionLayout.Controls.Add(downloadProgressBar, 0, 1);
            actionLayout.Controls.Add(actionHint, 0, 2);
            actionLayout.Controls.Add(actionButtons, 0, 3);
            actionCard.Controls.Add(actionLayout);
            root.Controls.Add(actionCard, 0, 3);

            skipLargeDownloadCheck.CheckedChanged += delegate
            {
                maxDownloadSizeNumber.Enabled = skipLargeDownloadCheck.Checked;
                settings.SkipLargeDownloadFiles = skipLargeDownloadCheck.Checked;
                SaveSettings();
            };
            maxDownloadSizeNumber.ValueChanged += delegate
            {
                settings.MaxDownloadFileSizeGb = maxDownloadSizeNumber.Value;
                SaveSettings();
            };
        }

        private Panel NewCard()
        {
            return new Panel { BackColor = Card, Margin = new Padding(0, 0, 0, 12) };
        }

        private TabPage NewTab(string text, Color accent)
        {
            return new TabPage(text) { BackColor = Bg, ForeColor = TextColor, Padding = new Padding(0, 10, 0, 0), Tag = accent };
        }

        private Label NewSectionLabel(string text)
        {
            return new Label
            {
                Text = text,
                ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 11.5F, FontStyle.Bold),
                Height = 35
            };
        }

        private static void ApplySmoothTextRendering(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                Label label = control as Label;
                if (label != null) label.UseCompatibleTextRendering = true;
                Button button = control as Button;
                if (button != null) button.UseCompatibleTextRendering = true;
                if (control.HasChildren) ApplySmoothTextRendering(control);
            }
        }

        private void CaptureDpiMetrics(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                DpiMetric metric = new DpiMetric
                {
                    Bounds = control.Bounds,
                    Padding = control.Padding,
                    Margin = control.Margin,
                    MinimumSize = control.MinimumSize,
                    MaximumSize = control.MaximumSize,
                    Dock = control.Dock,
                    AutoSize = control.AutoSize
                };
                ModernTabControl tab = control as ModernTabControl;
                if (tab != null) metric.TabItemSize = tab.ItemSize;
                ListBox listBox = control as ListBox;
                if (listBox != null) metric.ListBoxItemHeight = listBox.ItemHeight;
                ListView listView = control as ListView;
                if (listView != null)
                {
                    metric.ListViewColumnWidths = new int[listView.Columns.Count];
                    for (int i = 0; i < listView.Columns.Count; i++) metric.ListViewColumnWidths[i] = listView.Columns[i].Width;
                }
                TableLayoutPanel table = control as TableLayoutPanel;
                if (table != null)
                {
                    metric.TableRowHeights = new float[table.RowStyles.Count];
                    for (int i = 0; i < table.RowStyles.Count; i++) metric.TableRowHeights[i] = table.RowStyles[i].Height;
                    metric.TableColumnWidths = new float[table.ColumnStyles.Count];
                    for (int i = 0; i < table.ColumnStyles.Count; i++) metric.TableColumnWidths[i] = table.ColumnStyles[i].Width;
                }
                dpiMetrics[control] = metric;
                if (control.HasChildren) CaptureDpiMetrics(control);
            }
        }

        private void ApplyDpiLayout(int dpi)
        {
            float scale = Math.Max(1F, dpi / 96F);
            currentDpiScale = scale;
            SuspendLayout();
            MinimumSize = ScaleSize(new Size(1100, 840), scale);
            foreach (KeyValuePair<Control, DpiMetric> pair in dpiMetrics)
            {
                Control control = pair.Key;
                DpiMetric metric = pair.Value;
                if (control.IsDisposed) continue;
                control.Padding = ScalePadding(metric.Padding, scale);
                control.Margin = ScalePadding(metric.Margin, scale);
                control.MinimumSize = ScaleSize(metric.MinimumSize, scale);
                control.MaximumSize = ScaleSize(metric.MaximumSize, scale);
                if (metric.Dock == DockStyle.None)
                {
                    Point location = new Point(ScaleValue(metric.Bounds.X, scale), ScaleValue(metric.Bounds.Y, scale));
                    if (metric.AutoSize) control.Location = location;
                    else control.Bounds = new Rectangle(location, ScaleSize(metric.Bounds.Size, scale));
                }
                else if (metric.Dock == DockStyle.Top || metric.Dock == DockStyle.Bottom)
                {
                    control.Height = ScaleValue(metric.Bounds.Height, scale);
                }
                else if (metric.Dock == DockStyle.Left || metric.Dock == DockStyle.Right)
                {
                    control.Width = ScaleValue(metric.Bounds.Width, scale);
                }

                ModernTabControl tab = control as ModernTabControl;
                if (tab != null && metric.TabItemSize.Width > 0) tab.ItemSize = ScaleSize(metric.TabItemSize, scale);
                ListBox listBox = control as ListBox;
                if (listBox != null && metric.ListBoxItemHeight > 0) listBox.ItemHeight = ScaleValue(metric.ListBoxItemHeight, scale);
                ListView listView = control as ListView;
                if (listView != null && metric.ListViewColumnWidths != null)
                {
                    for (int i = 0; i < listView.Columns.Count && i < metric.ListViewColumnWidths.Length; i++)
                        listView.Columns[i].Width = ScaleValue(metric.ListViewColumnWidths[i], scale);
                }
                TableLayoutPanel table = control as TableLayoutPanel;
                if (table != null)
                {
                    if (metric.TableRowHeights != null)
                    {
                        for (int i = 0; i < table.RowStyles.Count && i < metric.TableRowHeights.Length; i++)
                            if (table.RowStyles[i].SizeType == SizeType.Absolute) table.RowStyles[i].Height = metric.TableRowHeights[i] * scale;
                    }
                    if (metric.TableColumnWidths != null)
                    {
                        for (int i = 0; i < table.ColumnStyles.Count && i < metric.TableColumnWidths.Length; i++)
                            if (table.ColumnStyles[i].SizeType == SizeType.Absolute) table.ColumnStyles[i].Width = metric.TableColumnWidths[i] * scale;
                    }
                }
            }
            ResumeLayout(true);
            PerformLayout();
            foreach (Control control in dpiMetrics.Keys)
            {
                ListView list = control as ListView;
                if (list != null) ResizeApkListColumns(list);
                control.Invalidate();
            }
        }

        private static int ScaleValue(int value, float scale)
        {
            return value == 0 ? 0 : Math.Max(1, (int)Math.Round(value * scale));
        }

        private static Size ScaleSize(Size size, float scale)
        {
            return new Size(ScaleValue(size.Width, scale), ScaleValue(size.Height, scale));
        }

        private static Padding ScalePadding(Padding padding, float scale)
        {
            return new Padding(ScaleValue(padding.Left, scale), ScaleValue(padding.Top, scale),
                ScaleValue(padding.Right, scale), ScaleValue(padding.Bottom, scale));
        }

        private void RestoreWindowSize()
        {
            float scale = Math.Max(1F, currentDpiScale);
            Screen screen = Screen.FromHandle(Handle);
            Rectangle work = screen.WorkingArea;
            int margin = ScaleValue(24, scale);
            int maximumWidth = Math.Max(640, work.Width - margin * 2);
            int maximumHeight = Math.Max(520, work.Height - margin * 2);
            int minimumWidth = Math.Min(ScaleValue(1100, scale), maximumWidth);
            int minimumHeight = Math.Min(ScaleValue(840, scale), maximumHeight);
            int requestedWidth = ScaleValue(settings.WindowWidth > 0 ? settings.WindowWidth : 1200, scale);
            int requestedHeight = ScaleValue(settings.WindowHeight > 0 ? settings.WindowHeight : 960, scale);
            int width = Math.Max(minimumWidth, Math.Min(maximumWidth, requestedWidth));
            int height = Math.Max(minimumHeight, Math.Min(maximumHeight, requestedHeight));
            MinimumSize = new Size(minimumWidth, minimumHeight);
            StartPosition = FormStartPosition.Manual;
            Size = new Size(width, height);
            Location = new Point(work.Left + Math.Max(0, (work.Width - width) / 2),
                work.Top + Math.Max(0, (work.Height - height) / 2));
            if (settings.WindowMaximized) WindowState = FormWindowState.Maximized;
        }

        private void CaptureWindowSize()
        {
            Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            float scale = Math.Max(1F, currentDpiScale);
            settings.WindowWidth = Math.Max(1100, (int)Math.Round(bounds.Width / scale));
            settings.WindowHeight = Math.Max(840, (int)Math.Round(bounds.Height / scale));
            settings.WindowMaximized = WindowState == FormWindowState.Maximized;
        }

        private static void DrawSmoothText(Graphics graphics, string text, Font font, Color color, Rectangle bounds,
            StringAlignment horizontal, StringAlignment vertical, bool noWrap)
        {
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (SolidBrush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = horizontal;
                format.LineAlignment = vertical;
                format.Trimming = noWrap ? StringTrimming.EllipsisCharacter : StringTrimming.EllipsisWord;
                if (noWrap) format.FormatFlags = StringFormatFlags.NoWrap;
                graphics.DrawString(text ?? "", font, brush, bounds, format);
            }
        }

        private Button NewButton(string text, bool primary, int width)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Card2,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(5)
            };
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(60, 70, 86);
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(103, 171, 255) : Color.FromArgb(48, 58, 74);
            button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(55, 125, 218) : Color.FromArgb(27, 33, 43);
            button.Resize += delegate { ApplyRoundedRegion(button, 8); };
            ApplyRoundedRegion(button, 8);
            return button;
        }

        private static void ApplyRoundedRegion(Control control, int radius)
        {
            if (control.Width <= 0 || control.Height <= 0) return;
            Rectangle rect = new Rectangle(0, 0, control.Width, control.Height);
            int diameter = radius * 2;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
                path.AddArc(rect.Right - diameter - 1, rect.Top, diameter, diameter, 270, 90);
                path.AddArc(rect.Right - diameter - 1, rect.Bottom - diameter - 1, diameter, diameter, 0, 90);
                path.AddArc(rect.Left, rect.Bottom - diameter - 1, diameter, diameter, 90, 90);
                path.CloseFigure();
                Region old = control.Region;
                control.Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        private ListView NewApkList()
        {
            ListView list = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                HideSelection = false,
                BackColor = Card2,
                ForeColor = TextColor,
                BorderStyle = BorderStyle.None,
                Font = new Font(Font.FontFamily, 9.5F),
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            list.OwnerDraw = true;
            list.HandleCreated += delegate { SetWindowTheme(list.Handle, "DarkMode_Explorer", null); };
            list.DrawColumnHeader += DrawApkColumnHeader;
            list.DrawItem += delegate(object sender, DrawListViewItemEventArgs e) { };
            list.DrawSubItem += DrawApkSubItem;
            list.Columns.Add("APK 檔案", 280);
            list.Columns.Add("位置", 400);
            list.Columns.Add("狀態", 150);
            Panel headerCornerCover = new Panel
            {
                BackColor = Color.FromArgb(25, 31, 41),
                Enabled = false,
                TabStop = false
            };
            list.Controls.Add(headerCornerCover);
            Action positionHeaderCorner = delegate
            {
                int width = SystemInformation.VerticalScrollBarWidth + 3;
                int height = Math.Max(24, list.Font.Height + 10);
                headerCornerCover.SetBounds(Math.Max(0, list.ClientSize.Width - width), 0, width, height);
                headerCornerCover.BringToFront();
            };
            list.Resize += delegate
            {
                ResizeApkListColumns(list);
                positionHeaderCorner();
            };
            list.HandleCreated += delegate { list.BeginInvoke(positionHeaderCorner); };
            return list;
        }

        private void ResizeApkListColumns(ListView list)
        {
            if (list == null || list.Columns.Count < 3) return;
            int first = ScaleValue(280, currentDpiScale);
            int status = ScaleValue(150, currentDpiScale);
            // The final column must meet the dark scrollbar cover exactly. Leaving an
            // extra gap here exposes the native white header background as a square.
            int available = list.ClientSize.Width - first - status - SystemInformation.VerticalScrollBarWidth;
            list.Columns[0].Width = first;
            list.Columns[2].Width = status;
            if (available > ScaleValue(180, currentDpiScale)) list.Columns[1].Width = available;
        }

        private void DrawApkColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (SolidBrush background = new SolidBrush(Color.FromArgb(25, 31, 41)))
            using (Pen line = new Pen(Color.FromArgb(63, 75, 94)))
            {
                e.Graphics.FillRectangle(background, e.Bounds);
                e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
            Rectangle textRect = new Rectangle(e.Bounds.X + 12, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 16), e.Bounds.Height);
            using (Font headerFont = new Font(Font.FontFamily, 9.5F, FontStyle.Bold))
                DrawSmoothText(e.Graphics, e.Header.Text, headerFont, Color.FromArgb(198, 209, 225), textRect,
                    StringAlignment.Near, StringAlignment.Center, true);
        }

        private void DrawApkSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            bool selected = e.Item.Selected;
            Color rowColor = selected ? Color.FromArgb(46, 91, 151) :
                (e.ItemIndex % 2 == 0 ? Color.FromArgb(34, 41, 53) : Color.FromArgb(30, 37, 48));
            using (SolidBrush brush = new SolidBrush(rowColor)) e.Graphics.FillRectangle(brush, e.Bounds);
            Color textColor = selected ? Color.White : TextColor;
            if (!selected && e.ColumnIndex == 2)
            {
                string status = e.SubItem.Text ?? "";
                if (status.Contains("成功")) textColor = Green;
                else if (status.Contains("失敗") || status.Contains("不存在")) textColor = Red;
                else if (status.Contains("安裝中")) textColor = Color.FromArgb(255, 190, 75);
            }
            Rectangle textRect = new Rectangle(e.Bounds.X + 12, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 16), e.Bounds.Height);
            DrawSmoothText(e.Graphics, e.SubItem.Text, ((ListView)sender).Font, textColor, textRect,
                StringAlignment.Near, StringAlignment.Center, true);
        }

        private void DrawGroupListItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= groupList.Items.Count) return;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color fill = selected ? Color.FromArgb(43, 82, 133) :
                (e.Index % 2 == 0 ? Color.FromArgb(34, 41, 53) : Color.FromArgb(30, 37, 48));
            using (SolidBrush brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, e.Bounds);
            if (selected)
            {
                using (SolidBrush accentBrush = new SolidBrush(Accent))
                    e.Graphics.FillRectangle(accentBrush, e.Bounds.Left, e.Bounds.Top + 6, 4, e.Bounds.Height - 12);
            }
            ApkGroup group = groupList.Items[e.Index] as ApkGroup;
            string name = group == null ? groupList.Items[e.Index].ToString() : group.Name;
            string count = group == null ? "" : (group.IsFolderGroup ? "資料夾同步　" : "") + group.Apks.Count + " 個 APK";
            int textLeft = e.Bounds.X + ScaleValue(14, currentDpiScale);
            if (group != null && group.IsFolderGroup)
            {
                int iconSize = ScaleValue(19, currentDpiScale);
                int iconTop = e.Bounds.Y + ScaleValue(15, currentDpiScale);
                DrawFolderIcon(e.Graphics, new Rectangle(textLeft, iconTop, iconSize, iconSize));
                textLeft += iconSize + ScaleValue(9, currentDpiScale);
            }
            Rectangle nameRect = new Rectangle(textLeft, e.Bounds.Y + 6, Math.Max(20, e.Bounds.Right - textLeft - 14), 40);
            Rectangle countRect = new Rectangle(textLeft, e.Bounds.Y + 46, Math.Max(20, e.Bounds.Right - textLeft - 14), 18);
            DrawSmoothText(e.Graphics, name, groupList.Font, selected ? Color.White : TextColor, nameRect,
                StringAlignment.Near, StringAlignment.Near, false);
            using (Font countFont = new Font(groupList.Font.FontFamily, 8.5F))
                DrawSmoothText(e.Graphics, count, countFont, selected ? Color.White : Muted, countRect,
                    StringAlignment.Near, StringAlignment.Center, true);
        }

        private static void DrawFolderIcon(Graphics graphics, Rectangle bounds)
        {
            if (bounds.Width < 4 || bounds.Height < 4) return;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Color.FromArgb(255, 190, 75);
            Color edge = Color.FromArgb(205, 132, 33);
            int tabWidth = Math.Max(3, bounds.Width / 2);
            int tabHeight = Math.Max(2, bounds.Height / 4);
            Rectangle tab = new Rectangle(bounds.Left + 1, bounds.Top + 1, tabWidth, tabHeight + 2);
            Rectangle body = new Rectangle(bounds.Left, bounds.Top + tabHeight, bounds.Width, bounds.Height - tabHeight);
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(edge, 1F))
            {
                graphics.FillRectangle(brush, tab);
                graphics.FillRectangle(brush, body);
                graphics.DrawRectangle(pen, body.X, body.Y, Math.Max(1, body.Width - 1), Math.Max(1, body.Height - 1));
            }
        }

        private void GroupListMouseMove(object sender, MouseEventArgs e)
        {
            int index = groupList.IndexFromPoint(e.Location);
            if (index == lastGroupTooltipIndex) return;
            lastGroupTooltipIndex = index;
            groupNameToolTip.Hide(groupList);
            if (index >= 0 && index < groupList.Items.Count)
            {
                ApkGroup group = groupList.Items[index] as ApkGroup;
                string text = group == null ? groupList.Items[index].ToString() : group.Name + "（" +
                    (group.IsFolderGroup ? "資料夾同步，" : "") + group.Apks.Count + " 個 APK）";
                groupNameToolTip.Show(text, groupList, e.X + 16, e.Y + 20, 8000);
            }
        }

        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(settingsFile))
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    AppSettings loaded = serializer.Deserialize<AppSettings>(File.ReadAllText(settingsFile, Encoding.UTF8));
                    if (loaded != null)
                    {
                        if (loaded.Groups == null) loaded.Groups = new List<ApkGroup>();
                        if (loaded.GroupOrder == null) loaded.GroupOrder = new List<string>();
                        if (String.IsNullOrWhiteSpace(loaded.DownloadFolder))
                            loaded.DownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Android手機資料下載");
                        if (loaded.MaxDownloadFileSizeGb <= 0) loaded.MaxDownloadFileSizeGb = 2M;
                        foreach (ApkGroup group in loaded.Groups)
                        {
                            if (group.Apks == null) group.Apks = new List<ApkEntry>();
                            if (String.IsNullOrWhiteSpace(group.Id)) group.Id = Guid.NewGuid().ToString("N");
                        }
                        return loaded;
                    }
                }
            }
            catch { }
            return new AppSettings();
        }

        private void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsFile));
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                File.WriteAllText(settingsFile, serializer.Serialize(settings), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log("設定儲存失敗：" + ex.Message);
            }
        }

        private void Log(string text)
        {
            if (logBox == null) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), text);
                return;
            }
            logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
        }

        private string FindAdb()
        {
            List<string> candidates = new List<string>();
            if (!String.IsNullOrWhiteSpace(settings.AdbPath)) candidates.Add(settings.AdbPath);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            candidates.Add(Path.Combine(baseDir, "adb.exe"));
            candidates.Add(Path.Combine(baseDir, "platform-tools", "adb.exe"));
            string localSdk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk", "platform-tools", "adb.exe");
            candidates.Add(localSdk);
            foreach (string candidate in candidates)
            {
                if (!String.IsNullOrWhiteSpace(candidate) && File.Exists(candidate)) return candidate;
            }
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string folder in path.Split(Path.PathSeparator))
            {
                try
                {
                    string candidate = Path.Combine(folder.Trim(), "adb.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
            return "";
        }

        private async Task<AdbResult> RunAdbAsync(string arguments)
        {
            string adb = FindAdb();
            if (String.IsNullOrWhiteSpace(adb))
            {
                return new AdbResult { Started = false, ExitCode = -1, Error = "找不到 adb.exe" };
            }
            return await Task.Run(delegate
            {
                AdbResult result = new AdbResult();
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = adb,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };
                    using (Process process = Process.Start(psi))
                    {
                        result.Started = true;
                        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                        Task<string> errorTask = process.StandardError.ReadToEndAsync();
                        process.WaitForExit();
                        result.Output = outputTask.Result;
                        result.Error = errorTask.Result;
                        result.ExitCode = process.ExitCode;
                    }
                }
                catch (Exception ex)
                {
                    result.Started = false;
                    result.ExitCode = -1;
                    result.Error = ex.Message;
                }
                return result;
            });
        }

        private async Task<AdbResult> RunAdbToFileAsync(string arguments, string outputPath)
        {
            string adb = FindAdb();
            if (String.IsNullOrWhiteSpace(adb))
                return new AdbResult { Started = false, ExitCode = -1, Error = "找不到 adb.exe" };
            return await Task.Run(delegate
            {
                AdbResult result = new AdbResult();
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = adb,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardErrorEncoding = Encoding.UTF8
                    };
                    using (Process process = Process.Start(psi))
                    {
                        result.Started = true;
                        Task<string> errorTask = process.StandardError.ReadToEndAsync();
                        using (FileStream file = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            process.StandardOutput.BaseStream.CopyTo(file);
                        process.WaitForExit();
                        result.Error = errorTask.Result;
                        result.ExitCode = process.ExitCode;
                    }
                }
                catch (Exception ex)
                {
                    result.Started = false;
                    result.ExitCode = -1;
                    result.Error = ex.Message;
                }
                return result;
            });
        }

        private async Task CheckConnectionAsync()
        {
            if (busy) return;
            busy = true;
            refreshButton.Enabled = false;
            devices.Clear();
            string adb = FindAdb();
            if (String.IsNullOrWhiteSpace(adb))
            {
                adbStatusLabel.Text = "● 找不到 ADB";
                adbStatusLabel.ForeColor = Red;
                deviceStatusLabel.Text = "尚未安裝或指定 Android Platform Tools";
                deviceDetailLabel.Text = "按右側「選擇 adb.exe」指定檔案";
                Log("找不到 adb.exe，請指定 Android SDK platform-tools 內的 adb.exe。");
                busy = false;
                refreshButton.Enabled = true;
                return;
            }
            settings.AdbPath = adb;
            SaveSettings();
            adbStatusLabel.Text = "● ADB 已就緒";
            adbStatusLabel.ForeColor = Green;
            deviceStatusLabel.Text = "正在檢查手機連線...";
            deviceDetailLabel.Text = adb;
            Log("使用 ADB：" + adb);
            AdbResult result = await RunAdbAsync("devices -l");
            if (!result.Started || result.ExitCode != 0)
            {
                adbStatusLabel.Text = "● ADB 執行失敗";
                adbStatusLabel.ForeColor = Red;
                deviceStatusLabel.Text = "無法啟動 ADB";
                deviceDetailLabel.Text = CleanOutput(result.Error);
                Log("ADB 錯誤：" + CleanOutput(result.Error));
            }
            else
            {
                devices = ParseDevices(result.Output);
                UpdateDeviceCard();
            }
            busy = false;
            refreshButton.Enabled = true;
        }

        private List<DeviceInfo> ParseDevices(string output)
        {
            List<DeviceInfo> found = new List<DeviceInfo>();
            string[] lines = (output ?? "").Replace("\r", "").Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("List of devices")) continue;
                string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                DeviceInfo info = new DeviceInfo { Serial = parts[0], State = parts[1], Model = "", Product = "" };
                for (int i = 2; i < parts.Length; i++)
                {
                    if (parts[i].StartsWith("model:")) info.Model = parts[i].Substring(6).Replace('_', ' ');
                    if (parts[i].StartsWith("product:")) info.Product = parts[i].Substring(8);
                }
                found.Add(info);
            }
            return found;
        }

        private void UpdateDeviceCard()
        {
            List<DeviceInfo> ready = devices.Where(delegate(DeviceInfo d) { return d.State == "device"; }).ToList();
            List<DeviceInfo> unauthorized = devices.Where(delegate(DeviceInfo d) { return d.State == "unauthorized"; }).ToList();
            List<DeviceInfo> offline = devices.Where(delegate(DeviceInfo d) { return d.State == "offline"; }).ToList();
            if (ready.Count > 0)
            {
                DeviceInfo first = ready[0];
                deviceStatusLabel.Text = ready.Count == 1 ? "手機已正確連線" : "已連線 " + ready.Count + " 台手機";
                deviceStatusLabel.ForeColor = Green;
                string name = String.IsNullOrWhiteSpace(first.Model) ? "Android 裝置" : first.Model;
                deviceDetailLabel.Text = name + "　序號：" + first.Serial + (ready.Count > 1 ? "　（安裝時使用第一台）" : "");
                Log("連線成功：" + name + " / " + first.Serial);
            }
            else if (unauthorized.Count > 0)
            {
                deviceStatusLabel.Text = "手機尚未允許 USB 偵錯";
                deviceStatusLabel.ForeColor = Color.FromArgb(255, 190, 75);
                deviceDetailLabel.Text = "請解鎖手機，在 USB 偵錯授權視窗按「允許」，再重新檢查";
                Log("偵測到未授權裝置：" + unauthorized[0].Serial);
            }
            else if (offline.Count > 0)
            {
                deviceStatusLabel.Text = "手機連線離線";
                deviceStatusLabel.ForeColor = Red;
                deviceDetailLabel.Text = "請重新插拔 USB，切換 USB 偵錯後再重新檢查";
                Log("裝置離線：" + offline[0].Serial);
            }
            else
            {
                deviceStatusLabel.Text = "找不到已連線的手機";
                deviceStatusLabel.ForeColor = Red;
                deviceDetailLabel.Text = "確認 USB 線可傳輸資料，且已開啟開發人員選項與 USB 偵錯";
                Log("ADB 正常，但目前沒有偵測到手機。");
            }
        }

        private void BrowseAdb(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "選擇 adb.exe";
                dialog.Filter = "ADB 執行檔 (adb.exe)|adb.exe|執行檔 (*.exe)|*.exe";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    settings.AdbPath = dialog.FileName;
                    SaveSettings();
                    Log("已指定 ADB：" + dialog.FileName);
                    BeginInvoke(new Action(async delegate { await CheckConnectionAsync(); }));
                }
            }
        }

        private static string AppVersionText()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            return version == null ? "" : "v" + version.Major + "." + version.Minor + "." + version.Build;
        }

        private void ShowAbout(object sender, EventArgs e)
        {
            using (Form aboutForm = new Form())
            {
                float scale = Math.Max(1F, currentDpiScale);
                Rectangle workArea = Screen.FromControl(this).WorkingArea;
                int margin = ScaleValue(24, scale);
                int maximumWidth = Math.Max(ScaleValue(480, scale), workArea.Width - margin * 2);
                int maximumHeight = Math.Max(ScaleValue(360, scale), workArea.Height - margin * 2);
                aboutForm.Text = "關於 Android ADB 快速工具";
                aboutForm.StartPosition = FormStartPosition.CenterParent;
                aboutForm.BackColor = Bg;
                aboutForm.ForeColor = TextColor;
                aboutForm.Font = Font;
                aboutForm.AutoScaleMode = AutoScaleMode.None;
                aboutForm.ShowIcon = false;
                aboutForm.MinimizeBox = false;
                aboutForm.Size = new Size(Math.Min(ScaleValue(660, scale), maximumWidth),
                    Math.Min(ScaleValue(500, scale), maximumHeight));
                aboutForm.MinimumSize = new Size(Math.Min(ScaleValue(520, scale), maximumWidth),
                    Math.Min(ScaleValue(440, scale), maximumHeight));
                aboutForm.Padding = ScalePadding(new Padding(28, 24, 28, 20), scale);

                TableLayoutPanel layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Bg,
                    ColumnCount = 1,
                    RowCount = 8,
                    Margin = new Padding(0)
                };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(58, scale)));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(38, scale)));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(62, scale)));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(42, scale)));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(42, scale)));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(50, scale)));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(54, scale)));
                aboutForm.Controls.Add(layout);

                Label title = new Label
                {
                    Text = "Android ADB 快速工具",
                    ForeColor = TextColor,
                    Font = new Font(Font.FontFamily, 19F, FontStyle.Bold),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                Label version = new Label
                {
                    Text = "程式版本：" + AppVersionText(),
                    ForeColor = Muted,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                Label purpose = new Label
                {
                    Text = "透過 ADB 快速管理 Android 裝置、安裝 APK、調整設定與下載手機資料。",
                    ForeColor = TextColor,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                Label author = new Label
                {
                    Text = "作者：廖阿輝",
                    ForeColor = TextColor,
                    Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                LinkLabel email = NewAboutLink("郵件：chehui@gmail.com");
                email.LinkClicked += delegate { OpenExternalLink("mailto:chehui@gmail.com"); };
                LinkLabel website = NewAboutLink("網站：https://ahui3c.com");
                website.LinkClicked += delegate { OpenExternalLink("https://ahui3c.com"); };
                LinkLabel license = NewAboutLink("授權：GNU AGPLv3｜無任何擔保｜檢視授權與原始碼");
                license.LinkClicked += delegate
                {
                    OpenExternalLink("https://github.com/ahui3c/AndroidADBTools/blob/main/LICENSE");
                };
                FlowLayoutPanel actions = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    BackColor = Bg,
                    Padding = ScalePadding(new Padding(0, 8, 0, 0), scale)
                };
                Button close = NewButton("關閉", true, 110);
                close.Size = ScaleSize(new Size(110, 36), scale);
                close.MinimumSize = close.Size;
                close.Click += delegate { aboutForm.Close(); };
                actions.Controls.Add(close);

                layout.Controls.Add(title, 0, 0);
                layout.Controls.Add(version, 0, 1);
                layout.Controls.Add(purpose, 0, 2);
                layout.Controls.Add(author, 0, 3);
                layout.Controls.Add(email, 0, 4);
                layout.Controls.Add(website, 0, 5);
                layout.Controls.Add(license, 0, 6);
                layout.Controls.Add(actions, 0, 7);
                ApplySmoothTextRendering(aboutForm);
                aboutForm.ShowDialog(this);
            }
        }

        private LinkLabel NewAboutLink(string text)
        {
            return new LinkLabel
            {
                Text = text,
                LinkColor = Color.FromArgb(112, 176, 255),
                ActiveLinkColor = Color.FromArgb(160, 205, 255),
                VisitedLinkColor = Color.FromArgb(112, 176, 255),
                BackColor = Bg,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                LinkBehavior = LinkBehavior.HoverUnderline
            };
        }

        private void OpenExternalLink(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "無法開啟連結。\n\n" + ex.Message, "開啟失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowConnectionHelp(object sender, EventArgs e)
        {
            const string usbHelp =
                "USB 連線步驟\r\n\r\n" +
                "1. 手機進入「設定 > 關於手機」，連按 7 次版本號，開啟開發人員模式。\r\n\r\n" +
                "2. 到「設定 > 系統 > 開發人員選項」，開啟「USB 偵錯」。\r\n\r\n" +
                "3. 使用支援資料傳輸的 USB 線連接手機與電腦。\r\n\r\n" +
                "4. 解鎖手機，在「允許 USB 偵錯嗎？」視窗按下允許。建議勾選「一律允許這部電腦」。\r\n\r\n" +
                "5. 回到 Android ADB 快速工具，按下「重新檢查」。\r\n\r\n" +
                "若仍找不到手機：\r\n" +
                "• 確認 USB 模式不是僅充電。\r\n" +
                "• 更換支援資料傳輸的 USB 線或 USB 連接埠。\r\n" +
                "• 在開發人員選項撤銷 USB 偵錯授權，再重新連接。";

            const string wifiHelp =
                "Wi-Fi 無線偵錯（Android 11 以上）\r\n\r\n" +
                "1. 手機與電腦連接到可互通的同一個 Wi-Fi 網路。\r\n\r\n" +
                "2. 在手機的開發人員選項開啟「無線偵錯」。\r\n\r\n" +
                "3. 點選「使用配對碼配對裝置」，記下配對用 IP、Port 與六位數配對碼。\r\n\r\n" +
                "4. 在 adb.exe 所在資料夾開啟命令提示字元，執行：\r\n" +
                "   adb pair 手機IP:配對Port\r\n" +
                "   接著輸入手機顯示的六位數配對碼。\r\n\r\n" +
                "5. 回到無線偵錯主畫面，記下「IP 位址與連接埠」。這個偵錯 Port 通常與配對 Port 不同。\r\n\r\n" +
                "6. 執行：\r\n" +
                "   adb connect 手機IP:偵錯Port\r\n\r\n" +
                "7. 顯示 connected 後，回到本工具按「重新檢查」。\r\n\r\n" +
                "Android 10 以下\r\n" +
                "先用 USB 完成偵錯授權，再執行：\r\n" +
                "   adb tcpip 5555\r\n" +
                "   adb connect 手機IP:5555\r\n" +
                "連線成功後即可拔除 USB。\r\n\r\n" +
                "提醒：重新開啟無線偵錯、切換網路或 IP／Port 改變後，可能需要再次執行 adb connect。";

            using (Form helpForm = new Form())
            {
                float helpScale = Math.Max(1F, currentDpiScale);
                Rectangle workArea = Screen.FromControl(this).WorkingArea;
                int screenMargin = ScaleValue(24, helpScale);
                int maxWidth = Math.Max(ScaleValue(620, helpScale), workArea.Width - screenMargin * 2);
                int maxHeight = Math.Max(ScaleValue(480, helpScale), workArea.Height - screenMargin * 2);
                helpForm.Text = "ADB 連線教學";
                helpForm.StartPosition = FormStartPosition.CenterParent;
                helpForm.BackColor = Bg;
                helpForm.ForeColor = TextColor;
                helpForm.Font = Font;
                helpForm.AutoScaleMode = AutoScaleMode.None;
                helpForm.MinimumSize = new Size(Math.Min(ScaleValue(760, helpScale), maxWidth),
                    Math.Min(ScaleValue(560, helpScale), maxHeight));
                helpForm.Size = new Size(Math.Min(ScaleValue(900, helpScale), maxWidth),
                    Math.Min(ScaleValue(680, helpScale), maxHeight));
                helpForm.ShowIcon = false;

                Panel header = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = ScaleValue(84, helpScale),
                    Padding = ScalePadding(new Padding(22, 16, 22, 8), helpScale),
                    BackColor = Bg
                };
                Label title = new Label
                {
                    Text = "ADB 連線教學",
                    ForeColor = TextColor,
                    Font = new Font(Font.FontFamily, 18F, FontStyle.Bold),
                    Dock = DockStyle.Top,
                    Height = ScaleValue(36, helpScale)
                };
                Label hint = new Label
                {
                    Text = "選擇連線方式；文字可反白並使用 Ctrl+C 複製。",
                    ForeColor = Muted,
                    Dock = DockStyle.Fill
                };
                header.Controls.Add(hint);
                header.Controls.Add(title);
                helpForm.Controls.Add(header);

                ModernTabControl helpTabs = new ModernTabControl
                {
                    Dock = DockStyle.Fill,
                    Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
                    BackColor = Bg,
                    ItemSize = ScaleSize(new Size(280, 46), helpScale)
                };
                helpTabs.TabPages.Add(CreateConnectionHelpPage("USB 連線", Color.FromArgb(53, 120, 219), usbHelp));
                helpTabs.TabPages.Add(CreateConnectionHelpPage("Wi-Fi 無線偵錯", Color.FromArgb(32, 151, 116), wifiHelp));
                helpForm.Controls.Add(helpTabs);
                helpTabs.BringToFront();

                FlowLayoutPanel footer = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = ScaleValue(60, helpScale),
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = ScalePadding(new Padding(10), helpScale),
                    BackColor = Bg
                };
                Button close = NewButton("關閉", true, 110);
                close.Size = ScaleSize(new Size(110, 36), helpScale);
                close.MinimumSize = close.Size;
                close.Margin = ScalePadding(new Padding(5), helpScale);
                close.Click += delegate { helpForm.Close(); };
                footer.Controls.Add(close);
                helpForm.Controls.Add(footer);
                footer.BringToFront();
                ApplySmoothTextRendering(helpForm);
                helpForm.ShowDialog(this);
            }
        }

        private TabPage CreateConnectionHelpPage(string title, Color accent, string helpText)
        {
            float scale = Math.Max(1F, currentDpiScale);
            TabPage page = NewTab(title, accent);
            page.Padding = ScalePadding(new Padding(12), scale);
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = Card, Padding = ScalePadding(new Padding(16), scale) };
            TextBox text = new TextBox
            {
                Text = helpText,
                Multiline = true,
                ReadOnly = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Card2,
                ForeColor = TextColor,
                BorderStyle = BorderStyle.None,
                Font = new Font(Font.FontFamily, 10.5F),
                Padding = ScalePadding(new Padding(10), scale)
            };
            text.HandleCreated += delegate { SetWindowTheme(text.Handle, "DarkMode_Explorer", null); };
            card.Controls.Add(text);
            page.Controls.Add(card);
            return page;
        }

        private void ScanFolderGroups()
        {
            folderGroups.Clear();
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "APKs");
            try
            {
                Directory.CreateDirectory(root);
                foreach (string folder in Directory.GetDirectories(root).OrderBy(delegate(string path) { return path; }, StringComparer.OrdinalIgnoreCase))
                {
                    ApkGroup group = new ApkGroup
                    {
                        Id = "folder:" + folder.ToLowerInvariant(),
                        Name = Path.GetFileName(folder),
                        IsFolderGroup = true,
                        FolderPath = folder
                    };
                    ReloadFolderGroup(group);
                    folderGroups.Add(group);
                }
                Log("已掃描 APKs 資料夾：找到 " + folderGroups.Count + " 個資料夾組合。");
            }
            catch (Exception ex)
            {
                Log("掃描 APKs 資料夾失敗：" + ex.Message);
            }
        }

        private void ReloadFolderGroup(ApkGroup group)
        {
            if (group == null || !group.IsFolderGroup) return;
            group.Apks.Clear();
            try
            {
                if (!Directory.Exists(group.FolderPath)) return;
                IEnumerable<string> files = Directory.GetFiles(group.FolderPath, "*", SearchOption.AllDirectories)
                    .Where(delegate(string path) { return String.Equals(Path.GetExtension(path), ".apk", StringComparison.OrdinalIgnoreCase); })
                    .OrderBy(delegate(string path) { return path; }, StringComparer.OrdinalIgnoreCase);
                foreach (string path in files) group.Apks.Add(new ApkEntry(Path.GetFullPath(path)));
            }
            catch (Exception ex)
            {
                Log("更新資料夾組合「" + group.Name + "」失敗：" + ex.Message);
            }
        }

        private List<ApkGroup> AllGroups()
        {
            List<ApkGroup> groups = new List<ApkGroup>(settings.Groups);
            groups.AddRange(folderGroups);
            if (settings.GroupOrder == null || settings.GroupOrder.Count == 0) return groups;
            Dictionary<string, int> order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < settings.GroupOrder.Count; i++)
                if (!String.IsNullOrWhiteSpace(settings.GroupOrder[i]) && !order.ContainsKey(settings.GroupOrder[i]))
                    order[settings.GroupOrder[i]] = i;
            return groups.OrderBy(delegate(ApkGroup group)
            {
                int index;
                return order.TryGetValue(group.Id, out index) ? index : Int32.MaxValue;
            }).ToList();
        }

        private void RefreshGroups(string selectId)
        {
            groupList.BeginUpdate();
            groupList.Items.Clear();
            int selectIndex = -1;
            List<ApkGroup> allGroups = AllGroups();
            for (int i = 0; i < allGroups.Count; i++)
            {
                groupList.Items.Add(allGroups[i]);
                if (allGroups[i].Id == selectId) selectIndex = i;
            }
            groupList.EndUpdate();
            int widest = groupList.ClientSize.Width;
            using (Graphics graphics = groupList.CreateGraphics())
            {
                foreach (ApkGroup group in allGroups)
                {
                    int width = TextRenderer.MeasureText(graphics, group.Name + "　" + group.Apks.Count + " 個 APK", groupList.Font).Width + 44;
                    if (width > widest) widest = width;
                }
            }
            groupList.HorizontalExtent = widest;
            if (selectIndex >= 0) groupList.SelectedIndex = selectIndex;
            else if (groupList.Items.Count > 0) groupList.SelectedIndex = 0;
            else ShowSelectedGroup();
        }

        private void RefreshGroups()
        {
            RefreshGroups(null);
        }

        private ApkGroup SelectedGroup()
        {
            return groupList == null ? null : groupList.SelectedItem as ApkGroup;
        }

        private void ShowSelectedGroup()
        {
            ApkGroup group = SelectedGroup();
            apkList.Items.Clear();
            if (group == null)
            {
                groupTitle.Text = "請建立或選擇安裝組合";
                groupHint.Text = "建立組合後，可將 APK 直接拖到右側清單";
                UpdateGroupActionButtons();
                return;
            }
            if (group.IsFolderGroup)
            {
                ReloadFolderGroup(group);
                groupList.Invalidate();
            }
            groupTitle.Text = group.Name;
            groupHint.Text = group.IsFolderGroup
                ? group.Apks.Count + " 個 APK　｜　來源：APKs\\" + group.Name + "　｜　點選時自動更新"
                : group.Apks.Count + " 個 APK　｜　可拖放 APK 到右側清單　｜　雙擊左側組合可全部安裝";
            foreach (ApkEntry entry in group.Apks)
            {
                AddApkListItem(apkList, entry.Path, File.Exists(entry.Path) ? "等待安裝" : "檔案不存在");
            }
            UpdateGroupActionButtons();
        }

        private void UpdateGroupActionButtons()
        {
            ApkGroup group = SelectedGroup();
            bool editable = !busy && group != null && !group.IsFolderGroup;
            if (renameGroupButton != null) renameGroupButton.Enabled = editable;
            if (deleteGroupButton != null) deleteGroupButton.Enabled = editable;
            if (addGroupApksButton != null) addGroupApksButton.Enabled = editable;
            if (removeGroupApkButton != null) removeGroupApkButton.Enabled = editable;
            List<ApkGroup> groups = AllGroups();
            int index = group == null ? -1 : groups.FindIndex(delegate(ApkGroup item) { return item.Id == group.Id; });
            if (moveGroupUpButton != null) moveGroupUpButton.Enabled = !busy && index > 0;
            if (moveGroupDownButton != null) moveGroupDownButton.Enabled = !busy && index >= 0 && index < groups.Count - 1;
        }

        private void MoveSelectedGroup(int direction)
        {
            if (busy || direction == 0) return;
            ApkGroup selected = SelectedGroup();
            if (selected == null) return;
            List<ApkGroup> groups = AllGroups();
            int index = groups.FindIndex(delegate(ApkGroup group) { return group.Id == selected.Id; });
            int target = index + direction;
            if (index < 0 || target < 0 || target >= groups.Count) return;
            ApkGroup moved = groups[index];
            groups[index] = groups[target];
            groups[target] = moved;
            settings.GroupOrder = groups.Select(delegate(ApkGroup group) { return group.Id; }).ToList();
            SaveSettings();
            RefreshGroups(selected.Id);
        }

        private void AddGroup(object sender, EventArgs e)
        {
            string name = Prompt("新增安裝組合", "組合名稱", "我的應用程式");
            if (String.IsNullOrWhiteSpace(name)) return;
            ApkGroup group = new ApkGroup { Name = name.Trim() };
            if (settings.GroupOrder == null || settings.GroupOrder.Count == 0)
                settings.GroupOrder = AllGroups().Select(delegate(ApkGroup item) { return item.Id; }).ToList();
            settings.Groups.Add(group);
            settings.GroupOrder.Add(group.Id);
            SaveSettings();
            RefreshGroups(group.Id);
        }

        private void RenameGroup(object sender, EventArgs e)
        {
            ApkGroup group = SelectedGroup();
            if (group == null || group.IsFolderGroup) return;
            string name = Prompt("重新命名", "組合名稱", group.Name);
            if (String.IsNullOrWhiteSpace(name)) return;
            group.Name = name.Trim();
            SaveSettings();
            RefreshGroups(group.Id);
        }

        private void DeleteGroup(object sender, EventArgs e)
        {
            ApkGroup group = SelectedGroup();
            if (group == null || group.IsFolderGroup || busy) return;
            if (MessageBox.Show(this, "確定刪除「" + group.Name + "」？\nAPK 原始檔不會被刪除。", "刪除組合", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            settings.Groups.Remove(group);
            if (settings.GroupOrder != null) settings.GroupOrder.RemoveAll(delegate(string id) { return String.Equals(id, group.Id, StringComparison.OrdinalIgnoreCase); });
            SaveSettings();
            RefreshGroups();
        }

        private void AddApksToGroup(object sender, EventArgs e)
        {
            ApkGroup group = SelectedGroup();
            if (group == null || group.IsFolderGroup)
            {
                MessageBox.Show(this, "請先建立一個安裝組合。", "尚無組合", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string[] paths = ChooseApkFiles();
            if (paths.Length == 0) return;
            AddApksToGroup(group, paths);
        }

        private void GroupApkDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            ApkGroup group = SelectedGroup();
            if (busy || group == null || group.IsFolderGroup || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Any(delegate(string f) { return File.Exists(f) && String.Equals(Path.GetExtension(f), ".apk", StringComparison.OrdinalIgnoreCase); }))
                e.Effect = DragDropEffects.Copy;
        }

        private void GroupApkDragDrop(object sender, DragEventArgs e)
        {
            if (busy || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            ApkGroup group = SelectedGroup();
            if (group == null || group.IsFolderGroup) return;
            string[] files = ((string[])e.Data.GetData(DataFormats.FileDrop))
                .Where(delegate(string f) { return File.Exists(f) && String.Equals(Path.GetExtension(f), ".apk", StringComparison.OrdinalIgnoreCase); })
                .ToArray();
            AddApksToGroup(group, files);
        }

        private void AddApksToGroup(ApkGroup group, IEnumerable<string> paths)
        {
            if (group == null || group.IsFolderGroup) return;
            bool changed = false;
            foreach (string path in paths)
            {
                string fullPath = Path.GetFullPath(path);
                if (group.Apks.Any(delegate(ApkEntry a) { return String.Equals(a.Path, fullPath, StringComparison.OrdinalIgnoreCase); })) continue;
                group.Apks.Add(new ApkEntry(fullPath));
                changed = true;
            }
            if (!changed) return;
            SaveSettings();
            RefreshGroups(group.Id);
        }

        private void RemoveSelectedApks(object sender, EventArgs e)
        {
            ApkGroup group = SelectedGroup();
            if (group == null || group.IsFolderGroup || busy || apkList.SelectedItems.Count == 0) return;
            List<string> paths = new List<string>();
            foreach (ListViewItem item in apkList.SelectedItems) paths.Add((string)item.Tag);
            group.Apks.RemoveAll(delegate(ApkEntry a) { return paths.Contains(a.Path); });
            SaveSettings();
            RefreshGroups(group.Id);
        }

        private string[] ChooseApkFiles()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "選擇 APK";
                dialog.Filter = "Android APK (*.apk)|*.apk";
                dialog.Multiselect = true;
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileNames : new string[0];
            }
        }

        private async void ChooseSingleApks(object sender, EventArgs e)
        {
            if (busy) return;
            await InstallQuickFilesAsync(ChooseApkFiles());
        }

        private void ApkDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            if (!busy && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effect = files.Any(delegate(string f) { return File.Exists(f) && String.Equals(Path.GetExtension(f), ".apk", StringComparison.OrdinalIgnoreCase); }) ? DragDropEffects.Copy : DragDropEffects.None;
            }
        }

        private async void ApkDragDrop(object sender, DragEventArgs e)
        {
            if (busy || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] files = ((string[])e.Data.GetData(DataFormats.FileDrop))
                .Where(delegate(string f) { return File.Exists(f) && String.Equals(Path.GetExtension(f), ".apk", StringComparison.OrdinalIgnoreCase); }).ToArray();
            await InstallQuickFilesAsync(files);
        }

        private async Task InstallQuickFilesAsync(IEnumerable<string> files)
        {
            if (busy) return;
            HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                string full = Path.GetFullPath(file);
                if (File.Exists(full) && String.Equals(Path.GetExtension(full), ".apk", StringComparison.OrdinalIgnoreCase)) unique.Add(full);
            }
            if (unique.Count == 0) return;
            if (!await EnsureReadyDeviceAsync()) return;
            List<Tuple<string, ListViewItem>> jobs = new List<Tuple<string, ListViewItem>>();
            foreach (string path in unique) jobs.Add(Tuple.Create<string, ListViewItem>(path, null));
            quickInstalling = true;
            dropPanel.Invalidate();
            try
            {
                await InstallJobsAsync(jobs, "快速安裝");
            }
            finally
            {
                quickInstalling = false;
                dropPanel.Invalidate();
            }
        }

        private ListViewItem AddApkListItem(ListView list, string path, string status)
        {
            ListViewItem item = new ListViewItem(Path.GetFileName(path));
            item.SubItems.Add(Path.GetDirectoryName(path) ?? "");
            item.SubItems.Add(status);
            item.Tag = path;
            if (status.Contains("成功")) item.ForeColor = Green;
            else if (status.Contains("失敗") || status.Contains("不存在")) item.ForeColor = Red;
            list.Items.Add(item);
            return item;
        }

        private DeviceInfo ReadyDevice()
        {
            return devices.FirstOrDefault(delegate(DeviceInfo d) { return d.State == "device"; });
        }

        private async Task<bool> EnsureReadyDeviceAsync()
        {
            if (ReadyDevice() != null) return true;
            await CheckConnectionAsync();
            if (ReadyDevice() != null) return true;
            MessageBox.Show(this, "目前沒有可安裝的 Android 手機。\n請先完成 USB 偵錯授權並重新檢查。", "手機未連線", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        private async Task ReadBrightnessAsync()
        {
            if (busy || brightnessApplying) return;
            if (!await EnsureReadyDeviceAsync()) return;
            DeviceInfo device = ReadyDevice();
            if (device == null) return;
            busy = true;
            SetInstallButtons(false);
            brightnessStatusLabel.Text = "正在讀取設備亮度...";
            brightnessStatusLabel.ForeColor = Muted;
            string prefix = "-s " + Quote(device.Serial) + " shell ";
            AdbResult brightness = await RunAdbAsync(prefix + "settings get system screen_brightness");
            AdbResult mode = await RunAdbAsync(prefix + "settings get system screen_brightness_mode");
            int value = 0;
            bool ok = AdbCommandSucceeded(brightness) && Int32.TryParse(FirstOutputLine(brightness.Output), out value);
            if (ok)
            {
                value = Math.Max(0, value);
                Tuple<int, bool, string> maximumInfo = await DetectBrightnessMaximumAsync(prefix, value);
                SetBrightnessMaximum(maximumInfo.Item1);
                brightnessAutoMode = FirstOutputLine(mode.Output) == "1";
                SetBrightnessControls(value, false);
                brightnessLastApplied = value;
                brightnessStatusLabel.Text = "目前亮度：" + value + " / " + brightnessDetectedMaximum +
                    "（" + BrightnessPercent(value) + "%）" + (brightnessAutoMode == true ? "；自動亮度已開啟" : "；手動亮度");
                brightnessStatusLabel.ForeColor = Green;
                Log("已讀取設備亮度：" + value + " / " + brightnessDetectedMaximum + "（" + maximumInfo.Item3 + "）");
            }
            else
            {
                brightnessStatusLabel.Text = "無法讀取目前亮度";
                brightnessStatusLabel.ForeColor = Red;
                Log("讀取亮度失敗：" + CleanOutput((brightness.Output ?? "") + " " + (brightness.Error ?? "")));
            }
            busy = false;
            SetInstallButtons(true);
        }

        private async Task<Tuple<int, bool, string>> DetectBrightnessMaximumAsync(string shellPrefix, int currentValue)
        {
            AdbResult overlay = await RunAdbAsync(shellPrefix + "cmd overlay lookup android android:integer/config_screenBrightnessSettingMaximum");
            int maximum = ParseDirectBrightnessMaximum((overlay.Output ?? "") + " " + (overlay.Error ?? ""));
            if (maximum > 0)
                return DetectedMaximumResult(maximum, currentValue, "Android 系統資源");

            AdbResult power = await RunAdbAsync(shellPrefix + "dumpsys power");
            maximum = ParseNamedBrightnessMaximum((power.Output ?? "") + " " + (power.Error ?? ""));
            if (maximum > 0)
                return DetectedMaximumResult(maximum, currentValue, "電源服務");

            AdbResult display = await RunAdbAsync(shellPrefix + "dumpsys display");
            maximum = ParseNamedBrightnessMaximum((display.Output ?? "") + " " + (display.Error ?? ""));
            if (maximum > 0)
                return DetectedMaximumResult(maximum, currentValue, "顯示服務");

            int inferred = Math.Max(255, currentValue);
            return Tuple.Create(inferred, false, "設備未公開最大值，目前暫用 " + inferred + "；可手動修改");
        }

        private static Tuple<int, bool, string> DetectedMaximumResult(int reported, int current, string source)
        {
            int effective = Math.Max(reported, current);
            string description = source + "回報最大值 " + reported;
            if (effective > reported) description += "；目前亮度較高，範圍擴大為 " + effective;
            return Tuple.Create(effective, true, description);
        }

        private static int ParseDirectBrightnessMaximum(string output)
        {
            string text = (output ?? "").Trim();
            Match hex = Regex.Match(text, @"(?:^|\s)0x([0-9a-f]+)(?:\s|$)", RegexOptions.IgnoreCase);
            if (hex.Success)
            {
                try { return Convert.ToInt32(hex.Groups[1].Value, 16); }
                catch { }
            }
            string[] lines = text.Replace("\r", "").Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                int value;
                string line = lines[i].Trim();
                if (Int32.TryParse(line, out value) && value > 0) return value;
                Match trailing = Regex.Match(line, @"(?:=|->|:)\s*(\d+)\s*$");
                if (trailing.Success && Int32.TryParse(trailing.Groups[1].Value, out value) && value > 0) return value;
            }
            return 0;
        }

        private static int ParseNamedBrightnessMaximum(string output)
        {
            string pattern = @"(?:config_screenBrightnessSettingMaximum|mScreenBrightnessSettingMaximum|screenBrightnessSettingMaximum)\s*[=:]\s*(\d+)";
            Match match = Regex.Match(output ?? "", pattern, RegexOptions.IgnoreCase);
            int value;
            return match.Success && Int32.TryParse(match.Groups[1].Value, out value) && value > 0 ? value : 0;
        }

        private int BrightnessPercent(int value)
        {
            if (brightnessDetectedMaximum <= 0) return 0;
            return (int)Math.Round(value * 100.0 / brightnessDetectedMaximum);
        }

        private async Task ApplyBrightnessAsync()
        {
            if (brightnessApplying)
            {
                brightnessUpdateTimer.Stop();
                brightnessUpdateTimer.Start();
                return;
            }
            if (busy)
            {
                brightnessStatusLabel.Text = "等待其他操作完成...";
                brightnessUpdateTimer.Stop();
                brightnessUpdateTimer.Start();
                return;
            }
            brightnessApplying = true;
            if (!await EnsureReadyDeviceAsync())
            {
                brightnessApplying = false;
                return;
            }
            DeviceInfo device = ReadyDevice();
            if (device == null)
            {
                brightnessApplying = false;
                return;
            }
            int value = brightnessPendingValue;
            busy = true;
            SetInstallButtons(false);
            brightnessStatusLabel.Text = "正在套用亮度 " + value + "...";
            brightnessStatusLabel.ForeColor = Color.FromArgb(255, 190, 75);
            string prefix = "-s " + Quote(device.Serial) + " shell ";
            bool ok = true;
            if (brightnessDisableAutoCheck.Checked && brightnessAutoMode != false)
            {
                AdbResult modeResult = await RunAdbAsync(prefix + "settings put system screen_brightness_mode 0");
                ok = AdbCommandSucceeded(modeResult);
                if (ok) brightnessAutoMode = false;
                else Log("關閉自動亮度失敗：" + CleanOutput((modeResult.Output ?? "") + " " + (modeResult.Error ?? "")));
            }
            AdbResult brightnessResult = await RunAdbAsync(prefix + "settings put system screen_brightness " + value);
            ok = ok && AdbCommandSucceeded(brightnessResult);
            if (ok)
            {
                brightnessLastApplied = value;
                brightnessStatusLabel.Text = "已套用亮度 " + value + " / " + brightnessDetectedMaximum +
                    "（" + BrightnessPercent(value) + "%）" + (brightnessDisableAutoCheck.Checked ? "；手動亮度" : "");
                brightnessStatusLabel.ForeColor = Green;
                Log("設備亮度已調整為 " + value + "。");
            }
            else
            {
                brightnessStatusLabel.Text = "亮度套用失敗，請查看執行紀錄";
                brightnessStatusLabel.ForeColor = Red;
                Log("調整亮度失敗：" + CleanOutput((brightnessResult.Output ?? "") + " " + (brightnessResult.Error ?? "")));
            }
            busy = false;
            brightnessApplying = false;
            SetInstallButtons(true);
            if (brightnessPendingValue != value)
            {
                brightnessUpdateTimer.Stop();
                brightnessUpdateTimer.Start();
            }
        }

        private async Task ReadQuickSettingsAsync()
        {
            if (busy) return;
            if (!await EnsureReadyDeviceAsync()) return;
            DeviceInfo device = ReadyDevice();
            if (device == null) return;
            busy = true;
            SetInstallButtons(false);
            quickSettingsStateLabel.Text = "正在讀取手機設定...";
            quickSettingsStateLabel.ForeColor = Muted;
            string prefix = "-s " + Quote(device.Serial) + " shell ";
            AdbResult brightness = await RunAdbAsync(prefix + "settings get system screen_brightness_mode");
            AdbResult timeout = await RunAdbAsync(prefix + "settings get system screen_off_timeout");
            AdbResult stayOn = await RunAdbAsync(prefix + "settings get global stay_on_while_plugged_in");
            bool ok = AdbCommandSucceeded(brightness) && AdbCommandSucceeded(timeout) && AdbCommandSucceeded(stayOn);
            if (ok)
            {
                string brightnessValue = FirstOutputLine(brightness.Output);
                string timeoutValue = FirstOutputLine(timeout.Output);
                string stayOnValue = FirstOutputLine(stayOn.Output);
                long timeoutMilliseconds;
                long.TryParse(timeoutValue, out timeoutMilliseconds);
                int stayOnMask;
                int.TryParse(stayOnValue, out stayOnMask);
                loadingQuickSettings = true;
                autoBrightnessCheck.Checked = brightnessValue == "1";
                timeoutTenMinutesCheck.Checked = timeoutMilliseconds == 600000;
                timeoutNeverCheck.Checked = timeoutMilliseconds >= 2147483000;
                stayOnWhileChargingCheck.Checked = stayOnMask != 0;
                if (timeoutNeverCheck.Checked) timeoutTenMinutesCheck.Checked = false;
                loadingQuickSettings = false;
                string timeoutDescription = timeoutMilliseconds == 600000
                    ? "10 分鐘"
                    : (timeoutMilliseconds >= 2147483000 ? "最長逾時" : FormatTimeout(timeoutMilliseconds));
                quickSettingsStateLabel.Text = "目前：自動亮度" + (autoBrightnessCheck.Checked ? "開啟" : "關閉") +
                    "；螢幕逾時 " + timeoutDescription + "；充電保持亮屏" + (stayOnWhileChargingCheck.Checked ? "開啟" : "關閉");
                quickSettingsStateLabel.ForeColor = Green;
                Log("已讀取快速功能設定：" + quickSettingsStateLabel.Text.Replace("目前：", ""));
            }
            else
            {
                quickSettingsStateLabel.Text = "讀取失敗，手機可能限制 ADB 修改系統設定";
                quickSettingsStateLabel.ForeColor = Red;
                Log("讀取手機設定失敗：" + CleanOutput((brightness.Error ?? "") + " " + (timeout.Error ?? "")));
            }
            busy = false;
            SetInstallButtons(true);
        }

        private async Task SetMediaVolumeExtremeAsync(bool maximum)
        {
            if (busy) return;
            if (!await EnsureReadyDeviceAsync()) return;
            DeviceInfo device = ReadyDevice();
            if (device == null) return;
            busy = true;
            SetInstallButtons(false);
            Log("正在將手機媒體音量調到" + (maximum ? "最高" : "最低") + "。");
            AdbResult result = await RunAdbAsync("-s " + Quote(device.Serial) + " shell cmd media_session volume --stream 3 --set " + (maximum ? "1000" : "0"));
            string combined = ((result.Output ?? "") + " " + (result.Error ?? "")).Trim();
            bool ok = AdbCommandSucceeded(result) && combined.IndexOf("Error", StringComparison.OrdinalIgnoreCase) < 0 &&
                combined.IndexOf("Unknown", StringComparison.OrdinalIgnoreCase) < 0;
            if (!ok)
            {
                string keyCode = maximum ? "24" : "25";
                string remoteCommand = String.Join("; ", Enumerable.Repeat("input keyevent " + keyCode, 40).ToArray());
                result = await RunAdbAsync("-s " + Quote(device.Serial) + " shell " + Quote(remoteCommand));
                ok = AdbCommandSucceeded(result);
            }
            busy = false;
            SetInstallButtons(true);
            if (ok)
            {
                Log("媒體音量已調到" + (maximum ? "最高" : "最低") + "。");
                MessageBox.Show(this, "媒體音量已調到" + (maximum ? "最高。" : "最低。"), "音量調整", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                string detail = CleanOutput((result.Output ?? "") + " " + (result.Error ?? ""));
                Log("媒體音量調整失敗：" + detail);
                MessageBox.Show(this, "無法調整手機媒體音量。\n\n" + detail, "音量調整失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task OpenUrlOnDeviceAsync()
        {
            if (busy) return;
            string value = (urlTextBox.Text ?? "").Trim();
            if (value.Length == 0) return;
            if (value.IndexOf("://", StringComparison.Ordinal) < 0) value = "https://" + value;
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) ||
                !(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                MessageBox.Show(this, "請輸入有效的 HTTP 或 HTTPS 網址。", "網址格式錯誤", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!await EnsureReadyDeviceAsync()) return;
            DeviceInfo device = ReadyDevice();
            if (device == null) return;
            busy = true;
            SetInstallButtons(false);
            urlTextBox.Text = uri.AbsoluteUri;
            Log("在手機開啟網址：" + uri.AbsoluteUri);
            AdbResult result = await RunAdbAsync("-s " + Quote(device.Serial) + " shell am start -a android.intent.action.VIEW -d " + Quote(uri.AbsoluteUri));
            string combined = ((result.Output ?? "") + " " + (result.Error ?? "")).Trim();
            bool ok = AdbCommandSucceeded(result) && combined.IndexOf("Error:", StringComparison.OrdinalIgnoreCase) < 0;
            busy = false;
            SetInstallButtons(true);
            if (!ok)
            {
                Log("開啟網址失敗：" + CleanOutput(combined));
                MessageBox.Show(this, "手機無法開啟指定網址。\n\n" + CleanOutput(combined), "開啟網址失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task CaptureScreenshotAsync()
        {
            if (busy) return;
            if (!await EnsureReadyDeviceAsync()) return;
            DeviceInfo device = ReadyDevice();
            if (device == null) return;
            string outputPath;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "儲存手機畫面截圖";
                dialog.Filter = "PNG 圖片 (*.png)|*.png";
                dialog.DefaultExt = "png";
                dialog.AddExtension = true;
                dialog.FileName = "Android_Screenshot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
                dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                outputPath = dialog.FileName;
            }
            busy = true;
            SetInstallButtons(false);
            Log("正在擷取手機畫面：" + outputPath);
            AdbResult result = await RunAdbToFileAsync("-s " + Quote(device.Serial) + " exec-out screencap -p", outputPath);
            bool ok = AdbCommandSucceeded(result) && IsPngFile(outputPath);
            busy = false;
            SetInstallButtons(true);
            if (ok)
            {
                Log("手機截圖已儲存：" + outputPath);
                MessageBox.Show(this, "手機截圖已儲存到：\n\n" + outputPath, "截圖完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
                string detail = CleanOutput((result.Output ?? "") + " " + (result.Error ?? ""));
                Log("手機截圖失敗：" + detail);
                MessageBox.Show(this, "無法擷取手機畫面。\n\n" + detail, "截圖失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BrowseDownloadFolder(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "選擇手機資料壓縮檔的儲存位置";
                dialog.ShowNewFolderButton = true;
                if (!String.IsNullOrWhiteSpace(settings.DownloadFolder) && Directory.Exists(settings.DownloadFolder))
                    dialog.SelectedPath = settings.DownloadFolder;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                settings.DownloadFolder = dialog.SelectedPath;
                downloadFolderTextBox.Text = dialog.SelectedPath;
                SaveSettings();
            }
        }

        private async Task DownloadPhoneDataAsync()
        {
            if (busy) return;
            if (!await EnsureReadyDeviceAsync()) return;
            DeviceInfo device = ReadyDevice();
            if (device == null) return;

            string outputFolder = (downloadFolderTextBox.Text ?? "").Trim();
            if (String.IsNullOrWhiteSpace(outputFolder))
            {
                MessageBox.Show(this, "請先選擇電腦儲存位置。", "尚未選擇位置", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                Directory.CreateDirectory(outputFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "無法建立儲存資料夾。\n\n" + ex.Message, "儲存位置錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            settings.DownloadFolder = outputFolder;
            settings.SkipLargeDownloadFiles = skipLargeDownloadCheck.Checked;
            settings.MaxDownloadFileSizeGb = maxDownloadSizeNumber.Value;
            SaveSettings();

            busy = true;
            SetInstallButtons(false);
            SetDownloadBusy(true);
            string temporaryFolder = Path.Combine(Path.GetTempPath(), "AndroidADBTools", Guid.NewGuid().ToString("N"));
            string zipPath = "";
            try
            {
                downloadStatusLabel.Text = "正在掃描手機檔案與大小...";
                downloadProgressBar.Style = ProgressBarStyle.Marquee;
                downloadProgressBar.MarqueeAnimationSpeed = 24;
                Log("開始掃描手機的 DCIM、Pictures 與 Picture 資料夾。");

                List<RemoteFileInfo> remoteFiles = await ReadPhoneMediaFilesAsync(device);
                if (remoteFiles.Count == 0)
                {
                    downloadStatusLabel.Text = "找不到可下載的相片或截圖檔案";
                    MessageBox.Show(this, "手機的 DCIM、Pictures 與 Picture 資料夾內沒有找到可讀取的檔案。", "沒有檔案", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                long maximumBytes = (long)(maxDownloadSizeNumber.Value * 1024M * 1024M * 1024M);
                List<RemoteFileInfo> selectedFiles = remoteFiles.Where(delegate(RemoteFileInfo file)
                {
                    return !skipLargeDownloadCheck.Checked || file.Size <= maximumBytes;
                }).ToList();
                List<RemoteFileInfo> skippedFiles = remoteFiles.Except(selectedFiles).ToList();
                long selectedBytes = selectedFiles.Sum(delegate(RemoteFileInfo file) { return file.Size; });
                long skippedBytes = skippedFiles.Sum(delegate(RemoteFileInfo file) { return file.Size; });
                Log("掃描完成：共 " + remoteFiles.Count + " 個檔案，準備下載 " + selectedFiles.Count + " 個（" + FormatBytes(selectedBytes) + "）。");
                if (skippedFiles.Count > 0)
                    Log("依大小限制略過 " + skippedFiles.Count + " 個檔案（" + FormatBytes(skippedBytes) + "）。");

                if (selectedFiles.Count == 0)
                {
                    downloadStatusLabel.Text = "所有檔案都超過設定上限";
                    MessageBox.Show(this,
                        "找到 " + remoteFiles.Count + " 個檔案，但全部超過 " + maxDownloadSizeNumber.Value.ToString("0.0") + " GB 上限，因此沒有進行傳輸。",
                        "全部略過", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Directory.CreateDirectory(temporaryFolder);
                downloadProgressBar.Style = ProgressBarStyle.Continuous;
                downloadProgressBar.MarqueeAnimationSpeed = 0;
                downloadProgressBar.Minimum = 0;
                downloadProgressBar.Maximum = selectedFiles.Count;
                downloadProgressBar.Value = 0;
                int downloaded = 0;
                int failed = 0;

                for (int i = 0; i < selectedFiles.Count; i++)
                {
                    RemoteFileInfo file = selectedFiles[i];
                    try
                    {
                        string relativePath = MakeSafeMediaRelativePath(file.Path);
                        string localPath = Path.Combine(temporaryFolder, relativePath);
                        string localFolder = Path.GetDirectoryName(localPath);
                        if (!String.IsNullOrWhiteSpace(localFolder)) Directory.CreateDirectory(localFolder);
                        downloadStatusLabel.Text = "下載中 " + (i + 1) + " / " + selectedFiles.Count + "：" + Path.GetFileName(relativePath);
                        Log("下載手機檔案：" + file.Path + "（" + FormatBytes(file.Size) + "）");
                        AdbResult pull = await RunAdbAsync("-s " + Quote(device.Serial) + " pull " + Quote(file.Path) + " " + Quote(localPath));
                        if (pull.Started && pull.ExitCode == 0 && File.Exists(localPath)) downloaded++;
                        else
                        {
                            failed++;
                            Log("下載失敗：" + file.Path + " / " + CleanOutput((pull.Error ?? "") + " " + (pull.Output ?? "")));
                        }
                    }
                    catch (Exception fileError)
                    {
                        failed++;
                        Log("下載失敗：" + file.Path + " / " + fileError.Message);
                    }
                    downloadProgressBar.Value = Math.Min(downloadProgressBar.Maximum, i + 1);
                }

                if (downloaded == 0)
                {
                    downloadStatusLabel.Text = "檔案下載失敗";
                    MessageBox.Show(this, "所有檔案都下載失敗，未建立壓縮檔。請查看執行紀錄。", "下載失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string deviceName = await ReadDeviceDisplayNameAsync(device);
                string zipName = SanitizeWindowsName(deviceName) + "_" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip";
                zipPath = GetUniqueFilePath(Path.Combine(outputFolder, zipName));
                downloadStatusLabel.Text = "正在建立壓縮檔...";
                downloadProgressBar.Style = ProgressBarStyle.Marquee;
                downloadProgressBar.MarqueeAnimationSpeed = 24;
                await Task.Run(delegate
                {
                    ZipFile.CreateFromDirectory(temporaryFolder, zipPath, CompressionLevel.Optimal, false);
                });

                downloadProgressBar.Style = ProgressBarStyle.Continuous;
                downloadProgressBar.MarqueeAnimationSpeed = 0;
                downloadProgressBar.Maximum = 100;
                downloadProgressBar.Value = 100;
                downloadStatusLabel.Text = "完成：" + Path.GetFileName(zipPath);
                Log("手機資料下載完成：" + zipPath);
                MessageBox.Show(this,
                    "手機資料已下載並打包完成。\n\n下載成功：" + downloaded + " 個\n依大小略過：" + skippedFiles.Count + " 個" +
                    (failed > 0 ? "\n下載失敗：" + failed + " 個" : "") + "\n\n" + zipPath,
                    "資料下載完成", MessageBoxButtons.OK, failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                downloadStatusLabel.Text = "下載或打包失敗";
                Log("手機資料下載失敗：" + ex.Message);
                if (!String.IsNullOrWhiteSpace(zipPath))
                {
                    try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                }
                MessageBox.Show(this, "下載或建立壓縮檔時發生錯誤。\n\n" + ex.Message, "資料下載失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                try { if (Directory.Exists(temporaryFolder)) Directory.Delete(temporaryFolder, true); } catch { }
                busy = false;
                SetInstallButtons(true);
                SetDownloadBusy(false);
                if (downloadProgressBar.Style == ProgressBarStyle.Marquee)
                {
                    downloadProgressBar.Style = ProgressBarStyle.Continuous;
                    downloadProgressBar.MarqueeAnimationSpeed = 0;
                }
            }
        }

        private async Task<List<RemoteFileInfo>> ReadPhoneMediaFilesAsync(DeviceInfo device)
        {
            string token = Guid.NewGuid().ToString("N");
            string remoteManifest = "/data/local/tmp/android_adb_tools_media_" + token + ".txt";
            string localManifest = Path.Combine(Path.GetTempPath(), "android_adb_tools_media_" + token + ".txt");
            List<RemoteFileInfo> parsedFiles = null;
            Exception failure = null;
            try
            {
                string command = ": > " + remoteManifest + "; " +
                    "for d in /sdcard/DCIM /sdcard/Pictures /sdcard/Picture; do " +
                    "if [ -d \"$d\" ]; then find \"$d\" -type f -exec stat -c \"%s|%n\" {} \\; >> " + remoteManifest + "; fi; done; echo READY";
                AdbResult scan = await RunAdbWithConnectionRetryAsync("-s " + Quote(device.Serial) + " shell " + Quote(command));
                string scanText = ((scan.Output ?? "") + " " + (scan.Error ?? "")).Trim();
                if (!scan.Started || scan.ExitCode != 0 || scanText.IndexOf("READY", StringComparison.OrdinalIgnoreCase) < 0)
                    throw new InvalidOperationException("無法在手機建立檔案清單：" + CleanOutput(scanText));

                AdbResult pull = await RunAdbWithConnectionRetryAsync("-s " + Quote(device.Serial) + " pull " + Quote(remoteManifest) + " " + Quote(localManifest));
                if (!pull.Started || pull.ExitCode != 0 || !File.Exists(localManifest))
                    throw new InvalidOperationException("無法取得手機檔案清單：" + CleanOutput((pull.Error ?? "") + " " + (pull.Output ?? "")));

                string output = File.ReadAllText(localManifest, Encoding.UTF8);
                Dictionary<string, RemoteFileInfo> files = new Dictionary<string, RemoteFileInfo>(StringComparer.Ordinal);
                foreach (string rawLine in output.Replace("\r", "").Split('\n'))
                {
                    string line = rawLine.Trim();
                    int separator = line.IndexOf('|');
                    if (separator <= 0 || separator >= line.Length - 1) continue;
                    long size;
                    if (!Int64.TryParse(line.Substring(0, separator), out size)) continue;
                    string path = line.Substring(separator + 1).Trim();
                    if (!path.StartsWith("/sdcard/", StringComparison.Ordinal) &&
                        !path.StartsWith("/storage/emulated/0/", StringComparison.Ordinal)) continue;
                    if (!files.ContainsKey(path)) files[path] = new RemoteFileInfo { Path = path, Size = Math.Max(0, size) };
                }
                parsedFiles = files.Values.OrderBy(delegate(RemoteFileInfo file) { return file.Path; }, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                try { if (File.Exists(localManifest)) File.Delete(localManifest); } catch { }
            }
            try { await RunAdbAsync("-s " + Quote(device.Serial) + " shell rm -f " + Quote(remoteManifest)); } catch { }
            if (failure != null) throw failure;
            return parsedFiles ?? new List<RemoteFileInfo>();
        }

        private async Task<AdbResult> RunAdbWithConnectionRetryAsync(string arguments)
        {
            AdbResult last = null;
            bool connectionWasReset = false;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                last = await RunAdbAsync(arguments);
                string detail = ((last.Output ?? "") + " " + (last.Error ?? "")).Trim();
                if (last.Started && last.ExitCode == 0) return last;
                if (detail.IndexOf("protocol fault", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    detail.IndexOf("connection reset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    detail.IndexOf("device offline", StringComparison.OrdinalIgnoreCase) >= 0)
                    connectionWasReset = true;
                if (!connectionWasReset || attempt == 3) break;
                Log("ADB 連線在掃描時中斷，正在自動重試（" + attempt + "/3）...");
                await Task.Delay(1000 * attempt);
                await RunAdbAsync("start-server");
            }
            return last ?? new AdbResult { Started = false, ExitCode = -1, Error = "ADB 未執行" };
        }

        private async Task<string> ReadDeviceDisplayNameAsync(DeviceInfo device)
        {
            AdbResult result = await RunAdbAsync("-s " + Quote(device.Serial) + " shell getprop ro.product.model");
            string name = FirstOutputLine(result.Output);
            if (String.IsNullOrWhiteSpace(name)) name = device.Model;
            if (String.IsNullOrWhiteSpace(name)) name = device.Serial;
            return String.IsNullOrWhiteSpace(name) ? "Android手機" : name;
        }

        private void SetDownloadBusy(bool downloading)
        {
            if (startDownloadButton != null) startDownloadButton.Enabled = !downloading;
            if (browseDownloadFolderButton != null) browseDownloadFolderButton.Enabled = !downloading;
            if (skipLargeDownloadCheck != null) skipLargeDownloadCheck.Enabled = !downloading;
            if (maxDownloadSizeNumber != null) maxDownloadSizeNumber.Enabled = !downloading && skipLargeDownloadCheck.Checked;
        }

        private static string MakeSafeMediaRelativePath(string remotePath)
        {
            string normalized = (remotePath ?? "").Replace('\\', '/');
            if (normalized.StartsWith("/storage/emulated/0/", StringComparison.Ordinal))
                normalized = "/sdcard/" + normalized.Substring("/storage/emulated/0/".Length);
            if (normalized.StartsWith("/sdcard/", StringComparison.Ordinal)) normalized = normalized.Substring(8);
            string[] parts = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> safeParts = new List<string>();
            foreach (string part in parts)
            {
                if (part == "." || part == "..") continue;
                safeParts.Add(SanitizeWindowsName(part));
            }
            if (safeParts.Count == 0) safeParts.Add("未命名檔案");
            return Path.Combine(safeParts.ToArray());
        }

        private static string SanitizeWindowsName(string value)
        {
            string text = String.IsNullOrWhiteSpace(value) ? "未命名" : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(text.Length);
            foreach (char character in text)
                builder.Append(character < 32 || invalid.Contains(character) ? '_' : character);
            string result = builder.ToString().Trim().TrimEnd('.', ' ');
            if (String.IsNullOrWhiteSpace(result)) result = "未命名";
            string baseName = Path.GetFileNameWithoutExtension(result).ToUpperInvariant();
            string[] reserved = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            if (reserved.Contains(baseName)) result = "_" + result;
            if (result.Length > 120)
            {
                string extension = Path.GetExtension(result);
                int keep = Math.Max(1, 120 - extension.Length);
                result = result.Substring(0, keep).TrimEnd('.', ' ') + extension;
            }
            return result;
        }

        private static string GetUniqueFilePath(string path)
        {
            if (!File.Exists(path)) return path;
            string folder = Path.GetDirectoryName(path) ?? "";
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            for (int i = 2; i < 10000; i++)
            {
                string candidate = Path.Combine(folder, name + "_" + i + extension);
                if (!File.Exists(candidate)) return candidate;
            }
            return Path.Combine(folder, name + "_" + Guid.NewGuid().ToString("N") + extension);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L) return (bytes / (1024D * 1024D * 1024D)).ToString("0.00") + " GB";
            if (bytes >= 1024L * 1024L) return (bytes / (1024D * 1024D)).ToString("0.0") + " MB";
            if (bytes >= 1024L) return (bytes / 1024D).ToString("0.0") + " KB";
            return bytes + " B";
        }

        private static bool IsPngFile(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length < 8) return false;
            byte[] signature = new byte[8];
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                if (stream.Read(signature, 0, signature.Length) != signature.Length) return false;
            byte[] expected = { 137, 80, 78, 71, 13, 10, 26, 10 };
            return signature.SequenceEqual(expected);
        }

        private async Task ApplyQuickSettingsAsync()
        {
            if (busy) return;
            if (!await EnsureReadyDeviceAsync()) return;
            DeviceInfo device = ReadyDevice();
            if (device == null) return;
            busy = true;
            SetInstallButtons(false);
            quickSettingsStateLabel.Text = "正在套用設定...";
            quickSettingsStateLabel.ForeColor = Color.FromArgb(255, 190, 75);
            string prefix = "-s " + Quote(device.Serial) + " shell ";
            List<Tuple<string, AdbResult>> results = new List<Tuple<string, AdbResult>>();
            string brightnessName = "自動亮度：" + (autoBrightnessCheck.Checked ? "開啟" : "關閉");
            results.Add(Tuple.Create(brightnessName,
                await ApplyAndVerifySettingAsync(prefix,
                    "settings put system screen_brightness_mode " + (autoBrightnessCheck.Checked ? "1" : "0"),
                    "settings get system screen_brightness_mode",
                    delegate(string value) { return value == (autoBrightnessCheck.Checked ? "1" : "0"); },
                    autoBrightnessCheck.Checked ? "1（開啟）" : "0（關閉）")));
            if (timeoutTenMinutesCheck.Checked)
            {
                results.Add(Tuple.Create("螢幕關閉時間：10 分鐘",
                    await ApplyAndVerifySettingAsync(prefix,
                        "settings put system screen_off_timeout 600000",
                        "settings get system screen_off_timeout",
                        delegate(string value) { return value == "600000"; }, "600000")));
            }
            else if (timeoutNeverCheck.Checked)
            {
                results.Add(Tuple.Create("螢幕關閉時間：不自動關閉",
                    await ApplyAndVerifySettingAsync(prefix,
                        "settings put system screen_off_timeout 2147483647",
                        "settings get system screen_off_timeout",
                        delegate(string value) { long timeout; return Int64.TryParse(value, out timeout) && timeout >= 2147483000; }, "2147483647")));
            }
            bool keepAwakeWhileCharging = stayOnWhileChargingCheck.Checked;
            results.Add(Tuple.Create("充電時保持螢幕不關閉：" + (keepAwakeWhileCharging ? "開啟" : "關閉"),
                await ApplyAndVerifySettingAsync(prefix,
                    "svc power stayon " + (keepAwakeWhileCharging ? "true" : "false"),
                    "settings get global stay_on_while_plugged_in",
                    delegate(string value)
                    {
                        int mask;
                        return Int32.TryParse(value, out mask) && (keepAwakeWhileCharging ? mask != 0 : mask == 0);
                    }, keepAwakeWhileCharging ? "非 0（開啟）" : "0（關閉）")));

            List<string> reportLines = new List<string>();
            List<string> failedNames = new List<string>();
            int successCount = 0;
            foreach (Tuple<string, AdbResult> item in results)
            {
                string name = item.Item1;
                AdbResult result = item.Item2;
                if (AdbCommandSucceeded(result))
                {
                    successCount++;
                    reportLines.Add("[成功] " + name);
                    Log("快速設定成功：" + name);
                }
                else
                {
                    failedNames.Add(name);
                    string detail = QuickSettingErrorDetail(result);
                    reportLines.Add("[失敗] " + name + "\n       " + detail);
                    Log("快速設定失敗：" + name + " / " + detail);
                }
            }

            int failedCount = results.Count - successCount;
            busy = false;
            SetInstallButtons(true);
            if (failedCount == 0)
            {
                await ReadQuickSettingsAsync();
                MessageBox.Show(this, "所有設定都已成功套用。\n\n" + String.Join("\n", reportLines.ToArray()),
                    "快速功能設定", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                quickSettingsStateLabel.Text = successCount > 0
                    ? "部分套用成功；失敗：" + String.Join("、", failedNames.ToArray())
                    : "套用失敗：" + String.Join("、", failedNames.ToArray());
                quickSettingsStateLabel.ForeColor = Red;
                Log("快速設定完成：成功 " + successCount + " 項，失敗 " + failedCount + " 項。其他設定已繼續執行。" );
                MessageBox.Show(this,
                    "各項設定已分別執行；失敗項目不會阻止其他設定。\n\n" + String.Join("\n", reportLines.ToArray()),
                    successCount > 0 ? "部分設定套用失敗" : "設定套用失敗",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string QuickSettingErrorDetail(AdbResult result)
        {
            if (result == null) return "ADB 沒有回傳結果";
            string detail = CleanOutput((result.Output ?? "") + " " + (result.Error ?? ""));
            if (String.IsNullOrWhiteSpace(detail))
            {
                if (!result.Started) detail = "ADB 無法啟動";
                else detail = "ADB 結束代碼 " + result.ExitCode;
            }
            return detail.Length > 180 ? detail.Substring(0, 180) + "..." : detail;
        }

        private async Task<AdbResult> ApplyAndVerifySettingAsync(string prefix, string applyCommand,
            string readCommand, Func<string, bool> valueMatches, string expectedValue)
        {
            AdbResult applied = await RunAdbAsync(prefix + applyCommand);
            if (!AdbCommandSucceeded(applied)) return applied;
            AdbResult read = await RunAdbAsync(prefix + readCommand);
            if (!AdbCommandSucceeded(read)) return read;
            string actualValue = FirstOutputLine(read.Output);
            if (!valueMatches(actualValue))
            {
                return new AdbResult
                {
                    Started = true,
                    ExitCode = -2,
                    Output = read.Output,
                    Error = "寫入後讀回值為「" + (String.IsNullOrWhiteSpace(actualValue) ? "空白" : actualValue) + "」，預期「" + expectedValue + "」"
                };
            }
            return applied;
        }

        private static bool AdbCommandSucceeded(AdbResult result)
        {
            if (result == null || !result.Started || result.ExitCode != 0) return false;
            string combined = (result.Output ?? "") + " " + (result.Error ?? "");
            return combined.IndexOf("Security exception", StringComparison.OrdinalIgnoreCase) < 0 &&
                combined.IndexOf("Permission denial", StringComparison.OrdinalIgnoreCase) < 0 &&
                combined.IndexOf("not found", StringComparison.OrdinalIgnoreCase) < 0 &&
                combined.IndexOf("Error:", StringComparison.OrdinalIgnoreCase) < 0 &&
                combined.IndexOf("Failed", StringComparison.OrdinalIgnoreCase) < 0 &&
                combined.IndexOf("Failure", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static string FirstOutputLine(string output)
        {
            string[] lines = (output ?? "").Replace("\r", "").Split('\n');
            foreach (string line in lines)
            {
                if (!String.IsNullOrWhiteSpace(line)) return line.Trim();
            }
            return "";
        }

        private static string FormatTimeout(long milliseconds)
        {
            if (milliseconds <= 0) return "未知";
            if (milliseconds >= 60000 && milliseconds % 60000 == 0) return (milliseconds / 60000) + " 分鐘";
            return Math.Max(1, milliseconds / 1000) + " 秒";
        }

        private async Task InstallSelectedGroupAsync()
        {
            if (busy) return;
            ApkGroup group = SelectedGroup();
            if (group == null || group.Apks.Count == 0)
            {
                MessageBox.Show(this, "這個組合還沒有 APK。", "沒有 APK", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!await EnsureReadyDeviceAsync()) return;
            ShowSelectedGroup();
            List<Tuple<string, ListViewItem>> jobs = new List<Tuple<string, ListViewItem>>();
            for (int i = 0; i < group.Apks.Count; i++) jobs.Add(Tuple.Create(group.Apks[i].Path, apkList.Items[i]));
            await InstallJobsAsync(jobs, "組合「" + group.Name + "」");
        }

        private async Task InstallJobsAsync(List<Tuple<string, ListViewItem>> jobs, string title)
        {
            DeviceInfo device = ReadyDevice();
            if (device == null) return;
            busy = true;
            SetInstallButtons(false);
            int success = 0;
            int failed = 0;
            Log("開始安裝 " + title + "，共 " + jobs.Count + " 個 APK。");
            foreach (Tuple<string, ListViewItem> job in jobs)
            {
                string path = job.Item1;
                ListViewItem item = job.Item2;
                if (!File.Exists(path))
                {
                    SetItemStatus(item, "失敗：檔案不存在", Red);
                    failed++;
                    Log("略過不存在的檔案：" + path);
                    continue;
                }
                SetItemStatus(item, "安裝中...", Color.FromArgb(255, 190, 75));
                Log("安裝：" + Path.GetFileName(path));
                string flags = "-r" + (settings.AllowDowngrade ? " -d" : "");
                string args = "-s " + Quote(device.Serial) + " install " + flags + " " + Quote(path);
                AdbResult result = await RunAdbAsync(args);
                string combined = ((result.Output ?? "") + " " + (result.Error ?? "")).Trim();
                bool ok = result.Started && result.ExitCode == 0 && combined.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0;
                if (ok)
                {
                    SetItemStatus(item, "成功（ADB 已確認）", Green);
                    success++;
                    Log("安裝成功：" + Path.GetFileName(path));
                }
                else
                {
                    string reason = FriendlyInstallError(combined);
                    SetItemStatus(item, "失敗：" + reason, Red);
                    failed++;
                    Log("安裝失敗：" + Path.GetFileName(path) + " / " + CleanOutput(combined));
                }
            }
            busy = false;
            SetInstallButtons(true);
            Log("安裝完成：成功 " + success + "，失敗 " + failed + "。");
            MessageBox.Show(this, "安裝完成\n\n成功：" + success + "\n失敗：" + failed + (failed > 0 ? "\n\n可到「執行紀錄」查看詳細原因。" : ""), title, MessageBoxButtons.OK, failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void SetInstallButtons(bool enabled)
        {
            installGroupButton.Enabled = enabled;
            refreshButton.Enabled = enabled;
            browseAdbButton.Enabled = enabled;
            if (dropPanel != null)
            {
                dropPanel.Enabled = enabled;
                dropPanel.Cursor = enabled ? Cursors.Hand : Cursors.WaitCursor;
                dropPanel.Invalidate();
            }
            if (applyQuickSettingsButton != null) applyQuickSettingsButton.Enabled = enabled;
            if (readQuickSettingsButton != null) readQuickSettingsButton.Enabled = enabled;
            if (volumeMinimumButton != null) volumeMinimumButton.Enabled = enabled;
            if (volumeMaximumButton != null) volumeMaximumButton.Enabled = enabled;
            if (openUrlButton != null) openUrlButton.Enabled = enabled;
            if (screenshotButton != null) screenshotButton.Enabled = enabled;
            if (startDownloadButton != null) startDownloadButton.Enabled = enabled;
            if (browseDownloadFolderButton != null) browseDownloadFolderButton.Enabled = enabled;
            if (skipLargeDownloadCheck != null) skipLargeDownloadCheck.Enabled = enabled;
            if (maxDownloadSizeNumber != null) maxDownloadSizeNumber.Enabled = enabled && skipLargeDownloadCheck.Checked;
            if (urlTextBox != null) urlTextBox.Enabled = enabled;
            if (readBrightnessButton != null) readBrightnessButton.Enabled = enabled;
            if (applyBrightnessButton != null) applyBrightnessButton.Enabled = enabled;
            UpdateGroupActionButtons();
        }

        private void SetItemStatus(ListViewItem item, string status, Color color)
        {
            if (item == null) return;
            item.SubItems[2].Text = status;
            item.ForeColor = color;
            item.EnsureVisible();
        }

        private static string FriendlyInstallError(string output)
        {
            string value = output ?? "";
            if (value.IndexOf("INSTALL_FAILED_VERSION_DOWNGRADE", StringComparison.OrdinalIgnoreCase) >= 0) return "版本較舊，請勾選允許降版";
            if (value.IndexOf("INSTALL_FAILED_UPDATE_INCOMPATIBLE", StringComparison.OrdinalIgnoreCase) >= 0) return "簽章與已安裝版本不同";
            if (value.IndexOf("INSTALL_FAILED_INSUFFICIENT_STORAGE", StringComparison.OrdinalIgnoreCase) >= 0) return "手機儲存空間不足";
            if (value.IndexOf("INSTALL_PARSE_FAILED", StringComparison.OrdinalIgnoreCase) >= 0) return "APK 無效或不相容";
            if (value.IndexOf("INSTALL_FAILED_NO_MATCHING_ABIS", StringComparison.OrdinalIgnoreCase) >= 0) return "APK 不支援此手機架構";
            if (value.IndexOf("INSTALL_FAILED_USER_RESTRICTED", StringComparison.OrdinalIgnoreCase) >= 0) return "手機禁止透過 USB 安裝";
            if (value.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0) return "手機尚未允許 USB 偵錯";
            if (value.IndexOf("offline", StringComparison.OrdinalIgnoreCase) >= 0) return "手機連線離線";
            string clean = CleanOutput(value);
            return clean.Length > 70 ? clean.Substring(0, 70) + "..." : (clean.Length == 0 ? "ADB 未回報成功" : clean);
        }

        private static string CleanOutput(string text)
        {
            return (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string Quote(string text)
        {
            return "\"" + (text ?? "").Replace("\"", "\\\"") + "\"";
        }

        private string Prompt(string title, string label, string value)
        {
            using (Form form = new Form())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ClientSize = new Size(420, 150);
                form.BackColor = Card;
                form.ForeColor = TextColor;
                form.Font = Font;
                Label prompt = new Label { Text = label, AutoSize = true, Location = new Point(18, 18) };
                TextBox input = new TextBox { Text = value, Location = new Point(18, 46), Width = 382, BackColor = Card2, ForeColor = TextColor, BorderStyle = BorderStyle.FixedSingle };
                Button ok = NewButton("確定", true, 86);
                ok.Location = new Point(218, 96);
                ok.DialogResult = DialogResult.OK;
                Button cancel = NewButton("取消", false, 86);
                cancel.Location = new Point(314, 96);
                cancel.DialogResult = DialogResult.Cancel;
                form.Controls.Add(prompt);
                form.Controls.Add(input);
                form.Controls.Add(ok);
                form.Controls.Add(cancel);
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                form.Shown += delegate { input.Focus(); input.SelectAll(); };
                return form.ShowDialog(this) == DialogResult.OK ? input.Text : null;
            }
        }
    }
}
