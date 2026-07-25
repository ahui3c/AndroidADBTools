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
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("Android ADB 快速工具")]
[assembly: AssemblyDescription("Android ADB 連線確認、APK 快速安裝與檔案傳輸工具")]
[assembly: AssemblyCompany("AndroidADBTools")]
[assembly: AssemblyProduct("Android ADB 快速工具")]
[assembly: AssemblyCopyright("Copyright © 2026 廖阿輝")]
[assembly: AssemblyVersion("2.0.1.0")]
[assembly: AssemblyFileVersion("2.0.1.0")]
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
        public string SelectedDeviceSerial { get; set; }
        public bool InstallToAllDevices { get; set; }
        public List<WifiDeviceRecord> WifiDevices { get; set; }
        public bool WifiAutoReconnect { get; set; }
        public string SpotreadPath { get; set; }
        public string SpotreadCorrectionPath { get; set; }
        public decimal AutoBrightnessTargetNit { get; set; }
        public decimal AutoBrightnessToleranceNit { get; set; }

        public AppSettings()
        {
            AdbPath = "";
            Groups = new List<ApkGroup>();
            GroupOrder = new List<string>();
            DownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Android手機資料下載");
            SkipLargeDownloadFiles = true;
            MaxDownloadFileSizeGb = 2M;
            SelectedDeviceSerial = "";
            WifiDevices = new List<WifiDeviceRecord>();
            SpotreadPath = "";
            SpotreadCorrectionPath = "";
            AutoBrightnessTargetNit = 200M;
            AutoBrightnessToleranceNit = 2M;
        }
    }

    public sealed class WifiDeviceRecord
    {
        public string Host { get; set; }
        public int PairingPort { get; set; }
        public int DebugPort { get; set; }
        public string DisplayName { get; set; }
        public DateTime LastConnected { get; set; }

        public WifiDeviceRecord()
        {
            Host = "";
            DisplayName = "Android 裝置";
        }

        public string DebugEndpoint
        {
            get
            {
                if (String.IsNullOrWhiteSpace(Host) || DebugPort <= 0) return "";
                return MainForm.FormatNetworkEndpoint(Host, DebugPort);
            }
        }

        public override string ToString()
        {
            string endpoint = DebugEndpoint;
            if (String.IsNullOrWhiteSpace(endpoint))
                endpoint = String.IsNullOrWhiteSpace(Host) ? "尚未設定偵錯位址" : Host;
            string name = String.IsNullOrWhiteSpace(DisplayName) ? "Android 裝置" : DisplayName;
            return name + "　｜　" + endpoint;
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

        public string DisplayName
        {
            get { return String.IsNullOrWhiteSpace(Model) ? "Android 裝置" : Model; }
        }

        public bool IsWireless
        {
            get
            {
                string serial = Serial ?? "";
                return serial.IndexOf(':') >= 0 ||
                    serial.IndexOf("_adb-tls", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    serial.StartsWith("adb-", StringComparison.OrdinalIgnoreCase);
            }
        }

        public string ConnectionLabel { get { return IsWireless ? "Wi-Fi" : "USB"; } }

        public override string ToString()
        {
            return DisplayName + "　｜　" + (Serial ?? "") + "　｜　" + ConnectionLabel;
        }
    }

    public sealed class MdnsServiceInfo
    {
        public string Name { get; set; }
        public string ServiceType { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }

        public bool IsPairing
        {
            get { return (ServiceType ?? "").IndexOf("pairing", StringComparison.OrdinalIgnoreCase) >= 0; }
        }

        public override string ToString()
        {
            return (IsPairing ? "配對" : "偵錯") + "　｜　" + MainForm.FormatNetworkEndpoint(Host, Port) +
                (String.IsNullOrWhiteSpace(Name) ? "" : "　｜　" + Name);
        }
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
        private bool quickTransferring;
        private bool quickInstallDragOver;
        private bool quickTransferDragOver;
        private string quickTransferStatus = "";

        private Label adbStatusLabel;
        private Label deviceStatusLabel;
        private Label deviceDetailLabel;
        private ComboBox deviceSelector;
        private CheckBox installAllDevicesCheck;
        private bool updatingDeviceSelector;
        private Button refreshButton;
        private Button browseAdbButton;
        private Button installGroupButton;
        private Button renameGroupButton;
        private Button deleteGroupButton;
        private Button addGroupApksButton;
        private Button removeGroupApkButton;
        private ListBox groupList;
        private ListView apkList;
        private TextBox logBox;
        private CheckBox downgradeCheck;
        private Label groupTitle;
        private Label groupHint;
        private Panel dropPanel;
        private Panel transferDropPanel;
        private ComboBox quickTransferDestinationComboBox;
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
        private TextBox spotreadPathTextBox;
        private TextBox spotreadCorrectionTextBox;
        private NumericUpDown autoBrightnessTargetNumber;
        private NumericUpDown autoBrightnessToleranceNumber;
        private Button browseSpotreadButton;
        private Button browseSpotreadCorrectionButton;
        private Button testMeterButton;
        private Button openWhitePatternButton;
        private Button startAutoBrightnessButton;
        private Label autoBrightnessStatusLabel;
        private Label autoBrightnessReadingLabel;
        private ProgressBar autoBrightnessProgressBar;
        private bool autoBrightnessRunning;
        private bool autoBrightnessCancelRequested;
        private ToolTip groupNameToolTip;
        private int lastGroupTooltipIndex = -1;
        private ToolTip apkListToolTip;
        private int lastApkTooltipIndex = -1;
        private int groupDragStartIndex = -1;
        private int groupDragInsertIndex = -1;
        private int groupDragLastScrollTick;
        private Point groupDragStartPoint;
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
                await AutoReconnectWifiDevicesAsync(false);
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
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(18, 46),
                Height = 30
            };
            deviceDetailLabel = new Label
            {
                Text = "請開啟 USB 偵錯並連接手機",
                ForeColor = Muted,
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(20, 79),
                Height = 25
            };
            deviceCard.Controls.Add(adbStatusLabel);
            deviceCard.Controls.Add(deviceStatusLabel);
            deviceCard.Controls.Add(deviceDetailLabel);

            Panel deviceControls = new Panel
            {
                Dock = DockStyle.Right,
                Width = 700,
                BackColor = Card
            };
            FlowLayoutPanel statusActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 52,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 5, 0, 0),
                BackColor = Card
            };
            refreshButton = NewButton("重新檢查", true, 108);
            refreshButton.Click += async delegate { await CheckConnectionAsync(); };
            browseAdbButton = NewButton("選擇 adb.exe", false, 132);
            browseAdbButton.Click += BrowseAdb;
            Button helpButton = NewButton("Wi-Fi 連線", false, 112);
            helpButton.Click += ShowConnectionHelp;
            Button aboutButton = NewButton("關於", false, 78);
            aboutButton.Click += ShowAbout;
            statusActions.Controls.Add(refreshButton);
            statusActions.Controls.Add(browseAdbButton);
            statusActions.Controls.Add(helpButton);
            statusActions.Controls.Add(aboutButton);

            FlowLayoutPanel deviceSelectionRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 5, 0, 0),
                BackColor = Card
            };
            installAllDevicesCheck = new CheckBox
            {
                Text = "APK 安裝到全部裝置",
                ForeColor = Muted,
                AutoSize = true,
                Enabled = false,
                Margin = new Padding(12, 7, 2, 0)
            };
            installAllDevicesCheck.CheckedChanged += DeviceInstallSelectionChanged;
            deviceSelector = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Card2,
                ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat,
                Width = 350,
                Enabled = false,
                Margin = new Padding(8, 2, 0, 0)
            };
            deviceSelector.SelectedIndexChanged += DeviceSelectorChanged;
            Label deviceSelectorLabel = new Label
            {
                Text = "操作裝置",
                ForeColor = Muted,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0)
            };
            deviceSelectionRow.Controls.Add(installAllDevicesCheck);
            deviceSelectionRow.Controls.Add(deviceSelector);
            deviceSelectionRow.Controls.Add(deviceSelectorLabel);
            deviceControls.Controls.Add(deviceSelectionRow);
            deviceControls.Controls.Add(statusActions);
            deviceCard.Controls.Add(deviceControls);
            deviceControls.BringToFront();
            deviceCard.Resize += delegate
            {
                int width = Math.Max(ScaleValue(220, currentDpiScale),
                    deviceCard.ClientSize.Width - deviceControls.Width - ScaleValue(44, currentDpiScale));
                deviceStatusLabel.Width = width;
                deviceDetailLabel.Width = width;
            };
            deviceControls.Resize += delegate
            {
                int width = Math.Max(ScaleValue(220, currentDpiScale),
                    deviceCard.ClientSize.Width - deviceControls.Width - ScaleValue(44, currentDpiScale));
                deviceStatusLabel.Width = width;
                deviceDetailLabel.Width = width;
            };

            mainTabs = new ModernTabControl();
            mainTabs.Dock = DockStyle.Fill;
            mainTabs.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold);
            mainTabs.BackColor = Bg;
            mainTabs.ItemSize = new Size(165, 46);
            TabPage groupsTab = NewTab("▦  常用 APK 安裝", Color.FromArgb(53, 120, 219));
            TabPage singleTab = NewTab("⇩  快速安裝 / 傳輸", Color.FromArgb(126, 87, 194));
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
                Height = 98,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Card,
            };
            groupButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            groupButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            groupButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            groupButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            Button addGroup = NewButton("＋ 新增", true, 90);
            renameGroupButton = NewButton("編輯名稱", false, 110);
            deleteGroupButton = NewButton("刪除組合", false, 95);
            addGroup.Dock = DockStyle.Fill;
            renameGroupButton.Dock = DockStyle.Fill;
            deleteGroupButton.Dock = DockStyle.Fill;
            addGroup.Click += AddGroup;
            renameGroupButton.Click += RenameGroup;
            deleteGroupButton.Click += DeleteGroup;
            groupButtons.Controls.Add(addGroup, 0, 0);
            groupButtons.SetColumnSpan(addGroup, 2);
            groupButtons.Controls.Add(renameGroupButton, 0, 1);
            groupButtons.Controls.Add(deleteGroupButton, 1, 1);
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
                HorizontalScrollbar = false,
                AllowDrop = true
            };
            groupList.HandleCreated += delegate { SetWindowTheme(groupList.Handle, "DarkMode_Explorer", null); };
            groupList.DrawItem += DrawGroupListItem;
            groupNameToolTip = new ToolTip { InitialDelay = 300, ReshowDelay = 100, AutoPopDelay = 8000, ShowAlways = true };
            groupList.MouseDown += GroupListMouseDown;
            groupList.MouseMove += GroupListDragMouseMove;
            groupList.MouseMove += GroupListMouseMove;
            groupList.MouseUp += delegate { groupDragStartIndex = -1; };
            groupList.MouseLeave += delegate { lastGroupTooltipIndex = -1; groupNameToolTip.Hide(groupList); };
            groupList.DragOver += GroupListDragOver;
            groupList.DragDrop += GroupListDragDrop;
            groupList.DragLeave += delegate { SetGroupDragInsertIndex(-1); };
            groupList.SelectedIndexChanged += delegate { ShowSelectedGroup(); };
            groupList.MouseDoubleClick += GroupListMouseDoubleClick;
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
                Text = "拖曳左側組合可調整順序；雙擊自訂組合可編輯名稱",
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
            apkListToolTip = new ToolTip { InitialDelay = 300, ReshowDelay = 100, AutoPopDelay = 12000, ShowAlways = true };
            apkList.MouseMove += ApkListMouseMove;
            apkList.MouseLeave += delegate
            {
                lastApkTooltipIndex = -1;
                apkListToolTip.Hide(apkList);
            };
            apkList.DragEnter += GroupApkDragEnter;
            apkList.DragDrop += GroupApkDragDrop;
            right.Controls.Add(apkList);
            apkList.BringToFront();
        }

        private void BuildSingleTab(TabPage tab)
        {
            TableLayoutPanel dropAreas = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Bg,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            dropAreas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            dropAreas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            dropAreas.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            dropPanel = NewCard();
            dropPanel.Dock = DockStyle.Fill;
            dropPanel.Margin = new Padding(0, 0, 6, 0);
            dropPanel.AllowDrop = true;
            dropPanel.Cursor = Cursors.Hand;
            dropPanel.DragEnter += ApkDragEnter;
            dropPanel.DragLeave += ApkDragLeave;
            dropPanel.DragDrop += ApkDragDrop;
            dropPanel.Click += ChooseSingleApks;
            dropPanel.Paint += DrawQuickInstallDropPanel;

            transferDropPanel = NewCard();
            transferDropPanel.Dock = DockStyle.Fill;
            transferDropPanel.Margin = new Padding(6, 0, 0, 0);
            transferDropPanel.AllowDrop = true;
            transferDropPanel.DragEnter += QuickTransferDragEnter;
            transferDropPanel.DragLeave += QuickTransferDragLeave;
            transferDropPanel.DragDrop += QuickTransferDragDrop;
            transferDropPanel.Paint += DrawQuickTransferDropPanel;

            Label transferDestinationLabel = new Label
            {
                Text = "手機目的地",
                ForeColor = Muted,
                AutoSize = true,
                Location = new Point(38, 38)
            };
            quickTransferDestinationComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Card2,
                ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat,
                Width = 220,
                Location = new Point(132, 31)
            };
            quickTransferDestinationComboBox.Items.AddRange(new object[]
            {
                "Download\\",
                "DCIM\\",
                "Pictures\\",
                "\\（內部儲存根目錄）"
            });
            quickTransferDestinationComboBox.SelectedIndex = 0;
            quickTransferDestinationComboBox.SelectedIndexChanged += delegate
            {
                if (transferDropPanel != null) transferDropPanel.Invalidate();
            };
            quickTransferDestinationComboBox.HandleCreated += delegate
            {
                SetWindowTheme(quickTransferDestinationComboBox.Handle, "DarkMode_Explorer", null);
            };
            transferDropPanel.Controls.Add(transferDestinationLabel);
            transferDropPanel.Controls.Add(quickTransferDestinationComboBox);

            tab.Padding = new Padding(8);
            dropAreas.Controls.Add(dropPanel, 0, 0);
            dropAreas.Controls.Add(transferDropPanel, 1, 0);
            tab.Controls.Add(dropAreas);
        }

        private void DrawQuickInstallDropPanel(object sender, PaintEventArgs e)
        {
            Panel panel = (Panel)sender;
            Rectangle border = new Rectangle(20, 20, Math.Max(1, panel.ClientSize.Width - 41), Math.Max(1, panel.ClientSize.Height - 41));
            Color borderColor = quickInstalling ? Color.FromArgb(255, 190, 75) :
                (quickInstallDragOver ? Color.FromArgb(126, 87, 194) : Accent);
            using (Pen pen = new Pen(borderColor, quickInstallDragOver ? 3F : 2F))
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
                DrawSmoothText(e.Graphics, quickInstalling ? "正在安裝 APK..." : "把 APK 拖到這裡安裝", titleFont,
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
                Text = "上方保留手動調整；下方可搭配 ArgyllCMS 與外接色度計，依實測 nit 全自動校準。",
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
                Height = 228,
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
                Height = 0,
                Visible = false
            };

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
                Height = 56,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Card2,
                Padding = new Padding(0, 12, 0, 0)
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
                Dock = DockStyle.Top,
                Height = 48,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Card2,
                Padding = new Padding(0, 4, 0, 0)
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
            controlCard.Controls.Add(actions);
            actions.BringToFront();

            Panel autoCard = BuildAutoBrightnessCard();
            brightnessViewport.Controls.Add(autoCard);
            autoCard.BringToFront();
            brightnessViewport.AutoScrollMinSize = new Size(0, controlCard.Height + autoCard.Height + 18);

            brightnessUpdateTimer = new Timer();
            brightnessUpdateTimer.Interval = 180;
            brightnessUpdateTimer.Tick += async delegate
            {
                brightnessUpdateTimer.Stop();
                await ApplyBrightnessAsync();
            };
        }

        private Panel BuildAutoBrightnessCard()
        {
            Panel card = new Panel
            {
                Dock = DockStyle.Top,
                Height = 388,
                BackColor = Color.FromArgb(30, 38, 49),
                Padding = new Padding(16),
                Margin = new Padding(0, 12, 0, 0)
            };
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 8,
                BackColor = card.BackColor,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Label title = new Label
            {
                Text = "全自動調整亮度（實測 nit 閉迴路）",
                ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 14F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(title, 0, 0);
            layout.SetColumnSpan(title, 4);
            Label note = new Label
            {
                Text = "將 Calibrite Display Plus HL 感測面貼平手機中央並顯示全白畫面。程式會反覆量測 cd/m²、調整 Android 亮度，直到接近目標值。HL 是否可用以 spotread 實際辨識結果為準。",
                ForeColor = Muted,
                Dock = DockStyle.Fill,
                AutoEllipsis = true
            };
            layout.Controls.Add(note, 0, 1);
            layout.SetColumnSpan(note, 4);

            spotreadPathTextBox = BrightnessToolTextBox(settings.SpotreadPath);
            browseSpotreadButton = NewButton("選擇 spotread.exe", false, 124);
            browseSpotreadButton.Dock = DockStyle.Fill;
            testMeterButton = NewButton("試量測", false, 112);
            testMeterButton.Dock = DockStyle.Fill;
            layout.Controls.Add(BrightnessToolLabel("ArgyllCMS spotread"), 0, 2);
            layout.Controls.Add(spotreadPathTextBox, 1, 2);
            layout.Controls.Add(browseSpotreadButton, 2, 2);
            layout.Controls.Add(testMeterButton, 3, 2);

            spotreadCorrectionTextBox = BrightnessToolTextBox(settings.SpotreadCorrectionPath);
            browseSpotreadCorrectionButton = NewButton("選擇校正檔", false, 112);
            browseSpotreadCorrectionButton.Dock = DockStyle.Fill;
            Button clearCorrectionButton = NewButton("清除校正檔", false, 112);
            clearCorrectionButton.Dock = DockStyle.Fill;
            layout.Controls.Add(BrightnessToolLabel("選用 CCSS／CCMX"), 0, 3);
            layout.Controls.Add(spotreadCorrectionTextBox, 1, 3);
            layout.Controls.Add(browseSpotreadCorrectionButton, 2, 3);
            layout.Controls.Add(clearCorrectionButton, 3, 3);

            autoBrightnessTargetNumber = BrightnessNitNumber(10M, 10000M, settings.AutoBrightnessTargetNit, 0);
            autoBrightnessToleranceNumber = BrightnessNitNumber(0.5M, 100M, settings.AutoBrightnessToleranceNit, 1);
            FlowLayoutPanel targetRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = card.BackColor, Padding = new Padding(0, 4, 0, 0) };
            targetRow.Controls.Add(autoBrightnessTargetNumber);
            targetRow.Controls.Add(new Label { Text = "nit　　允許誤差", ForeColor = Muted, AutoSize = true, Padding = new Padding(6, 8, 0, 0) });
            targetRow.Controls.Add(autoBrightnessToleranceNumber);
            targetRow.Controls.Add(new Label { Text = "nit", ForeColor = Muted, AutoSize = true, Padding = new Padding(6, 8, 0, 0) });
            layout.Controls.Add(BrightnessToolLabel("目標真實亮度"), 0, 4);
            layout.Controls.Add(targetRow, 1, 4);
            layout.SetColumnSpan(targetRow, 3);

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = card.BackColor, Padding = new Padding(0, 5, 0, 0) };
            openWhitePatternButton = NewButton("手機開啟白色測試圖", false, 170);
            startAutoBrightnessButton = NewButton("開始全自動調整", true, 155);
            actions.Controls.Add(openWhitePatternButton);
            actions.Controls.Add(startAutoBrightnessButton);
            actions.Controls.Add(new Label { Text = "最多 18 次量測；每次調整後會等待畫面穩定。", ForeColor = Muted, AutoSize = true, Padding = new Padding(12, 10, 0, 0) });
            layout.Controls.Add(actions, 0, 5);
            layout.SetColumnSpan(actions, 4);

            autoBrightnessProgressBar = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 18, Value = 0, Style = ProgressBarStyle.Continuous, Margin = new Padding(0, 7, 0, 7) };
            layout.Controls.Add(autoBrightnessProgressBar, 0, 6);
            layout.SetColumnSpan(autoBrightnessProgressBar, 4);
            autoBrightnessReadingLabel = new Label { Text = "尚未量測", ForeColor = Muted, Dock = DockStyle.Top, Height = 24, AutoEllipsis = true };
            autoBrightnessStatusLabel = new Label { Text = "請先選擇 ArgyllCMS bin 資料夾中的 spotread.exe，並按「試量測」確認儀器。", ForeColor = Muted, Dock = DockStyle.Fill, AutoEllipsis = true };
            Panel statePanel = new Panel { Dock = DockStyle.Fill, BackColor = card.BackColor };
            statePanel.Controls.Add(autoBrightnessStatusLabel);
            statePanel.Controls.Add(autoBrightnessReadingLabel);
            autoBrightnessReadingLabel.BringToFront();
            autoBrightnessStatusLabel.Dock = DockStyle.Fill;
            autoBrightnessStatusLabel.Padding = new Padding(0, 24, 0, 0);
            layout.Controls.Add(statePanel, 0, 7);
            layout.SetColumnSpan(statePanel, 4);
            card.Controls.Add(layout);

            browseSpotreadButton.Click += BrowseSpotread;
            browseSpotreadCorrectionButton.Click += BrowseSpotreadCorrection;
            clearCorrectionButton.Click += delegate
            {
                spotreadCorrectionTextBox.Text = "";
                settings.SpotreadCorrectionPath = "";
                SaveSettings();
            };
            spotreadPathTextBox.TextChanged += delegate { settings.SpotreadPath = spotreadPathTextBox.Text.Trim(); SaveSettings(); };
            spotreadCorrectionTextBox.TextChanged += delegate { settings.SpotreadCorrectionPath = spotreadCorrectionTextBox.Text.Trim(); SaveSettings(); };
            autoBrightnessTargetNumber.ValueChanged += delegate { settings.AutoBrightnessTargetNit = autoBrightnessTargetNumber.Value; SaveSettings(); };
            autoBrightnessToleranceNumber.ValueChanged += delegate { settings.AutoBrightnessToleranceNit = autoBrightnessToleranceNumber.Value; SaveSettings(); };
            testMeterButton.Click += async delegate { await TestBrightnessMeterAsync(); };
            openWhitePatternButton.Click += async delegate { await OpenWhitePatternOnPhoneAsync(); };
            startAutoBrightnessButton.Click += async delegate
            {
                if (autoBrightnessRunning)
                {
                    autoBrightnessCancelRequested = true;
                    startAutoBrightnessButton.Text = "正在停止…";
                    startAutoBrightnessButton.Enabled = false;
                    return;
                }
                await RunAutomaticBrightnessAsync();
            };
            return card;
        }

        private TextBox BrightnessToolTextBox(string text)
        {
            TextBox box = new TextBox { Text = text ?? "", Dock = DockStyle.Fill, BackColor = Bg, ForeColor = TextColor, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 6, 8, 6) };
            box.HandleCreated += delegate { SetWindowTheme(box.Handle, "DarkMode_Explorer", null); };
            return box;
        }

        private Label BrightnessToolLabel(string text)
        {
            return new Label { Text = text, ForeColor = TextColor, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        }

        private NumericUpDown BrightnessNitNumber(decimal minimum, decimal maximum, decimal value, int decimals)
        {
            value = Math.Max(minimum, Math.Min(maximum, value));
            NumericUpDown number = new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                Value = value,
                DecimalPlaces = decimals,
                Increment = decimals > 0 ? 0.5M : 10M,
                Width = 105,
                Height = 34,
                TextAlign = HorizontalAlignment.Right,
                BackColor = Bg,
                ForeColor = TextColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            number.HandleCreated += delegate { SetWindowTheme(number.Handle, "DarkMode_Explorer", null); };
            return number;
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
                Control target = Control.FromHandle(message.HWnd);
                for (Control current = target; current != null; current = current.Parent)
                    if (current == brightnessTrackBar || current == brightnessNumber) return true;
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
                Text = "快速下載手機圖片影音資料",
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
                int accentInset = ScaleValue(6, currentDpiScale);
                int accentWidth = Math.Max(3, ScaleValue(4, currentDpiScale));
                using (SolidBrush accentBrush = new SolidBrush(Accent))
                    e.Graphics.FillRectangle(accentBrush, e.Bounds.Left, e.Bounds.Top + accentInset,
                        accentWidth, Math.Max(1, e.Bounds.Height - accentInset * 2));
            }
            ApkGroup group = groupList.Items[e.Index] as ApkGroup;
            string name = group == null ? groupList.Items[e.Index].ToString() : group.Name;
            string count = group == null ? "" : (group.IsFolderGroup ? "資料夾同步　" : "") + group.Apks.Count + " 個 APK";
            int textLeft = e.Bounds.X + ScaleValue(14, currentDpiScale);
            int horizontalPadding = ScaleValue(14, currentDpiScale);
            int nameHeight = Math.Max(groupList.Font.Height + ScaleValue(4, currentDpiScale),
                ScaleValue(24, currentDpiScale));
            int countHeight;
            int contentGap = ScaleValue(2, currentDpiScale);
            using (Font countFont = new Font(groupList.Font.FontFamily, 8.5F))
            {
                countHeight = Math.Max(countFont.Height + ScaleValue(2, currentDpiScale),
                    ScaleValue(18, currentDpiScale));
                int contentHeight = nameHeight + contentGap + countHeight;
                int contentTop = e.Bounds.Top + Math.Max(0, (e.Bounds.Height - contentHeight) / 2);

                if (group != null && group.IsFolderGroup)
                {
                    int iconSize = ScaleValue(19, currentDpiScale);
                    int iconTop = e.Bounds.Top + Math.Max(0, (e.Bounds.Height - iconSize) / 2);
                    DrawFolderIcon(e.Graphics, new Rectangle(textLeft, iconTop, iconSize, iconSize));
                    textLeft += iconSize + ScaleValue(9, currentDpiScale);
                }

                Rectangle nameRect = new Rectangle(textLeft, contentTop,
                    Math.Max(20, e.Bounds.Right - textLeft - horizontalPadding), nameHeight);
                Rectangle countRect = new Rectangle(textLeft, contentTop + nameHeight + contentGap,
                    Math.Max(20, e.Bounds.Right - textLeft - horizontalPadding), countHeight);
                DrawSmoothText(e.Graphics, name, groupList.Font, selected ? Color.White : TextColor, nameRect,
                    StringAlignment.Near, StringAlignment.Center, false);
                DrawSmoothText(e.Graphics, count, countFont, selected ? Color.White : Muted, countRect,
                    StringAlignment.Near, StringAlignment.Center, true);
            }

            if (groupDragInsertIndex == e.Index ||
                (groupDragInsertIndex == groupList.Items.Count && e.Index == groupList.Items.Count - 1))
            {
                int y = groupDragInsertIndex == groupList.Items.Count ? e.Bounds.Bottom - 2 : e.Bounds.Top + 1;
                using (Pen pen = new Pen(Accent, Math.Max(2F, currentDpiScale * 2F)))
                    e.Graphics.DrawLine(pen, e.Bounds.Left + 6, y, e.Bounds.Right - 6, y);
            }
        }

        private void DrawQuickTransferDropPanel(object sender, PaintEventArgs e)
        {
            Panel panel = (Panel)sender;
            Rectangle border = new Rectangle(20, 20, Math.Max(1, panel.ClientSize.Width - 41),
                Math.Max(1, panel.ClientSize.Height - 41));
            Color transferAccent = Color.FromArgb(35, 156, 181);
            Color borderColor = quickTransferring ? Color.FromArgb(255, 190, 75) :
                (quickTransferDragOver ? Color.FromArgb(65, 201, 138) : transferAccent);
            using (Pen pen = new Pen(borderColor, quickTransferDragOver ? 3F : 2F))
            {
                pen.DashStyle = DashStyle.Dash;
                e.Graphics.DrawRectangle(pen, border);
            }
            int centerY = panel.ClientSize.Height / 2;
            Rectangle titleBounds = new Rectangle(40, centerY - 70,
                Math.Max(1, panel.ClientSize.Width - 80), 64);
            Rectangle hintBounds = new Rectangle(40, centerY + 4,
                Math.Max(1, panel.ClientSize.Width - 80), 96);
            using (Font titleFont = new Font(Font.FontFamily, 20F, FontStyle.Bold))
            using (Font hintFont = new Font(Font.FontFamily, 11F, FontStyle.Regular))
            {
                DrawSmoothText(e.Graphics, quickTransferring ? "正在傳輸到手機..." : "把檔案或資料夾拖到這裡",
                    titleFont, quickTransferring ? Color.FromArgb(255, 190, 75) : TextColor,
                    titleBounds, StringAlignment.Center, StringAlignment.Center, false);
                string hint = quickTransferring
                    ? (String.IsNullOrWhiteSpace(quickTransferStatus) ? "請勿中斷手機連線" : quickTransferStatus)
                    : "放開後立即傳輸到手機 " + QuickTransferDestinationLabel() +
                        "\n資料夾名稱與完整子目錄結構都會保留";
                DrawSmoothText(e.Graphics, hint, hintFont, Muted, hintBounds,
                    StringAlignment.Center, StringAlignment.Near, false);
            }
        }

        private void ApkListMouseMove(object sender, MouseEventArgs e)
        {
            ListView list = sender as ListView;
            if (list == null || apkListToolTip == null) return;
            ListViewItem item = list.GetItemAt(e.X, e.Y);
            int index = item == null ? -1 : item.Index;
            if (index == lastApkTooltipIndex) return;

            lastApkTooltipIndex = index;
            apkListToolTip.Hide(list);
            if (item == null) return;

            string path = item.Tag as string;
            if (String.IsNullOrWhiteSpace(path)) return;
            string fileName = Path.GetFileName(path);
            string text = "完整檔名：" + fileName + Environment.NewLine +
                "完整位置：" + path;
            apkListToolTip.Show(text, list, e.X + ScaleValue(16, currentDpiScale),
                e.Y + ScaleValue(20, currentDpiScale), 12000);
        }

        private void GroupListMouseDown(object sender, MouseEventArgs e)
        {
            groupDragStartIndex = -1;
            if (busy || e.Button != MouseButtons.Left) return;
            int index = groupList.IndexFromPoint(e.Location);
            if (index < 0 || index >= groupList.Items.Count) return;
            groupDragStartIndex = index;
            groupDragStartPoint = e.Location;
        }

        private void GroupListMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (busy || e.Button != MouseButtons.Left) return;
            int index = groupList.IndexFromPoint(e.Location);
            if (index < 0 || index >= groupList.Items.Count) return;
            groupList.SelectedIndex = index;
            ApkGroup group = SelectedGroup();
            if (group != null && !group.IsFolderGroup) RenameGroup(groupList, EventArgs.Empty);
        }

        private void GroupListDragMouseMove(object sender, MouseEventArgs e)
        {
            if (busy || e.Button != MouseButtons.Left || groupDragStartIndex < 0 ||
                groupDragStartIndex >= groupList.Items.Count) return;
            Size dragSize = SystemInformation.DragSize;
            Rectangle dragBounds = new Rectangle(groupDragStartPoint.X - dragSize.Width / 2,
                groupDragStartPoint.Y - dragSize.Height / 2, dragSize.Width, dragSize.Height);
            if (dragBounds.Contains(e.Location)) return;
            ApkGroup group = groupList.Items[groupDragStartIndex] as ApkGroup;
            groupDragStartIndex = -1;
            if (group == null) return;
            groupNameToolTip.Hide(groupList);
            groupList.DoDragDrop(group, DragDropEffects.Move);
        }

        private void GroupListDragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            if (busy || !e.Data.GetDataPresent(typeof(ApkGroup)))
            {
                SetGroupDragInsertIndex(-1);
                return;
            }
            e.Effect = DragDropEffects.Move;
            Point location = groupList.PointToClient(new Point(e.X, e.Y));
            AutoScrollGroupListDuringDrag(location);
            SetGroupDragInsertIndex(GroupInsertIndexFromPoint(location));
        }

        private void AutoScrollGroupListDuringDrag(Point location)
        {
            int now = Environment.TickCount;
            if (unchecked((uint)(now - groupDragLastScrollTick)) < 120U || groupList.Items.Count == 0) return;
            int threshold = Math.Max(16, groupList.ItemHeight / 2);
            int topIndex = groupList.TopIndex;
            if (location.Y < threshold && topIndex > 0)
                groupList.TopIndex = topIndex - 1;
            else if (location.Y > groupList.ClientSize.Height - threshold)
            {
                int visibleCount = Math.Max(1, groupList.ClientSize.Height / Math.Max(1, groupList.ItemHeight));
                int maximumTop = Math.Max(0, groupList.Items.Count - visibleCount);
                if (topIndex < maximumTop) groupList.TopIndex = topIndex + 1;
            }
            else return;
            groupDragLastScrollTick = now;
        }

        private int GroupInsertIndexFromPoint(Point location)
        {
            for (int i = 0; i < groupList.Items.Count; i++)
            {
                Rectangle bounds = groupList.GetItemRectangle(i);
                if (location.Y < bounds.Top + bounds.Height / 2) return i;
            }
            return groupList.Items.Count;
        }

        private void SetGroupDragInsertIndex(int index)
        {
            if (groupDragInsertIndex == index) return;
            groupDragInsertIndex = index;
            groupList.Invalidate();
        }

        private void GroupListDragDrop(object sender, DragEventArgs e)
        {
            int insertIndex = groupDragInsertIndex;
            SetGroupDragInsertIndex(-1);
            if (busy || insertIndex < 0 || !e.Data.GetDataPresent(typeof(ApkGroup))) return;
            ApkGroup selected = e.Data.GetData(typeof(ApkGroup)) as ApkGroup;
            if (selected == null) return;
            List<ApkGroup> groups = AllGroups();
            int sourceIndex = groups.FindIndex(delegate(ApkGroup group) { return group.Id == selected.Id; });
            if (sourceIndex < 0) return;
            insertIndex = Math.Max(0, Math.Min(insertIndex, groups.Count));
            ApkGroup moved = groups[sourceIndex];
            groups.RemoveAt(sourceIndex);
            if (insertIndex > sourceIndex) insertIndex--;
            insertIndex = Math.Max(0, Math.Min(insertIndex, groups.Count));
            groups.Insert(insertIndex, moved);
            settings.GroupOrder = groups.Select(delegate(ApkGroup group) { return group.Id; }).ToList();
            SaveSettings();
            RefreshGroups(selected.Id);
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
                        if (loaded.WifiDevices == null) loaded.WifiDevices = new List<WifiDeviceRecord>();
                        if (loaded.AutoBrightnessTargetNit <= 0) loaded.AutoBrightnessTargetNit = 200M;
                        if (loaded.AutoBrightnessToleranceNit <= 0) loaded.AutoBrightnessToleranceNit = 2M;
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
            RefreshDeviceSelector(new List<DeviceInfo>());
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
                RefreshDeviceSelector(new List<DeviceInfo>());
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

        private void RefreshDeviceSelector(List<DeviceInfo> ready)
        {
            if (deviceSelector == null || installAllDevicesCheck == null) return;
            ready = ready ?? new List<DeviceInfo>();
            updatingDeviceSelector = true;
            try
            {
                string rememberedSerial = settings.SelectedDeviceSerial ?? "";
                deviceSelector.BeginUpdate();
                deviceSelector.Items.Clear();
                foreach (DeviceInfo device in ready) deviceSelector.Items.Add(device);
                deviceSelector.EndUpdate();

                int selectedIndex = ready.FindIndex(delegate(DeviceInfo device)
                {
                    return String.Equals(device.Serial, rememberedSerial, StringComparison.OrdinalIgnoreCase);
                });
                if (selectedIndex < 0 && ready.Count > 0) selectedIndex = 0;
                deviceSelector.SelectedIndex = selectedIndex;
                deviceSelector.Enabled = ready.Count > 1;
                installAllDevicesCheck.Enabled = ready.Count > 1;
                installAllDevicesCheck.Checked = ready.Count > 1 && settings.InstallToAllDevices;

                if (selectedIndex >= 0 && !String.Equals(settings.SelectedDeviceSerial,
                    ready[selectedIndex].Serial, StringComparison.OrdinalIgnoreCase))
                {
                    settings.SelectedDeviceSerial = ready[selectedIndex].Serial;
                    SaveSettings();
                }
            }
            finally
            {
                updatingDeviceSelector = false;
            }
        }

        private void DeviceSelectorChanged(object sender, EventArgs e)
        {
            if (updatingDeviceSelector) return;
            DeviceInfo selected = deviceSelector == null ? null : deviceSelector.SelectedItem as DeviceInfo;
            if (selected == null) return;
            settings.SelectedDeviceSerial = selected.Serial;
            SaveSettings();
            UpdateDeviceSelectionDetail();
        }

        private void DeviceInstallSelectionChanged(object sender, EventArgs e)
        {
            if (updatingDeviceSelector) return;
            settings.InstallToAllDevices = installAllDevicesCheck != null && installAllDevicesCheck.Checked;
            SaveSettings();
            UpdateDeviceSelectionDetail();
        }

        private void UpdateDeviceSelectionDetail()
        {
            List<DeviceInfo> ready = ReadyDevices();
            if (ready.Count == 0 || deviceDetailLabel == null) return;
            DeviceInfo primary = ReadyDevice();
            if (primary == null) return;
            bool all = installAllDevicesCheck != null && installAllDevicesCheck.Enabled &&
                installAllDevicesCheck.Checked;
            deviceDetailLabel.Text = all
                ? "APK 安裝目標：全部 " + ready.Count + " 台　｜　其他功能：" + primary
                : "目前操作：" + primary;
        }

        private void UpdateDeviceCard()
        {
            List<DeviceInfo> ready = devices.Where(delegate(DeviceInfo d) { return d.State == "device"; }).ToList();
            List<DeviceInfo> unauthorized = devices.Where(delegate(DeviceInfo d) { return d.State == "unauthorized"; }).ToList();
            List<DeviceInfo> offline = devices.Where(delegate(DeviceInfo d) { return d.State == "offline"; }).ToList();
            RefreshDeviceSelector(ready);
            if (ready.Count > 0)
            {
                deviceStatusLabel.Text = ready.Count == 1 ? "手機已正確連線" : "已連線 " + ready.Count + " 台手機";
                deviceStatusLabel.ForeColor = Green;
                UpdateDeviceSelectionDetail();
                bool wifiRecordChanged = false;
                foreach (DeviceInfo device in ready)
                {
                    Log("連線成功：" + device);
                    if (!device.IsWireless || settings.WifiDevices == null) continue;
                    WifiDeviceRecord record = settings.WifiDevices.FirstOrDefault(delegate(WifiDeviceRecord item)
                    {
                        return String.Equals(item.DebugEndpoint, device.Serial, StringComparison.OrdinalIgnoreCase);
                    });
                    if (record != null && !String.Equals(record.DisplayName, device.DisplayName, StringComparison.Ordinal))
                    {
                        record.DisplayName = device.DisplayName;
                        wifiRecordChanged = true;
                    }
                }
                if (wifiRecordChanged) SaveSettings();
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
            using (Form form = new Form())
            {
                float scale = Math.Max(1F, currentDpiScale);
                Rectangle area = Screen.FromControl(this).WorkingArea;
                form.Text = "ADB 連線與 Wi-Fi 配對";
                form.StartPosition = FormStartPosition.CenterParent;
                form.BackColor = Bg;
                form.ForeColor = TextColor;
                form.Font = Font;
                form.AutoScaleMode = AutoScaleMode.None;
                form.MinimumSize = new Size(Math.Min(ScaleValue(920, scale), area.Width), Math.Min(ScaleValue(700, scale), area.Height));
                form.Size = new Size(Math.Min(ScaleValue(1120, scale), area.Width - ScaleValue(24, scale)),
                    Math.Min(ScaleValue(840, scale), area.Height - ScaleValue(24, scale)));
                form.ShowIcon = false;

                Panel header = new Panel { Dock = DockStyle.Top, Height = ScaleValue(82, scale), Padding = ScalePadding(new Padding(24, 14, 24, 6), scale) };
                header.Controls.Add(new Label
                {
                    Text = "直接進行 ADB Wi-Fi 配對與連線",
                    Dock = DockStyle.Top,
                    Height = ScaleValue(36, scale),
                    Font = new Font(Font.FontFamily, 17F, FontStyle.Bold),
                    ForeColor = TextColor
                });
                header.Controls.Add(new Label
                {
                    Text = "Android 11 以上可使用六位數配對碼；手機與電腦必須位於可互通的同一區域網路。",
                    Dock = DockStyle.Bottom,
                    Height = ScaleValue(28, scale),
                    ForeColor = Muted
                });

                ModernTabControl tabs = new ModernTabControl
                {
                    Dock = DockStyle.Fill,
                    BackColor = Bg,
                    Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
                    ItemSize = ScaleSize(new Size(250, 44), scale)
                };
                tabs.TabPages.Add(CreateWifiConnectionPage(form));
                tabs.TabPages.Add(CreateConnectionHelpPage("USB 連線教學", Color.FromArgb(53, 120, 219), UsbConnectionHelpText()));

                FlowLayoutPanel footer = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = ScaleValue(58, scale),
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = ScalePadding(new Padding(10), scale),
                    BackColor = Bg
                };
                Button close = NewButton("關閉", true, 110);
                close.Size = ScaleSize(new Size(110, 36), scale);
                close.MinimumSize = close.Size;
                close.Click += delegate { form.Close(); };
                footer.Controls.Add(close);

                form.Controls.Add(tabs);
                form.Controls.Add(footer);
                form.Controls.Add(header);
                tabs.BringToFront();
                footer.BringToFront();
                ApplySmoothTextRendering(form);
                form.ShowDialog(this);
            }
        }

        private TabPage CreateWifiConnectionPage(Form owner)
        {
            float scale = Math.Max(1F, currentDpiScale);
            TabPage page = NewTab("Wi-Fi 配對與連線", Color.FromArgb(32, 151, 116));
            page.Padding = ScalePadding(new Padding(12), scale);
            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Card };
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = ScaleValue(650, scale),
                BackColor = Card,
                Padding = ScalePadding(new Padding(14), scale),
                ColumnCount = 1,
                RowCount = 4
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(72, scale)));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(178, scale)));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(170, scale)));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(180, scale)));

            Label compatibility = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = Card2,
                ForeColor = Muted,
                Padding = new Padding(14, 10, 14, 8),
                Text = "正在檢查 ADB 版本與無線偵錯相容性…",
                AutoEllipsis = true
            };
            root.Controls.Add(compatibility, 0, 0);

            TableLayoutPanel inputCard = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Card2,
                Padding = ScalePadding(new Padding(14, 10, 14, 10), scale),
                ColumnCount = 4,
                RowCount = 4,
                Margin = ScalePadding(new Padding(0, 10, 0, 0), scale)
            };
            inputCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            inputCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
            inputCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
            inputCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
            inputCard.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(28, scale)));
            inputCard.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(38, scale)));
            inputCard.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(28, scale)));
            inputCard.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(40, scale)));

            TextBox host = WifiInput("例如 192.168.1.20");
            NumericUpDown pairingPort = WifiPortInput();
            TextBox pairingCode = WifiInput("六位數配對碼");
            pairingCode.MaxLength = 6;
            NumericUpDown debugPort = WifiPortInput();
            Label actionState = new Label { Text = "請輸入手機無線偵錯畫面顯示的資料。", ForeColor = Muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            Button pairButton = NewButton("開始配對", true, 112);
            Button connectButton = NewButton("連線", true, 112);
            pairButton.Dock = DockStyle.Fill;
            connectButton.Dock = DockStyle.Fill;

            inputCard.Controls.Add(WifiFieldLabel("手機 IP／主機名稱"), 0, 0);
            inputCard.Controls.Add(WifiFieldLabel("配對 Port"), 1, 0);
            inputCard.Controls.Add(WifiFieldLabel("六位數配對碼"), 2, 0);
            inputCard.Controls.Add(new Label(), 3, 0);
            inputCard.Controls.Add(host, 0, 1);
            inputCard.Controls.Add(pairingPort, 1, 1);
            inputCard.Controls.Add(pairingCode, 2, 1);
            inputCard.Controls.Add(pairButton, 3, 1);
            inputCard.Controls.Add(WifiFieldLabel("偵錯連線 Port（通常與配對 Port 不同）"), 0, 2);
            inputCard.SetColumnSpan(inputCard.GetControlFromPosition(0, 2), 2);
            inputCard.Controls.Add(actionState, 2, 2);
            inputCard.SetColumnSpan(actionState, 2);
            inputCard.Controls.Add(debugPort, 0, 3);
            inputCard.Controls.Add(connectButton, 1, 3);
            inputCard.SetColumnSpan(actionState, 2);
            root.Controls.Add(inputCard, 0, 1);

            TableLayoutPanel discovery = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = ScalePadding(new Padding(0, 10, 0, 0), scale) };
            discovery.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            discovery.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            ListBox mdnsList = WifiListBox();
            ListBox recordsList = WifiListBox();
            Button scanButton;
            Button removeButton;
            Panel mdnsCard = WifiListCard("mDNS 區域網路搜尋", mdnsList, out scanButton, "開始搜尋");
            Panel recordsCard = WifiListCard("已配對裝置紀錄", recordsList, out removeButton, "移除紀錄");
            mdnsCard.Margin = ScalePadding(new Padding(0, 0, 5, 0), scale);
            recordsCard.Margin = ScalePadding(new Padding(5, 0, 0, 0), scale);
            discovery.Controls.Add(mdnsCard, 0, 0);
            discovery.Controls.Add(recordsCard, 1, 0);
            root.Controls.Add(discovery, 0, 2);

            Panel noteCard = new Panel { Dock = DockStyle.Fill, BackColor = Card2, Padding = ScalePadding(new Padding(14, 10, 14, 10), scale), Margin = ScalePadding(new Padding(0, 10, 0, 0), scale) };
            CheckBox autoReconnect = new CheckBox
            {
                Text = "程式啟動時自動重新連線已記錄的 Wi-Fi 裝置",
                Checked = settings.WifiAutoReconnect,
                ForeColor = TextColor,
                AutoSize = true,
                Location = new Point(ScaleValue(10, scale), ScaleValue(8, scale))
            };
            Label requirements = new Label
            {
                Text = "相容性：配對碼功能需要 Android 11 以上及 Android Platform Tools 30.0.0 以上。Android 10 以下沒有配對碼介面，需先用 USB 授權，再執行 adb tcpip 5555 與無線連線。部分廠牌會隱藏此功能；企業、訪客 Wi-Fi 或防火牆也可能阻擋 mDNS 與裝置互連。",
                ForeColor = Muted,
                AutoSize = false,
                Dock = DockStyle.Bottom,
                Height = ScaleValue(74, scale)
            };
            noteCard.Controls.Add(autoReconnect);
            noteCard.Controls.Add(requirements);
            root.Controls.Add(noteCard, 0, 3);
            scroll.Controls.Add(root);
            page.Controls.Add(scroll);

            Action refreshRecords = delegate
            {
                recordsList.BeginUpdate();
                recordsList.Items.Clear();
                foreach (WifiDeviceRecord record in settings.WifiDevices) recordsList.Items.Add(record);
                recordsList.EndUpdate();
            };
            Action refreshConnectButton = delegate
            {
                string endpoint;
                if (TryBuildEndpoint(host.Text, Decimal.ToInt32(debugPort.Value), out endpoint) && IsWifiEndpointConnected(endpoint))
                    connectButton.Text = "中斷";
                else connectButton.Text = "連線";
            };
            refreshRecords();

            autoReconnect.CheckedChanged += delegate { settings.WifiAutoReconnect = autoReconnect.Checked; SaveSettings(); };
            pairButton.Click += async delegate
            {
                string endpoint;
                string code = pairingCode.Text.Trim();
                if (!TryBuildEndpoint(host.Text, Decimal.ToInt32(pairingPort.Value), out endpoint) || !Regex.IsMatch(code, "^[0-9]{6}$"))
                {
                    SetWifiActionState(actionState, "請確認手機 IP、配對 Port 與六位數配對碼。", false);
                    return;
                }
                pairButton.Enabled = false;
                SetWifiActionState(actionState, "正在配對，請保持手機配對碼畫面開啟…", null);
                AdbResult result = await RunAdbAsync("pair " + Quote(endpoint) + " " + code);
                string output = CleanOutput((result.Output ?? "") + " " + (result.Error ?? ""));
                bool ok = AdbCommandSucceeded(result) && output.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0;
                if (ok)
                {
                    UpsertWifiRecord(NormalizeWifiHost(host.Text), Decimal.ToInt32(pairingPort.Value), 0, null);
                    refreshRecords();
                    pairingCode.Clear();
                    Log("Wi-Fi 配對成功：" + endpoint);
                    SetWifiActionState(actionState, "配對成功；請再輸入無線偵錯主畫面的偵錯連線 Port。", true);
                }
                else
                {
                    Log("Wi-Fi 配對失敗：" + output);
                    SetWifiActionState(actionState, "配對失敗：" + output, false);
                }
                pairButton.Enabled = true;
            };
            connectButton.Click += async delegate
            {
                string endpoint;
                if (!TryBuildEndpoint(host.Text, Decimal.ToInt32(debugPort.Value), out endpoint))
                {
                    SetWifiActionState(actionState, "請輸入有效的手機 IP 與偵錯連線 Port。", false);
                    return;
                }
                connectButton.Enabled = false;
                bool disconnecting = IsWifiEndpointConnected(endpoint);
                SetWifiActionState(actionState, disconnecting ? "正在中斷 Wi-Fi 裝置…" : "正在連線 Wi-Fi 裝置…", null);
                AdbResult result = await RunAdbAsync((disconnecting ? "disconnect " : "connect ") + Quote(endpoint));
                string output = CleanOutput((result.Output ?? "") + " " + (result.Error ?? ""));
                bool ok = AdbCommandSucceeded(result) && output.IndexOf("failed", StringComparison.OrdinalIgnoreCase) < 0 &&
                    output.IndexOf("cannot", StringComparison.OrdinalIgnoreCase) < 0;
                if (ok && !disconnecting)
                {
                    UpsertWifiRecord(NormalizeWifiHost(host.Text), Decimal.ToInt32(pairingPort.Value), Decimal.ToInt32(debugPort.Value), null);
                    refreshRecords();
                }
                Log((disconnecting ? "Wi-Fi 中斷" : "Wi-Fi 連線") + (ok ? "成功：" : "失敗：") + endpoint + " / " + output);
                SetWifiActionState(actionState, (disconnecting ? "中斷" : "連線") + (ok ? "成功" : "失敗") + "：" + output, ok);
                await CheckConnectionAsync();
                refreshConnectButton();
                connectButton.Enabled = true;
            };
            scanButton.Click += async delegate
            {
                scanButton.Enabled = false;
                mdnsList.Items.Clear();
                SetWifiActionState(actionState, "正在透過 mDNS 搜尋區域網路裝置…", null);
                AdbResult result = await RunAdbAsync("mdns services");
                List<MdnsServiceInfo> found = ParseMdnsServices(result.Output);
                foreach (MdnsServiceInfo service in found) mdnsList.Items.Add(service);
                string detail = found.Count > 0 ? "找到 " + found.Count + " 個無線偵錯服務；雙擊即可帶入。" :
                    "找不到服務。請確認手機已開啟無線偵錯，且網路允許 mDNS。";
                SetWifiActionState(actionState, detail, found.Count > 0 ? (bool?)true : false);
                scanButton.Enabled = true;
            };
            mdnsList.DoubleClick += delegate
            {
                MdnsServiceInfo service = mdnsList.SelectedItem as MdnsServiceInfo;
                if (service == null) return;
                host.Text = service.Host;
                if (service.IsPairing) pairingPort.Value = service.Port;
                else debugPort.Value = service.Port;
                refreshConnectButton();
            };
            recordsList.DoubleClick += delegate
            {
                WifiDeviceRecord record = recordsList.SelectedItem as WifiDeviceRecord;
                if (record == null) return;
                host.Text = record.Host;
                if (record.PairingPort > 0) pairingPort.Value = record.PairingPort;
                if (record.DebugPort > 0) debugPort.Value = record.DebugPort;
                refreshConnectButton();
            };
            removeButton.Click += delegate
            {
                WifiDeviceRecord record = recordsList.SelectedItem as WifiDeviceRecord;
                if (record == null) return;
                settings.WifiDevices.Remove(record);
                SaveSettings();
                refreshRecords();
                SetWifiActionState(actionState, "已移除裝置紀錄；這不會撤銷手機端的配對授權。", true);
            };
            host.TextChanged += delegate { refreshConnectButton(); };
            debugPort.ValueChanged += delegate { refreshConnectButton(); };
            page.HandleCreated += async delegate { await CheckWifiCompatibilityAsync(compatibility); };
            return page;
        }

        private TextBox WifiInput(string hint)
        {
            float scale = Math.Max(1F, currentDpiScale);
            TextBox input = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Bg,
                ForeColor = TextColor,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = ScalePadding(new Padding(0, 2, 10, 2), scale),
                Tag = hint
            };
            input.HandleCreated += delegate { SetWindowTheme(input.Handle, "DarkMode_Explorer", null); };
            return input;
        }

        private NumericUpDown WifiPortInput()
        {
            float scale = Math.Max(1F, currentDpiScale);
            NumericUpDown input = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 65535,
                Value = 0,
                BackColor = Bg,
                ForeColor = TextColor,
                BorderStyle = BorderStyle.FixedSingle,
                ThousandsSeparator = false,
                Margin = ScalePadding(new Padding(0, 2, 10, 2), scale)
            };
            input.HandleCreated += delegate { SetWindowTheme(input.Handle, "DarkMode_Explorer", null); };
            return input;
        }

        private Label WifiFieldLabel(string text)
        {
            return new Label { Text = text, ForeColor = Muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, AutoEllipsis = true };
        }

        private ListBox WifiListBox()
        {
            ListBox list = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Card2,
                ForeColor = TextColor,
                BorderStyle = BorderStyle.None,
                IntegralHeight = false,
                HorizontalScrollbar = true
            };
            list.HandleCreated += delegate { SetWindowTheme(list.Handle, "DarkMode_Explorer", null); };
            return list;
        }

        private Panel WifiListCard(string title, ListBox list, out Button button, string buttonText)
        {
            float scale = Math.Max(1F, currentDpiScale);
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = Card2, Padding = ScalePadding(new Padding(12, 8, 12, 10), scale) };
            Label label = new Label { Text = title, Dock = DockStyle.Top, Height = ScaleValue(28, scale), ForeColor = TextColor, Font = new Font(Font, FontStyle.Bold) };
            button = NewButton(buttonText, false, 108);
            button.Dock = DockStyle.Right;
            Panel bottom = new Panel { Dock = DockStyle.Bottom, Height = ScaleValue(38, scale), BackColor = Card2 };
            bottom.Controls.Add(button);
            card.Controls.Add(list);
            card.Controls.Add(bottom);
            card.Controls.Add(label);
            list.BringToFront();
            return card;
        }

        private static string UsbConnectionHelpText()
        {
            return "USB 連線步驟\r\n\r\n" +
                "1. 手機進入「設定 > 關於手機」，連按 7 次版本號，開啟開發人員模式。\r\n\r\n" +
                "2. 到「設定 > 系統 > 開發人員選項」，開啟「USB 偵錯」。\r\n\r\n" +
                "3. 使用支援資料傳輸的 USB 線連接手機與電腦。\r\n\r\n" +
                "4. 解鎖手機，在「允許 USB 偵錯嗎？」視窗按下允許。建議勾選「一律允許這部電腦」。\r\n\r\n" +
                "5. 回到 Android ADB 快速工具，按下「重新檢查」。\r\n\r\n" +
                "若仍找不到手機：\r\n• 確認 USB 模式不是僅充電。\r\n• 更換支援資料傳輸的 USB 線或 USB 連接埠。\r\n" +
                "• 在開發人員選項撤銷 USB 偵錯授權，再重新連接。";
        }

        private async Task CheckWifiCompatibilityAsync(Label label)
        {
            string adb = FindAdb();
            if (String.IsNullOrWhiteSpace(adb))
            {
                label.Text = "ADB 相容性：找不到 adb.exe，請先在主畫面指定 Android Platform Tools。";
                label.ForeColor = Red;
                return;
            }
            AdbResult version = await RunAdbAsync("version");
            string versionText = CleanOutput((version.Output ?? "") + " " + (version.Error ?? ""));
            Match match = Regex.Match(versionText, @"Version\s+(\d+)\.(\d+)\.(\d+)", RegexOptions.IgnoreCase);
            int major = match.Success ? SafeInt(match.Groups[1].Value) : -1;
            AdbResult mdns = await RunAdbAsync("mdns services");
            bool pairCompatible = AdbCommandSucceeded(version) && major >= 30;
            bool mdnsCompatible = AdbCommandSucceeded(mdns) &&
                ((mdns.Error ?? "").IndexOf("unknown command", StringComparison.OrdinalIgnoreCase) < 0);
            string detected = match.Success ? match.Value.Replace("Version", "Platform Tools") : "ADB 版本無法辨識";
            label.Text = "ADB 相容性：" + detected + "　｜　配對碼：" + (pairCompatible ? "支援" : "需要 Platform Tools 30.0.0+") +
                "　｜　mDNS 搜尋：" + (mdnsCompatible ? "可用" : "不可用或被網路阻擋") +
                "\r\n手機需求：Android 11+ 才有系統內建六位數無線偵錯配對；Android 10 以下請使用 USB 授權＋TCP/IP 模式。";
            label.ForeColor = pairCompatible ? Green : Color.FromArgb(255, 190, 75);
        }

        private static int SafeInt(string value)
        {
            int result;
            return Int32.TryParse(value, out result) ? result : 0;
        }

        private void SetWifiActionState(Label label, string text, bool? success)
        {
            label.Text = text;
            label.ForeColor = !success.HasValue ? Muted : success.Value ? Green : Red;
        }

        public static string FormatNetworkEndpoint(string host, int port)
        {
            host = NormalizeWifiHost(host);
            if (host.IndexOf(':') >= 0 && !host.StartsWith("[", StringComparison.Ordinal)) host = "[" + host + "]";
            return host + ":" + port;
        }

        private static string NormalizeWifiHost(string host)
        {
            host = (host ?? "").Trim();
            if (host.StartsWith("[", StringComparison.Ordinal) && host.EndsWith("]", StringComparison.Ordinal) && host.Length > 2)
                host = host.Substring(1, host.Length - 2);
            return host;
        }

        private static bool TryBuildEndpoint(string hostText, int port, out string endpoint)
        {
            endpoint = "";
            string host = NormalizeWifiHost(hostText);
            if (port < 1 || port > 65535 || host.Length == 0 || host.Length > 255 ||
                !Regex.IsMatch(host, "^[A-Za-z0-9.:%_-]+$")) return false;
            endpoint = FormatNetworkEndpoint(host, port);
            return true;
        }

        private bool IsWifiEndpointConnected(string endpoint)
        {
            return ReadyDevices().Any(delegate(DeviceInfo device)
            {
                return String.Equals(device.Serial, endpoint, StringComparison.OrdinalIgnoreCase);
            });
        }

        private WifiDeviceRecord UpsertWifiRecord(string host, int pairingPort, int debugPort, string displayName)
        {
            host = NormalizeWifiHost(host);
            if (settings.WifiDevices == null) settings.WifiDevices = new List<WifiDeviceRecord>();
            WifiDeviceRecord record = settings.WifiDevices.FirstOrDefault(delegate(WifiDeviceRecord item)
            {
                return String.Equals(NormalizeWifiHost(item.Host), host, StringComparison.OrdinalIgnoreCase);
            });
            if (record == null)
            {
                record = new WifiDeviceRecord { Host = host };
                settings.WifiDevices.Add(record);
            }
            if (pairingPort > 0) record.PairingPort = pairingPort;
            if (debugPort > 0)
            {
                record.DebugPort = debugPort;
                record.LastConnected = DateTime.Now;
            }
            if (!String.IsNullOrWhiteSpace(displayName)) record.DisplayName = displayName;
            SaveSettings();
            return record;
        }

        private List<MdnsServiceInfo> ParseMdnsServices(string output)
        {
            List<MdnsServiceInfo> found = new List<MdnsServiceInfo>();
            foreach (string raw in (output ?? "").Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("List of", StringComparison.OrdinalIgnoreCase)) continue;
                string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                string endpoint = parts[parts.Length - 1];
                string host;
                int port;
                if (!TryParseNetworkEndpoint(endpoint, out host, out port)) continue;
                string serviceType = parts.FirstOrDefault(delegate(string value) { return value.IndexOf("_adb-tls-", StringComparison.OrdinalIgnoreCase) >= 0; }) ?? "";
                if (serviceType.Length == 0) continue;
                found.Add(new MdnsServiceInfo { Name = parts[0], ServiceType = serviceType, Host = host, Port = port });
            }
            return found.GroupBy(delegate(MdnsServiceInfo item) { return item.ServiceType + "|" + item.Host + "|" + item.Port; }, StringComparer.OrdinalIgnoreCase)
                .Select(delegate(IGrouping<string, MdnsServiceInfo> group) { return group.First(); }).ToList();
        }

        private static bool TryParseNetworkEndpoint(string endpoint, out string host, out int port)
        {
            host = "";
            port = 0;
            endpoint = (endpoint ?? "").Trim();
            int separator;
            if (endpoint.StartsWith("[", StringComparison.Ordinal))
            {
                int close = endpoint.IndexOf(']');
                if (close < 1 || close + 2 >= endpoint.Length || endpoint[close + 1] != ':') return false;
                host = endpoint.Substring(1, close - 1);
                return Int32.TryParse(endpoint.Substring(close + 2), out port) && port > 0 && port <= 65535;
            }
            separator = endpoint.LastIndexOf(':');
            if (separator <= 0 || separator == endpoint.Length - 1) return false;
            host = endpoint.Substring(0, separator);
            return Int32.TryParse(endpoint.Substring(separator + 1), out port) && port > 0 && port <= 65535;
        }

        private async Task AutoReconnectWifiDevicesAsync(bool showResult)
        {
            if (!settings.WifiAutoReconnect || settings.WifiDevices == null || settings.WifiDevices.Count == 0) return;
            List<string> failed = new List<string>();
            int connected = 0;
            foreach (WifiDeviceRecord record in settings.WifiDevices.Where(delegate(WifiDeviceRecord item) { return item.DebugPort > 0; }).ToList())
            {
                string endpoint = record.DebugEndpoint;
                AdbResult result = await RunAdbAsync("connect " + Quote(endpoint));
                string detail = CleanOutput((result.Output ?? "") + " " + (result.Error ?? ""));
                bool ok = AdbCommandSucceeded(result) && detail.IndexOf("failed", StringComparison.OrdinalIgnoreCase) < 0 &&
                    detail.IndexOf("cannot", StringComparison.OrdinalIgnoreCase) < 0;
                if (ok)
                {
                    record.LastConnected = DateTime.Now;
                    connected++;
                    Log("自動重新連線成功：" + endpoint);
                }
                else
                {
                    failed.Add(endpoint);
                    Log("自動重新連線失敗：" + endpoint + " / " + detail);
                }
            }
            SaveSettings();
            if (showResult)
                MessageBox.Show(this, "自動重新連線完成：成功 " + connected + "，失敗 " + failed.Count,
                    "Wi-Fi 自動連線", MessageBoxButtons.OK, failed.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void ShowConnectionHelpLegacy(object sender, EventArgs e)
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
            lastApkTooltipIndex = -1;
            if (apkListToolTip != null) apkListToolTip.Hide(apkList);
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
                ? group.Apks.Count + " 個 APK　｜　來源：APKs\\" + group.Name + "　｜　資料夾同步組合不可改名"
                : group.Apks.Count + " 個 APK　｜　可拖放 APK 到右側清單　｜　雙擊左側組合可編輯名稱";
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
            quickInstallDragOver = e.Effect == DragDropEffects.Copy;
            if (dropPanel != null) dropPanel.Invalidate();
        }

        private void ApkDragLeave(object sender, EventArgs e)
        {
            quickInstallDragOver = false;
            if (dropPanel != null) dropPanel.Invalidate();
        }

        private async void ApkDragDrop(object sender, DragEventArgs e)
        {
            quickInstallDragOver = false;
            if (dropPanel != null) dropPanel.Invalidate();
            if (busy || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] files = ((string[])e.Data.GetData(DataFormats.FileDrop))
                .Where(delegate(string f) { return File.Exists(f) && String.Equals(Path.GetExtension(f), ".apk", StringComparison.OrdinalIgnoreCase); }).ToArray();
            await InstallQuickFilesAsync(files);
        }

        private void QuickTransferDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            if (!busy && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effect = paths.Any(delegate(string path) { return File.Exists(path) || Directory.Exists(path); })
                    ? DragDropEffects.Copy : DragDropEffects.None;
            }
            quickTransferDragOver = e.Effect == DragDropEffects.Copy;
            if (transferDropPanel != null) transferDropPanel.Invalidate();
        }

        private void QuickTransferDragLeave(object sender, EventArgs e)
        {
            quickTransferDragOver = false;
            if (transferDropPanel != null) transferDropPanel.Invalidate();
        }

        private async void QuickTransferDragDrop(object sender, DragEventArgs e)
        {
            quickTransferDragOver = false;
            if (transferDropPanel != null) transferDropPanel.Invalidate();
            if (busy || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] paths = ((string[])e.Data.GetData(DataFormats.FileDrop))
                .Where(delegate(string path) { return File.Exists(path) || Directory.Exists(path); })
                .ToArray();
            await TransferQuickItemsAsync(paths);
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

        private async Task TransferQuickItemsAsync(IEnumerable<string> paths)
        {
            if (busy) return;
            HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths ?? Enumerable.Empty<string>())
            {
                try
                {
                    string fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath) || Directory.Exists(fullPath)) unique.Add(fullPath);
                }
                catch (Exception ex)
                {
                    Log("略過無效的傳輸路徑：" + path + " / " + ex.Message);
                }
            }
            if (unique.Count == 0) return;
            if (!await EnsureReadyDeviceAsync()) return;
            DeviceInfo device = ReadyDevice();
            if (device == null) return;

            string remoteDestination = QuickTransferRemoteDestination();
            string destinationLabel = QuickTransferDestinationLabel();
            busy = true;
            quickTransferring = true;
            quickTransferStatus = "正在準備手機 " + destinationLabel + "...";
            SetInstallButtons(false);
            if (transferDropPanel != null) transferDropPanel.Invalidate();
            int success = 0;
            int failed = 0;
            try
            {
                Log("開始快速傳輸，共 " + unique.Count + " 個檔案或資料夾，目標：" + remoteDestination);
                AdbResult prepare = await RunAdbAsync("-s " + Quote(device.Serial) +
                    " shell mkdir -p " + Quote(remoteDestination));
                if (!AdbCommandSucceeded(prepare))
                {
                    string detail = CleanOutput((prepare.Output ?? "") + " " + (prepare.Error ?? ""));
                    Log("無法建立或存取手機目的地 " + remoteDestination + "：" + detail);
                    MessageBox.Show(this, "無法存取手機目的地 " + destinationLabel + "。\n\n" + detail,
                        "快速傳輸失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int index = 0;
                foreach (string path in unique)
                {
                    index++;
                    string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar));
                    if (String.IsNullOrWhiteSpace(name)) name = path;
                    quickTransferStatus = "正在傳輸 " + index + " / " + unique.Count + "：" + name;
                    if (transferDropPanel != null) transferDropPanel.Invalidate();
                    Log("傳輸到手機 " + remoteDestination + "：" + path);
                    AdbResult result = await RunAdbAsync("-s " + Quote(device.Serial) +
                        " push " + Quote(path) + " " + Quote(remoteDestination));
                    string detail = CleanOutput((result.Output ?? "") + " " + (result.Error ?? ""));
                    if (result.Started && result.ExitCode == 0)
                    {
                        success++;
                        Log("傳輸成功：" + name + (String.IsNullOrWhiteSpace(detail) ? "" : " / " + detail));
                    }
                    else
                    {
                        failed++;
                        Log("傳輸失敗：" + name + " / " + detail);
                    }
                }

                Log("快速傳輸完成：成功 " + success + "，失敗 " + failed + "。手機位置：" + remoteDestination);
                MessageBox.Show(this,
                    "快速傳輸完成\n\n成功：" + success + "\n失敗：" + failed +
                    "\n\n手機位置：" + destinationLabel +
                    (failed > 0 ? "\n\n可到「執行紀錄」查看失敗原因。" : ""),
                    "快速傳輸", MessageBoxButtons.OK,
                    failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            finally
            {
                busy = false;
                quickTransferring = false;
                quickTransferStatus = "";
                SetInstallButtons(true);
                if (transferDropPanel != null) transferDropPanel.Invalidate();
            }
        }

        private string QuickTransferRemoteDestination()
        {
            int selected = quickTransferDestinationComboBox == null ? 0 : quickTransferDestinationComboBox.SelectedIndex;
            if (selected == 1) return "/sdcard/DCIM/";
            if (selected == 2) return "/sdcard/Pictures/";
            if (selected == 3) return "/sdcard/";
            return "/sdcard/Download/";
        }

        private string QuickTransferDestinationLabel()
        {
            int selected = quickTransferDestinationComboBox == null ? 0 : quickTransferDestinationComboBox.SelectedIndex;
            if (selected == 1) return "DCIM\\";
            if (selected == 2) return "Pictures\\";
            if (selected == 3) return "\\（內部儲存根目錄）";
            return "Download\\";
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

        private List<DeviceInfo> ReadyDevices()
        {
            return devices.Where(delegate(DeviceInfo device) { return device.State == "device"; }).ToList();
        }

        private DeviceInfo ReadyDevice()
        {
            List<DeviceInfo> ready = ReadyDevices();
            if (ready.Count == 0) return null;
            DeviceInfo selected = deviceSelector == null ? null : deviceSelector.SelectedItem as DeviceInfo;
            if (selected != null)
            {
                DeviceInfo match = ready.FirstOrDefault(delegate(DeviceInfo device)
                {
                    return String.Equals(device.Serial, selected.Serial, StringComparison.OrdinalIgnoreCase);
                });
                if (match != null) return match;
            }
            if (!String.IsNullOrWhiteSpace(settings.SelectedDeviceSerial))
            {
                DeviceInfo remembered = ready.FirstOrDefault(delegate(DeviceInfo device)
                {
                    return String.Equals(device.Serial, settings.SelectedDeviceSerial, StringComparison.OrdinalIgnoreCase);
                });
                if (remembered != null) return remembered;
            }
            return ready[0];
        }

        private List<DeviceInfo> SelectedInstallDevices()
        {
            List<DeviceInfo> ready = ReadyDevices();
            bool installAll = ready.Count > 1 && installAllDevicesCheck != null && installAllDevicesCheck.Checked;
            if (installAll) return ready;
            DeviceInfo primary = ReadyDevice();
            return primary == null ? new List<DeviceInfo>() : new List<DeviceInfo> { primary };
        }

        private async Task<bool> EnsureReadyDeviceAsync()
        {
            if (ReadyDevice() != null) return true;
            await CheckConnectionAsync();
            if (ReadyDevice() != null) return true;
            MessageBox.Show(this, "目前沒有可操作的 Android 手機。\n請先完成 USB 偵錯授權並重新檢查。", "手機未連線", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        private void BrowseSpotread(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "選擇 ArgyllCMS spotread.exe";
                dialog.Filter = "ArgyllCMS spotread (spotread.exe)|spotread.exe|執行檔 (*.exe)|*.exe";
                string current = FindSpotread();
                if (!String.IsNullOrWhiteSpace(current)) dialog.InitialDirectory = Path.GetDirectoryName(current);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                settings.SpotreadPath = dialog.FileName;
                spotreadPathTextBox.Text = dialog.FileName;
                SaveSettings();
                autoBrightnessStatusLabel.Text = "已指定 spotread.exe；請連接色度計並按「試量測」。";
                autoBrightnessStatusLabel.ForeColor = Muted;
            }
        }

        private void BrowseSpotreadCorrection(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "選擇 ArgyllCMS 顯示器校正檔";
                dialog.Filter = "Argyll 校正檔 (*.ccss;*.ccmx)|*.ccss;*.ccmx|所有檔案 (*.*)|*.*";
                if (!String.IsNullOrWhiteSpace(settings.SpotreadCorrectionPath) && File.Exists(settings.SpotreadCorrectionPath))
                    dialog.InitialDirectory = Path.GetDirectoryName(settings.SpotreadCorrectionPath);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                settings.SpotreadCorrectionPath = dialog.FileName;
                spotreadCorrectionTextBox.Text = dialog.FileName;
                SaveSettings();
            }
        }

        private string FindSpotread()
        {
            List<string> candidates = new List<string>();
            if (spotreadPathTextBox != null && !String.IsNullOrWhiteSpace(spotreadPathTextBox.Text)) candidates.Add(spotreadPathTextBox.Text.Trim());
            if (!String.IsNullOrWhiteSpace(settings.SpotreadPath)) candidates.Add(settings.SpotreadPath);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            candidates.Add(Path.Combine(baseDir, "spotread.exe"));
            candidates.Add(Path.Combine(baseDir, "ArgyllCMS", "bin", "spotread.exe"));
            candidates.Add(Path.Combine(baseDir, "Argyll", "bin", "spotread.exe"));
            foreach (string candidate in candidates)
            {
                try { if (!String.IsNullOrWhiteSpace(candidate) && File.Exists(candidate)) return Path.GetFullPath(candidate); }
                catch { }
            }
            foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try
                {
                    string candidate = Path.Combine(folder.Trim(), "spotread.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
            return "";
        }

        private string SpotreadArguments()
        {
            string arguments = "-e -O";
            string correction = spotreadCorrectionTextBox == null ? settings.SpotreadCorrectionPath : spotreadCorrectionTextBox.Text.Trim();
            if (!String.IsNullOrWhiteSpace(correction)) arguments += " -X " + Quote(correction);
            return arguments;
        }

        private async Task<AdbResult> RunSpotreadAsync()
        {
            string executable = FindSpotread();
            if (String.IsNullOrWhiteSpace(executable))
                return new AdbResult { Started = false, ExitCode = -1, Error = "找不到 spotread.exe，請先指定 ArgyllCMS bin 資料夾中的檔案。" };
            string correction = spotreadCorrectionTextBox == null ? settings.SpotreadCorrectionPath : spotreadCorrectionTextBox.Text.Trim();
            if (!String.IsNullOrWhiteSpace(correction) && !File.Exists(correction))
                return new AdbResult { Started = false, ExitCode = -1, Error = "指定的 CCSS／CCMX 校正檔不存在。" };
            string arguments = SpotreadArguments();
            return await Task.Run(delegate
            {
                AdbResult result = new AdbResult();
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = executable,
                        Arguments = arguments,
                        WorkingDirectory = Path.GetDirectoryName(executable),
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
                        StringBuilder standardOutput = new StringBuilder();
                        StringBuilder standardError = new StringBuilder();
                        object outputLock = new object();
                        process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                        {
                            if (e.Data == null) return;
                            lock (outputLock) standardOutput.AppendLine(e.Data);
                        };
                        process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                        {
                            if (e.Data == null) return;
                            lock (outputLock) standardError.AppendLine(e.Data);
                        };
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        bool exited = false;
                        bool fatalInstrumentState = false;
                        DateTime deadline = DateTime.UtcNow.AddSeconds(60);
                        while (!(exited = process.WaitForExit(150)) && DateTime.UtcNow < deadline)
                        {
                            string liveOutput;
                            lock (outputLock) liveOutput = standardOutput.ToString() + "\n" + standardError.ToString();
                            if (liveOutput.IndexOf("sensor being in the wrong position", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                liveOutput.IndexOf("Ambient filter should be removed", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                fatalInstrumentState = true;
                                try { process.Kill(); } catch { }
                                process.WaitForExit(5000);
                                exited = true;
                                break;
                            }
                        }
                        if (!exited)
                        {
                            try { process.Kill(); } catch { }
                            process.WaitForExit(5000);
                            result.ExitCode = -2;
                            result.Error = "spotread 量測逾時（60 秒）。請確認儀器已連接、感測器放在螢幕上，且沒有其他程式占用儀器。";
                        }
                        else
                        {
                            process.WaitForExit();
                            result.ExitCode = fatalInstrumentState ? -3 : process.ExitCode;
                        }
                        lock (outputLock)
                        {
                            result.Output = standardOutput.ToString();
                            string stderr = standardError.ToString();
                            if (!String.IsNullOrWhiteSpace(stderr))
                                result.Error = String.IsNullOrWhiteSpace(result.Error) ? stderr : result.Error + " " + stderr;
                        }
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

        private async Task<Tuple<bool, double, string>> MeasureDisplayNitAsync()
        {
            AdbResult result = await RunSpotreadAsync();
            string combined = ((result.Output ?? "") + "\n" + (result.Error ?? "")).Trim();
            double nit = 0;
            bool parsed = result.Started && result.ExitCode == 0 && TryParseSpotreadNit(combined, out nit);
            if (parsed && nit >= 0) return Tuple.Create(true, nit, CleanOutput(combined));
            string detail = CleanOutput(combined);
            if (String.IsNullOrWhiteSpace(detail)) detail = "spotread 沒有回傳可辨識的 XYZ／Yxy 亮度結果。";
            return Tuple.Create(false, 0.0, ExplainSpotreadFailure(detail));
        }

        private static string ExplainSpotreadFailure(string detail)
        {
            string text = detail ?? "";
            if (text.IndexOf("sensor being in the wrong position", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Ambient filter should be removed", StringComparison.OrdinalIgnoreCase) >= 0)
                return "色度計目前位於環境光量測位置。請將白色環境光擴散蓋完全旋離感測鏡頭，確認鏡頭面貼平螢幕後再試。原始訊息：" + text;
            if (text.IndexOf("err 32", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("being used by another process", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("sharing violation", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string blockers = DetectKnownSpotreadBlockers();
                return "Windows 拒絕 spotread 開啟色度計（錯誤 32）。這不一定是校色軟體；Logitech／Alienware 等 RGB 動態燈光服務也可能獨占 HID 裝置。" +
                    (String.IsNullOrWhiteSpace(blockers) ? "" : " 目前偵測到可能的占用者：" + blockers + "。") +
                    "請先停止列出的常駐服務，再拔插色度計後重試。原始訊息：" + text;
            }
            if (text.IndexOf("Failed to initialise communications", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Communications failure", StringComparison.OrdinalIgnoreCase) >= 0)
                return "spotread 找到色度計，但無法建立通訊。請關閉其他校色軟體、拔插 USB，並確認 ArgyllCMS 驅動與儀器相容。原始訊息：" + text;
            if (text.IndexOf("No instrument", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("instrument access failed", StringComparison.OrdinalIgnoreCase) >= 0)
                return "spotread 找不到可用的色度計。請確認 USB 連線、驅動，以及儀器未被其他程式占用。原始訊息：" + text;
            return text;
        }

        private static string DetectKnownSpotreadBlockers()
        {
            string[,] known =
            {
                { "logi_lamparray_service", "Logitech LampArray Service" },
                { "AlienFXSubAgent", "Alienware AlienFX" },
                { "AWCCService", "Alienware Command Center" },
                { "LightingService", "ASUS Aura Lighting Service" },
                { "ArmouryCrate.Service", "ASUS Armoury Crate" },
                { "iCUE", "Corsair iCUE" },
                { "Razer Synapse Service", "Razer Synapse" },
                { "SignalRgb", "SignalRGB" },
                { "OpenRGB", "OpenRGB" }
            };
            List<string> running = new List<string>();
            for (int i = 0; i < known.GetLength(0); i++)
            {
                try
                {
                    Process[] processes = Process.GetProcessesByName(known[i, 0]);
                    if (processes.Length > 0) running.Add(known[i, 1] + "（" + known[i, 0] + ".exe）");
                    foreach (Process process in processes) process.Dispose();
                }
                catch { }
            }
            return String.Join("、", running.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static bool TryParseSpotreadNit(string output, out double nit)
        {
            nit = 0;
            string number = @"([+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[Ee][+-]?\d+)?)";
            Match match = Regex.Match(output ?? "", @"(?:Result\s+is\s+)?XYZ\s*:\s*" + number + @"[\s,]+" + number + @"[\s,]+" + number, RegexOptions.IgnoreCase);
            if (match.Success && TryParseInvariantDouble(match.Groups[2].Value, out nit)) return true;
            match = Regex.Match(output ?? "", @"Yxy\s*:\s*" + number, RegexOptions.IgnoreCase);
            if (match.Success && TryParseInvariantDouble(match.Groups[1].Value, out nit)) return true;
            match = Regex.Match(output ?? "", @"(?:^|\s)Y\s*=\s*" + number, RegexOptions.IgnoreCase);
            if (match.Success && TryParseInvariantDouble(match.Groups[1].Value, out nit)) return true;
            match = Regex.Match(output ?? "", number + @"\s*(?:cd\s*/\s*m(?:\^?2|²)|nit(?:s)?)", RegexOptions.IgnoreCase);
            return match.Success && TryParseInvariantDouble(match.Groups[1].Value, out nit);
        }

        private static bool TryParseInvariantDouble(string text, out double value)
        {
            return Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                Double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private async Task TestBrightnessMeterAsync()
        {
            if (autoBrightnessRunning)
            {
                autoBrightnessStatusLabel.Text = "全自動調整正在執行，無法同時試量測。";
                autoBrightnessStatusLabel.ForeColor = Color.FromArgb(255, 190, 75);
                return;
            }
            if (busy)
            {
                autoBrightnessStatusLabel.Text = "程式正在執行其他操作，請等操作完成後再試量測。";
                autoBrightnessStatusLabel.ForeColor = Color.FromArgb(255, 190, 75);
                return;
            }
            string spotread = FindSpotread();
            if (String.IsNullOrWhiteSpace(spotread))
            {
                autoBrightnessStatusLabel.Text = "找不到 spotread.exe，請先按「選擇 spotread.exe」。";
                autoBrightnessStatusLabel.ForeColor = Red;
                return;
            }
            settings.SpotreadPath = spotread;
            spotreadPathTextBox.Text = spotread;
            SaveSettings();
            testMeterButton.Enabled = false;
            testMeterButton.Text = "量測中…";
            autoBrightnessStatusLabel.Text = "正在呼叫 spotread 試量測；請將感測器貼在發光畫面上…";
            autoBrightnessStatusLabel.ForeColor = Color.FromArgb(255, 190, 75);
            autoBrightnessProgressBar.Style = ProgressBarStyle.Marquee;
            autoBrightnessProgressBar.MarqueeAnimationSpeed = 24;
            try
            {
                Tuple<bool, double, string> measured = await MeasureDisplayNitAsync();
                if (measured.Item1)
                {
                    autoBrightnessReadingLabel.Text = "儀器實測：" + measured.Item2.ToString("0.0", CultureInfo.CurrentCulture) + " nit";
                    autoBrightnessReadingLabel.ForeColor = Green;
                    autoBrightnessStatusLabel.Text = "spotread 已成功辨識並讀取儀器，可開始全自動調整。";
                    autoBrightnessStatusLabel.ForeColor = Green;
                    Log("色度計試量測成功：" + measured.Item2.ToString("0.000", CultureInfo.InvariantCulture) + " nit");
                }
                else
                {
                    autoBrightnessReadingLabel.Text = "試量測失敗";
                    autoBrightnessReadingLabel.ForeColor = Red;
                    autoBrightnessStatusLabel.Text = "無法取得亮度：" + ShortStatus(measured.Item3, 260);
                    autoBrightnessStatusLabel.ForeColor = Red;
                    Log("色度計試量測失敗：" + measured.Item3);
                    MessageBox.Show(this, measured.Item3, "色度計試量測失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                autoBrightnessReadingLabel.Text = "試量測失敗";
                autoBrightnessReadingLabel.ForeColor = Red;
                autoBrightnessStatusLabel.Text = "試量測發生錯誤：" + ShortStatus(ex.Message, 260);
                autoBrightnessStatusLabel.ForeColor = Red;
                Log("色度計試量測發生錯誤：" + ex.Message);
                MessageBox.Show(this, ex.Message, "色度計試量測錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                autoBrightnessProgressBar.Style = ProgressBarStyle.Continuous;
                autoBrightnessProgressBar.MarqueeAnimationSpeed = 0;
                autoBrightnessProgressBar.Value = 0;
                testMeterButton.Text = "試量測";
                testMeterButton.Enabled = true;
            }
        }

        private async Task OpenWhitePatternOnPhoneAsync()
        {
            if (busy || autoBrightnessRunning) return;
            if (!await EnsureReadyDeviceAsync()) return;
            DeviceInfo device = ReadyDevice();
            if (device == null) return;
            bool ok = await OpenWhitePatternOnPhoneAsync(device, true);
            if (ok)
            {
                autoBrightnessStatusLabel.Text = "白色測試圖已送到手機；請切換成全螢幕並將感測器貼在中央白色區域。";
                autoBrightnessStatusLabel.ForeColor = Green;
            }
        }

        private async Task<bool> OpenWhitePatternOnPhoneAsync(DeviceInfo device, bool showFailure)
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "AndroidADBTools-white-pattern.png");
            const string remoteFile = "/sdcard/Download/AndroidADBTools-white-pattern.png";
            try
            {
                using (Bitmap bitmap = new Bitmap(1080, 1920))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White);
                    bitmap.Save(tempFile, System.Drawing.Imaging.ImageFormat.Png);
                }
                string serial = "-s " + Quote(device.Serial) + " ";
                AdbResult push = await RunAdbAsync(serial + "push " + Quote(tempFile) + " " + Quote(remoteFile));
                if (!AdbCommandSucceeded(push)) throw new InvalidOperationException(CleanOutput((push.Output ?? "") + " " + (push.Error ?? "")));
                AdbResult open = await RunAdbAsync(serial + "shell am start -a android.intent.action.VIEW -t image/png -d " + Quote("file://" + remoteFile));
                if (!AdbCommandSucceeded(open)) throw new InvalidOperationException(CleanOutput((open.Output ?? "") + " " + (open.Error ?? "")));
                Log("已在手機開啟全白亮度量測圖。某些圖片檢視器仍需手動切換全螢幕。");
                return true;
            }
            catch (Exception ex)
            {
                Log("無法開啟白色測試圖：" + ex.Message);
                if (showFailure) MessageBox.Show(this, "無法自動在手機開啟白色測試圖。\n\n請手動在手機顯示全白畫面後再量測。\n\n" + ex.Message,
                    "白色測試圖", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        private async Task RunAutomaticBrightnessAsync()
        {
            if (autoBrightnessRunning || busy) return;
            string spotread = FindSpotread();
            if (String.IsNullOrWhiteSpace(spotread))
            {
                MessageBox.Show(this, "請先指定 ArgyllCMS bin 資料夾中的 spotread.exe。", "缺少 ArgyllCMS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!await EnsureReadyDeviceAsync()) return;
            DeviceInfo device = ReadyDevice();
            if (device == null) return;
            string prefix = "-s " + Quote(device.Serial) + " shell ";
            AdbResult currentResult = await RunAdbAsync(prefix + "settings get system screen_brightness");
            int current;
            if (!AdbCommandSucceeded(currentResult) || !Int32.TryParse(FirstOutputLine(currentResult.Output), out current))
            {
                MessageBox.Show(this, "無法讀取手機目前亮度，不能開始自動調整。", "亮度讀取失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Tuple<int, bool, string> maximumInfo = await DetectBrightnessMaximumAsync(prefix, current);
            SetBrightnessMaximum(maximumInfo.Item1);
            double target = (double)autoBrightnessTargetNumber.Value;
            double tolerance = (double)autoBrightnessToleranceNumber.Value;
            settings.SpotreadPath = spotread;
            settings.AutoBrightnessTargetNit = autoBrightnessTargetNumber.Value;
            settings.AutoBrightnessToleranceNit = autoBrightnessToleranceNumber.Value;
            SaveSettings();

            autoBrightnessRunning = true;
            autoBrightnessCancelRequested = false;
            busy = true;
            brightnessApplying = true;
            brightnessUpdateTimer.Stop();
            SetInstallButtons(false);
            SetAutoBrightnessUi(false);
            startAutoBrightnessButton.Enabled = true;
            startAutoBrightnessButton.Text = "取消自動調整";
            autoBrightnessProgressBar.Value = 0;
            autoBrightnessStatusLabel.Text = "正在準備白色測試畫面與手動亮度模式…";
            autoBrightnessStatusLabel.ForeColor = Color.FromArgb(255, 190, 75);
            bool converged = false;
            bool measurementFailed = false;
            int bestValue = Math.Max(1, Math.Min(brightnessDetectedMaximum, current));
            double bestNit = Double.NaN;
            double bestError = Double.MaxValue;
            int iteration = 0;
            try
            {
                await OpenWhitePatternOnPhoneAsync(device, false);
                AdbResult mode = await RunAdbAsync(prefix + "settings put system screen_brightness_mode 0");
                if (!AdbCommandSucceeded(mode)) throw new InvalidOperationException("無法關閉手機自動亮度：" + CleanOutput((mode.Output ?? "") + " " + (mode.Error ?? "")));
                brightnessAutoMode = false;
                await Task.Delay(1800);

                int low = 1;
                int high = brightnessDetectedMaximum;
                int candidate = bestValue;
                for (iteration = 1; iteration <= 18 && low <= high; iteration++)
                {
                    if (autoBrightnessCancelRequested) break;
                    autoBrightnessProgressBar.Value = Math.Min(iteration, autoBrightnessProgressBar.Maximum);
                    autoBrightnessStatusLabel.Text = "第 " + iteration + " / 18 次：套用 Android 亮度 " + candidate + "，等待畫面穩定…";
                    AdbResult applied = await RunAdbAsync(prefix + "settings put system screen_brightness " + candidate);
                    if (!AdbCommandSucceeded(applied)) throw new InvalidOperationException("套用亮度 " + candidate + " 失敗：" + CleanOutput((applied.Output ?? "") + " " + (applied.Error ?? "")));
                    SetBrightnessControls(candidate, false);
                    brightnessLastApplied = candidate;
                    await Task.Delay(1400);
                    if (autoBrightnessCancelRequested) break;
                    autoBrightnessStatusLabel.Text = "第 " + iteration + " / 18 次：色度計量測中…";
                    Tuple<bool, double, string> measured = await MeasureDisplayNitAsync();
                    if (!measured.Item1)
                    {
                        measurementFailed = true;
                        throw new InvalidOperationException("spotread 無法取得亮度：" + measured.Item3);
                    }
                    double actual = measured.Item2;
                    double error = Math.Abs(actual - target);
                    autoBrightnessReadingLabel.Text = "第 " + iteration + " 次實測：" + actual.ToString("0.0", CultureInfo.CurrentCulture) +
                        " nit　｜　目標 " + target.ToString("0.0", CultureInfo.CurrentCulture) + " nit　｜　誤差 " + error.ToString("0.0", CultureInfo.CurrentCulture);
                    autoBrightnessReadingLabel.ForeColor = error <= tolerance ? Green : Color.FromArgb(255, 190, 75);
                    Log("自動亮度量測：Android=" + candidate + "，實測=" + actual.ToString("0.000", CultureInfo.InvariantCulture) +
                        " nit，目標=" + target.ToString("0.000", CultureInfo.InvariantCulture) + " nit");
                    if (error < bestError)
                    {
                        bestError = error;
                        bestNit = actual;
                        bestValue = candidate;
                    }
                    if (error <= tolerance)
                    {
                        converged = true;
                        break;
                    }
                    if (actual < target) low = candidate + 1;
                    else high = candidate - 1;
                    if (low > high) break;
                    candidate = low + (high - low) / 2;
                }

                if (!Double.IsNaN(bestNit))
                {
                    AdbResult finalApply = await RunAdbAsync(prefix + "settings put system screen_brightness " + bestValue);
                    if (!AdbCommandSucceeded(finalApply)) throw new InvalidOperationException("無法套用最佳亮度值 " + bestValue + "。 ");
                    SetBrightnessControls(bestValue, false);
                    brightnessLastApplied = bestValue;
                }
                if (autoBrightnessCancelRequested)
                {
                    autoBrightnessStatusLabel.Text = Double.IsNaN(bestNit) ? "已取消，未取得有效量測。" :
                        "已取消；保留目前最佳結果 " + bestNit.ToString("0.0", CultureInfo.CurrentCulture) + " nit（Android 亮度 " + bestValue + "）。";
                    autoBrightnessStatusLabel.ForeColor = Color.FromArgb(255, 190, 75);
                    Log("使用者取消全自動亮度調整。");
                }
                else if (converged)
                {
                    autoBrightnessStatusLabel.Text = "校準完成：" + bestNit.ToString("0.0", CultureInfo.CurrentCulture) + " nit，Android 亮度 " + bestValue +
                        "，誤差 " + bestError.ToString("0.0", CultureInfo.CurrentCulture) + " nit。";
                    autoBrightnessStatusLabel.ForeColor = Green;
                    brightnessStatusLabel.Text = "全自動校準：Android 亮度 " + bestValue + "，實測 " + bestNit.ToString("0.0", CultureInfo.CurrentCulture) + " nit";
                    brightnessStatusLabel.ForeColor = Green;
                    Log("全自動亮度校準完成：" + bestNit.ToString("0.000", CultureInfo.InvariantCulture) + " nit，Android=" + bestValue);
                    MessageBox.Show(this, autoBrightnessStatusLabel.Text, "全自動亮度校準完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    autoBrightnessStatusLabel.Text = Double.IsNaN(bestNit) ? "無法取得有效量測。" :
                        "已完成搜尋，但設備無法在允許誤差內達到目標；最接近 " + bestNit.ToString("0.0", CultureInfo.CurrentCulture) +
                        " nit（Android 亮度 " + bestValue + "，誤差 " + bestError.ToString("0.0", CultureInfo.CurrentCulture) + " nit）。";
                    autoBrightnessStatusLabel.ForeColor = Red;
                    MessageBox.Show(this, autoBrightnessStatusLabel.Text + "\n\n可能原因：目標超出手機可達範圍、HDR／高亮度模式未啟用、畫面不是全白，或量測值有波動。",
                        "未能達到目標亮度", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                autoBrightnessStatusLabel.Text = "全自動調整失敗：" + ShortStatus(ex.Message, 300);
                autoBrightnessStatusLabel.ForeColor = Red;
                Log("全自動亮度調整失敗：" + ex.Message);
                MessageBox.Show(this, ex.Message + (measurementFailed ? "\n\n請先用「試量測」確認 ArgyllCMS 能辨識儀器。" : ""),
                    "全自動亮度調整失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                busy = false;
                brightnessApplying = false;
                autoBrightnessRunning = false;
                autoBrightnessCancelRequested = false;
                SetInstallButtons(true);
                SetAutoBrightnessUi(true);
                startAutoBrightnessButton.Text = "開始全自動調整";
                startAutoBrightnessButton.Enabled = true;
            }
        }

        private void SetAutoBrightnessUi(bool enabled)
        {
            if (spotreadPathTextBox != null) spotreadPathTextBox.Enabled = enabled;
            if (spotreadCorrectionTextBox != null) spotreadCorrectionTextBox.Enabled = enabled;
            if (browseSpotreadButton != null) browseSpotreadButton.Enabled = enabled;
            if (browseSpotreadCorrectionButton != null) browseSpotreadCorrectionButton.Enabled = enabled;
            if (testMeterButton != null) testMeterButton.Enabled = enabled;
            if (openWhitePatternButton != null) openWhitePatternButton.Enabled = enabled;
            if (autoBrightnessTargetNumber != null) autoBrightnessTargetNumber.Enabled = enabled;
            if (autoBrightnessToleranceNumber != null) autoBrightnessToleranceNumber.Enabled = enabled;
        }

        private static string ShortStatus(string text, int maximum)
        {
            text = CleanOutput(text);
            return text.Length <= maximum ? text : text.Substring(0, maximum) + "…";
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
            List<DeviceInfo> targetDevices = SelectedInstallDevices();
            if (targetDevices.Count == 0) return;
            busy = true;
            SetInstallButtons(false);
            int totalSuccess = 0;
            int totalFailed = 0;
            Dictionary<string, int> successByPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> lastFailureByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            StringBuilder report = new StringBuilder();
            try
            {
                Log("開始安裝 " + title + "，共 " + jobs.Count + " 個 APK，目標 " + targetDevices.Count + " 台裝置。");
                foreach (DeviceInfo device in targetDevices)
                {
                    int deviceSuccess = 0;
                    int deviceFailed = 0;
                    List<string> deviceFailedNames = new List<string>();
                    string deviceLabel = device.DisplayName + "｜" + device.Serial + "｜" + device.ConnectionLabel;
                    Log("APK 安裝裝置：" + deviceLabel);
                    foreach (Tuple<string, ListViewItem> job in jobs)
                    {
                        string path = job.Item1 ?? "";
                        ListViewItem item = job.Item2;
                        int currentSuccess;
                        if (!successByPath.TryGetValue(path, out currentSuccess)) successByPath[path] = 0;
                        if (!File.Exists(path))
                        {
                            string reason = "檔案不存在";
                            lastFailureByPath[path] = reason;
                            deviceFailed++;
                            totalFailed++;
                            deviceFailedNames.Add(Path.GetFileName(path));
                            Log("[" + deviceLabel + "] 略過不存在的檔案：" + path);
                            continue;
                        }

                        SetItemStatus(item, targetDevices.Count == 1 ? "安裝中..." :
                            "正在安裝到 " + device.DisplayName + "...", Color.FromArgb(255, 190, 75));
                        Log("[" + deviceLabel + "] 安裝：" + Path.GetFileName(path));
                        string flags = "-r" + (settings.AllowDowngrade ? " -d" : "");
                        string args = "-s " + Quote(device.Serial) + " install " + flags + " " + Quote(path);
                        AdbResult result = await RunAdbAsync(args);
                        string combined = ((result.Output ?? "") + " " + (result.Error ?? "")).Trim();
                        bool ok = result.Started && result.ExitCode == 0 &&
                            combined.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (ok)
                        {
                            successByPath[path] = successByPath[path] + 1;
                            deviceSuccess++;
                            totalSuccess++;
                            Log("[" + deviceLabel + "] 安裝成功：" + Path.GetFileName(path));
                        }
                        else
                        {
                            string reason = FriendlyInstallError(combined);
                            lastFailureByPath[path] = reason;
                            deviceFailed++;
                            totalFailed++;
                            deviceFailedNames.Add(Path.GetFileName(path));
                            Log("[" + deviceLabel + "] 安裝失敗：" + Path.GetFileName(path) + " / " + CleanOutput(combined));
                        }
                    }

                    report.AppendLine(device.DisplayName + "　｜　" + device.Serial + "　｜　" + device.ConnectionLabel);
                    report.AppendLine("成功：" + deviceSuccess + "　失敗：" + deviceFailed);
                    if (deviceFailedNames.Count > 0)
                    {
                        string failedText = String.Join("、", deviceFailedNames.Take(4).ToArray());
                        if (deviceFailedNames.Count > 4) failedText += " 等 " + deviceFailedNames.Count + " 項";
                        report.AppendLine("失敗項目：" + failedText);
                    }
                    report.AppendLine();
                }

                foreach (Tuple<string, ListViewItem> job in jobs)
                {
                    if (job.Item2 == null) continue;
                    int installedCount;
                    if (!successByPath.TryGetValue(job.Item1, out installedCount)) installedCount = 0;
                    if (!File.Exists(job.Item1))
                        SetItemStatus(job.Item2, "失敗：檔案不存在", Red);
                    else if (targetDevices.Count == 1 && installedCount == 1)
                        SetItemStatus(job.Item2, "成功（ADB 已確認）", Green);
                    else if (targetDevices.Count == 1)
                    {
                        string reason;
                        if (!lastFailureByPath.TryGetValue(job.Item1, out reason)) reason = "ADB 未回報成功";
                        SetItemStatus(job.Item2, "失敗：" + reason, Red);
                    }
                    else if (installedCount == targetDevices.Count)
                        SetItemStatus(job.Item2, installedCount + "/" + targetDevices.Count + " 台成功", Green);
                    else
                        SetItemStatus(job.Item2, installedCount + "/" + targetDevices.Count + " 台成功，" +
                            (targetDevices.Count - installedCount) + " 台失敗", Red);
                }
            }
            finally
            {
                busy = false;
                SetInstallButtons(true);
            }
            Log("安裝完成：裝置 " + targetDevices.Count + " 台，成功 " + totalSuccess + "，失敗 " + totalFailed + "。");
            MessageBox.Show(this, "安裝完成\n\n" + report.ToString().TrimEnd() +
                (totalFailed > 0 ? "\n\n可到「執行紀錄」查看各裝置詳細原因。" : ""),
                title, MessageBoxButtons.OK, totalFailed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void SetInstallButtons(bool enabled)
        {
            installGroupButton.Enabled = enabled;
            refreshButton.Enabled = enabled;
            browseAdbButton.Enabled = enabled;
            if (deviceSelector != null) deviceSelector.Enabled = enabled && ReadyDevices().Count > 1;
            if (installAllDevicesCheck != null) installAllDevicesCheck.Enabled = enabled && ReadyDevices().Count > 1;
            if (dropPanel != null)
            {
                dropPanel.Enabled = enabled;
                dropPanel.Cursor = enabled ? Cursors.Hand : Cursors.WaitCursor;
                dropPanel.Invalidate();
            }
            if (transferDropPanel != null)
            {
                transferDropPanel.Enabled = enabled;
                transferDropPanel.Cursor = enabled ? Cursors.Default : Cursors.WaitCursor;
                transferDropPanel.Invalidate();
            }
            if (quickTransferDestinationComboBox != null) quickTransferDestinationComboBox.Enabled = enabled;
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
                float scale = Math.Max(1F, currentDpiScale);
                Rectangle workArea = Screen.FromControl(this).WorkingArea;
                int outerMargin = ScaleValue(24, scale);
                int desiredWidth = ScaleValue(560, scale);
                int desiredHeight = ScaleValue(300, scale);
                bool renaming = String.Equals(title, "重新命名", StringComparison.Ordinal);

                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowIcon = false;
                form.ShowInTaskbar = false;
                form.AutoScaleMode = AutoScaleMode.None;
                form.ClientSize = new Size(Math.Min(desiredWidth, workArea.Width - outerMargin * 2),
                    Math.Min(desiredHeight, workArea.Height - outerMargin * 2));
                form.BackColor = Bg;
                form.ForeColor = TextColor;
                form.Font = Font;
                form.Padding = ScalePadding(new Padding(30, 24, 30, 22), scale);

                TableLayoutPanel layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Bg,
                    ColumnCount = 1,
                    RowCount = 6,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(42, scale)));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(38, scale)));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(30, scale)));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(46, scale)));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(52, scale)));

                Label heading = new Label
                {
                    Text = renaming ? "重新命名安裝組合" : "建立新的安裝組合",
                    ForeColor = TextColor,
                    Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                Label description = new Label
                {
                    Text = renaming ? "輸入新的組合名稱，儲存後會立即更新清單。" : "輸入容易辨識的名稱，建立後即可加入 APK。",
                    ForeColor = Muted,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                Label prompt = new Label
                {
                    Text = label,
                    ForeColor = TextColor,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.BottomLeft
                };
                TextBox input = new TextBox
                {
                    Text = value,
                    BackColor = Card2,
                    ForeColor = TextColor,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font(Font.FontFamily, 11.5F),
                    Dock = DockStyle.Fill,
                    Margin = ScalePadding(new Padding(0, 6, 0, 5), scale),
                    MaxLength = 100
                };

                FlowLayoutPanel buttons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Bg,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    Margin = new Padding(0),
                    Padding = ScalePadding(new Padding(0, 6, 0, 0), scale)
                };
                Button ok = NewButton(renaming ? "儲存名稱" : "建立組合", true, 126);
                ok.Size = new Size(ScaleValue(126, scale), ScaleValue(42, scale));
                ok.Margin = ScalePadding(new Padding(10, 0, 0, 0), scale);
                ok.DialogResult = DialogResult.OK;
                Button cancel = NewButton("取消", false, 96);
                cancel.Size = new Size(ScaleValue(96, scale), ScaleValue(42, scale));
                cancel.Margin = new Padding(0);
                cancel.DialogResult = DialogResult.Cancel;
                buttons.Controls.Add(ok);
                buttons.Controls.Add(cancel);

                layout.Controls.Add(heading, 0, 0);
                layout.Controls.Add(description, 0, 1);
                layout.Controls.Add(prompt, 0, 2);
                layout.Controls.Add(input, 0, 3);
                layout.Controls.Add(buttons, 0, 5);
                form.Controls.Add(layout);
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                form.Shown += delegate { input.Focus(); input.SelectAll(); };
                return form.ShowDialog(this) == DialogResult.OK ? input.Text : null;
            }
        }
    }
}
