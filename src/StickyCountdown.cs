using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace StickyCountdown
{
    static class Ui
    {
        public static readonly Color Card = Color.FromArgb(251, 252, 254);
        public static readonly Color Border = Color.FromArgb(227, 231, 241);
        public static readonly Color Separator = Color.FromArgb(236, 239, 246);
        public static readonly Color Title = Color.FromArgb(27, 30, 40);
        public static readonly Color Hint = Color.FromArgb(138, 144, 166);
        public static readonly Color Text = Color.FromArgb(34, 38, 52);
        public static readonly Color Count = Color.FromArgb(20, 23, 31);
        public static readonly Color Ready = Color.FromArgb(24, 150, 92);
        public static readonly Color Idle = Color.FromArgb(169, 176, 196);
        public static readonly Color RowFill = Color.FromArgb(244, 246, 251);
        public static readonly Color RowHover = Color.FromArgb(237, 240, 249);
        public static readonly Color NotesFill = Color.FromArgb(247, 249, 253);
        public static readonly Color DotCounting = Color.FromArgb(77, 124, 255);
        public static readonly Color DotReady = Color.FromArgb(34, 181, 115);
        public static readonly Color DotIdle = Color.FromArgb(201, 207, 220);
        public static readonly Color BtnFill = Color.FromArgb(244, 246, 251);
        public static readonly Color BtnHover = Color.FromArgb(237, 240, 249);
        public static readonly Color BtnDown = Color.FromArgb(229, 233, 244);
        public static readonly Color BtnText = Color.FromArgb(58, 65, 82);
        public static readonly Color BtnBorder = Color.FromArgb(227, 231, 241);
        public static readonly Color CloseText = Color.FromArgb(106, 113, 134);
        public static readonly Color CloseHover = Color.FromArgb(240, 224, 226);

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0) d = 2;
            if (d > r.Height) d = r.Height;
            if (d > r.Width) d = r.Width;
            if (d <= 0) d = 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            try { SetProcessDPIAware(); }
            catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new StickyForm());
        }
    }

    class RoundedPanel : Panel
    {
        public int Radius = 10;
        public Color FillColor = Ui.RowFill;
        public Color BorderColor = Color.Empty;

        public RoundedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Card;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath p = Ui.Round(r, Radius))
            {
                using (Brush b = new SolidBrush(FillColor))
                    e.Graphics.FillPath(b, p);
                if (!BorderColor.IsEmpty && BorderColor.A > 0)
                {
                    using (Pen pen = new Pen(BorderColor))
                        e.Graphics.DrawPath(pen, p);
                }
            }
        }
    }

    class Dot : Panel
    {
        public Color DotColor = Ui.DotIdle;

        public Dot()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.RowFill;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);
            using (Brush b = new SolidBrush(DotColor))
                e.Graphics.FillEllipse(b, 0, 0, Width - 1, Height - 1);
        }
    }

    class RoundButton : Button
    {
        public int Radius = 10;

        public RoundButton()
        {
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            TabStop = false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width <= 0 || Height <= 0) return;
            Region old = Region;
            Region = new Region(Ui.Round(new Rectangle(0, 0, Width, Height), Radius));
            if (old != null) old.Dispose();
        }
    }

    class Row
    {
        public RoundedPanel Host;
        public Dot StatusDot;
        public TextBox NameBox;
        public Label TimeLabel;
        public ContextMenuStrip Strip;
        public string NameBoxText = "";
        public string Mode = "none";
        public double Hours = 5;
        public string UntilText = "22:00";
        public DateTime Target;
        public bool Ready;
    }

    class StickyForm : Form
    {
        const int MaxRows = 10;
        const int EM_SETCUEBANNER = 0x1501;

        const uint WM_NCLBUTTONDOWN = 0xA1;
        const int HTCAPTION = 2;
        const int HWND_TOPMOST = -1;
        const int HWND_NOTOPMOST = -2;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        readonly List<Row> rows = new List<Row>();

        bool topMostOn = true;
        bool hasPos;
        Point loadPos;
        float scale = 1f;
        string settingsPath;
        string fallbackSettingsPath;
        string notesText = "";
        bool fieldDirty;
        int fieldChangedAt;
        bool applying;

        Label titleLabel;
        Label catLabel;
        Label hintLabel;
        RoundButton closeBtn;
        RoundButton addBtn;
        RoundedPanel notesHost;
        TextBox notesBox;
        ContextMenuStrip globalMenu;
        ToolStripMenuItem miTopMost;
        ToolTip tip;
        System.Windows.Forms.Timer tick;
        Font countFont;

        int S(int v)
        {
            return (int)Math.Round(v * scale);
        }

        public StickyForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "Limit LLM";
            BackColor = Ui.Card;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);

            tip = new ToolTip();
            countFont = new Font("Consolas", 12f, FontStyle.Bold);
            LoadSettings();
            TopMost = topMostOn;
            BuildUi();
            ApplySettings();

            tick = new System.Windows.Forms.Timer();
            tick.Interval = 500;
            tick.Tick += new EventHandler(OnTick);
            tick.Start();
        }

        void ApplyTopMost()
        {
            if (!IsHandleCreated) return;
            SetWindowPos(Handle, new IntPtr(topMostOn ? HWND_TOPMOST : HWND_NOTOPMOST), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            using (Graphics g = Graphics.FromHwnd(Handle))
                scale = g.DpiX / 96f;
            LayoutAll();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            TopMost = topMostOn;
            ApplyTopMost();
            ActiveControl = null;
            foreach (Row r in rows)
                r.NameBox.DeselectAll();
            notesBox.DeselectAll();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Region old = Region;
            Region = new Region(Ui.Round(new Rectangle(0, 0, Width, Height), S(14)));
            if (old != null) old.Dispose();
            LayoutAll();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.Clear(Ui.Card);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen pen = new Pen(Ui.Border, S(2)))
            using (GraphicsPath path = Ui.Round(new Rectangle(S(1), S(1), Width - S(3), Height - S(3)), S(13)))
                g.DrawPath(pen, path);

            using (Pen pen = new Pen(Ui.Separator, 1))
                g.DrawLine(pen, S(14), S(40), Width - S(14), S(40));
        }

        void BuildUi()
        {
            using (Graphics g = CreateGraphics())
                scale = g.DpiX / 96f;

            titleLabel = new Label();
            titleLabel.AutoSize = false;
            titleLabel.BackColor = Color.Transparent;
            titleLabel.ForeColor = Ui.Title;
            titleLabel.Font = new Font("Segoe UI Semibold", 11.5f);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.Text = "Limit LLM";
            titleLabel.Cursor = Cursors.SizeAll;

            closeBtn = MakeButton("\u00D7", 12);
            closeBtn.Font = new Font("Segoe UI", 12f);
            closeBtn.ForeColor = Ui.CloseText;
            closeBtn.BackColor = Ui.Card;
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.FlatAppearance.MouseOverBackColor = Ui.CloseHover;
            closeBtn.FlatAppearance.MouseDownBackColor = Ui.CloseHover;
            closeBtn.Click += delegate(object s, EventArgs e) { Close(); };

            addBtn = MakeButton("+  Tambah model", 10);
            addBtn.Font = new Font("Segoe UI", 9.5f);
            addBtn.Click += delegate(object s, EventArgs e) { AddRow(null, true); };

            catLabel = new Label();
            catLabel.AutoSize = false;
            catLabel.BackColor = Color.Transparent;
            catLabel.ForeColor = Ui.Hint;
            catLabel.Font = new Font("Segoe UI Semibold", 7.5f);
            catLabel.TextAlign = ContentAlignment.MiddleLeft;
            catLabel.Text = "CATATAN";
            catLabel.Cursor = Cursors.SizeAll;

            hintLabel = new Label();
            hintLabel.AutoSize = false;
            hintLabel.BackColor = Color.Transparent;
            hintLabel.ForeColor = Ui.Hint;
            hintLabel.Font = new Font("Segoe UI", 7.5f);
            hintLabel.TextAlign = ContentAlignment.MiddleRight;
            hintLabel.Text = "klik kanan baris = atur reset";
            hintLabel.Cursor = Cursors.SizeAll;

            notesHost = new RoundedPanel();
            notesHost.Radius = S(10);
            notesHost.FillColor = Ui.NotesFill;
            notesHost.BorderColor = Ui.BtnBorder;

            notesBox = new TextBox();
            notesBox.Multiline = true;
            notesBox.BorderStyle = BorderStyle.None;
            notesBox.BackColor = Ui.NotesFill;
            notesBox.ForeColor = Ui.Text;
            notesBox.Font = new Font("Segoe UI", 9.5f);
            notesBox.ScrollBars = ScrollBars.Vertical;
            notesBox.TextChanged += delegate(object s, EventArgs e) { fieldDirty = true; fieldChangedAt = Environment.TickCount; };
            notesBox.Leave += delegate(object s, EventArgs e) { SaveSettings(); };
            notesHost.Controls.Add(notesBox);

            Controls.Add(titleLabel);
            Controls.Add(closeBtn);
            Controls.Add(addBtn);
            Controls.Add(catLabel);
            Controls.Add(hintLabel);
            Controls.Add(notesHost);

            BuildGlobalMenu();

            AttachDrag(titleLabel);
            AttachDrag(catLabel);
            AttachDrag(hintLabel);
            AttachDrag(this);

            tip.SetToolTip(titleLabel, "Drag: pindah \u2022 Klik kanan: menu");
            tip.SetToolTip(addBtn, "Tambah baris model baru");
            tip.SetToolTip(notesHost, "Catatan bebas: jam pakai ideal, info harga per jam, dll.");

            foreach (Row r in rows)
                AttachRow(r);
        }

        RoundButton MakeButton(string text, int radius)
        {
            RoundButton b = new RoundButton();
            b.Radius = radius;
            b.Text = text;
            b.BackColor = Ui.BtnFill;
            b.ForeColor = Ui.BtnText;
            b.FlatAppearance.BorderColor = Ui.BtnBorder;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Ui.BtnHover;
            b.FlatAppearance.MouseDownBackColor = Ui.BtnDown;
            return b;
        }

        void BuildGlobalMenu()
        {
            globalMenu = new ContextMenuStrip();

            ToolStripMenuItem miAdd = new ToolStripMenuItem("Tambah model");
            miAdd.Click += delegate(object s, EventArgs e) { AddRow(null, true); };

            miTopMost = new ToolStripMenuItem("Selalu di atas");
            miTopMost.CheckOnClick = true;
            miTopMost.CheckedChanged += delegate(object s, EventArgs e)
            {
                if (applying) return;
                topMostOn = miTopMost.Checked;
                TopMost = topMostOn;
                ApplyTopMost();
                SaveSettings();
            };

            ToolStripMenuItem miExit = new ToolStripMenuItem("Keluar");
            miExit.Click += delegate(object s, EventArgs e) { Close(); };

            globalMenu.Items.Add(miAdd);
            globalMenu.Items.Add(miTopMost);
            globalMenu.Items.Add(new ToolStripSeparator());
            globalMenu.Items.Add(miExit);
            globalMenu.Opening += delegate(object s, System.ComponentModel.CancelEventArgs e)
            {
                applying = true;
                miTopMost.Checked = topMostOn;
                applying = false;
            };

            ContextMenuStrip = globalMenu;
        }

        Row NewRow(Row source)
        {
            Row r = new Row();

            r.Host = new RoundedPanel();
            r.Host.Radius = S(10);
            r.Host.FillColor = Ui.RowFill;

            r.StatusDot = new Dot();
            r.StatusDot.DotColor = Ui.DotIdle;

            r.NameBox = new TextBox();
            r.NameBox.BorderStyle = BorderStyle.None;
            r.NameBox.BackColor = Ui.RowFill;
            r.NameBox.ForeColor = Ui.Text;
            r.NameBox.Font = new Font("Segoe UI", 10f);
            r.NameBox.MaxLength = 40;
            r.NameBox.TextChanged += delegate(object s, EventArgs e) { fieldDirty = true; fieldChangedAt = Environment.TickCount; };

            r.TimeLabel = new Label();
            r.TimeLabel.AutoSize = false;
            r.TimeLabel.BackColor = Color.Transparent;
            r.TimeLabel.ForeColor = Ui.Idle;
            r.TimeLabel.Font = countFont;
            r.TimeLabel.TextAlign = ContentAlignment.MiddleRight;
            r.TimeLabel.Cursor = Cursors.Hand;
            r.TimeLabel.Text = "--:--:--";

            if (source != null)
            {
                r.Mode = source.Mode;
                r.Hours = source.Hours;
                r.UntilText = source.UntilText;
                r.Target = source.Target;
                r.Ready = source.Ready;
                r.NameBox.Text = source.NameBoxText;
            }

            r.Host.Controls.Add(r.StatusDot);
            r.Host.Controls.Add(r.NameBox);
            r.Host.Controls.Add(r.TimeLabel);

            BuildRowStrip(r);
            Controls.Add(r.Host);

            try { SendMessage(r.NameBox.Handle, EM_SETCUEBANNER, (IntPtr)1, "Nama model"); }
            catch { }

            return r;
        }

        void BuildRowStrip(Row r)
        {
            r.Strip = new ContextMenuStrip();
            r.Strip.Opening += delegate(object s, System.ComponentModel.CancelEventArgs e)
            {
                ContextMenuStrip strip = (ContextMenuStrip)s;
                strip.Items.Clear();

                string current;
                if (r.Ready) current = "Status: SIAP dipakai lagi";
                else if (r.Mode == "hours") current = "Countdown " + r.Hours.ToString("0.##", CultureInfo.InvariantCulture) + " jam";
                else if (r.Mode == "until") current = "Sampai jam " + r.UntilText;
                else current = "Belum diatur";
                ToolStripMenuItem miInfo = new ToolStripMenuItem(current);
                miInfo.Enabled = false;
                strip.Items.Add(miInfo);
                strip.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem miHours = new ToolStripMenuItem("Hitung mundur ... jam");
                miHours.Click += delegate(object s2, EventArgs e2) { PromptSetHours(r); };
                ToolStripMenuItem miUntil = new ToolStripMenuItem("Sampai jam ...");
                miUntil.Click += delegate(object s2, EventArgs e2) { PromptSetUntil(r); };
                strip.Items.Add(miHours);
                strip.Items.Add(miUntil);

                if (r.Mode != "none" || r.Ready)
                {
                    ToolStripMenuItem miClear = new ToolStripMenuItem("Bersihkan");
                    miClear.Click += delegate(object s2, EventArgs e2) { ClearRow(r); };
                    strip.Items.Add(miClear);
                }

                strip.Items.Add(new ToolStripSeparator());
                ToolStripMenuItem miDel = new ToolStripMenuItem("Hapus baris");
                miDel.Enabled = rows.Count > 1;
                miDel.Click += delegate(object s2, EventArgs e2) { RemoveRow(r); };
                strip.Items.Add(miDel);
            };
        }

        void AttachRow(Row r)
        {
            r.Host.ContextMenuStrip = r.Strip;
            r.StatusDot.ContextMenuStrip = r.Strip;
            r.NameBox.ContextMenuStrip = r.Strip;
            r.TimeLabel.ContextMenuStrip = r.Strip;

            r.TimeLabel.MouseUp += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                    r.Strip.Show(Cursor.Position);
            };

            MouseEventHandler enter = delegate(object s, MouseEventArgs e) { SetRowHover(r, true); };
            EventHandler enterE = delegate(object s, EventArgs e) { SetRowHover(r, true); };
            EventHandler leaveE = delegate(object s, EventArgs e) { SetRowHover(r, false); };
            r.Host.MouseEnter += enterE;
            r.Host.MouseLeave += leaveE;
            r.StatusDot.MouseEnter += enterE;
            r.StatusDot.MouseLeave += leaveE;
            r.TimeLabel.MouseEnter += enterE;
            r.TimeLabel.MouseLeave += leaveE;
            r.NameBox.MouseEnter += enterE;
            r.NameBox.MouseLeave += leaveE;

            tip.SetToolTip(r.TimeLabel, "Klik untuk atur reset");
        }

        void SetRowHover(Row r, bool on)
        {
            Color fill = on ? Ui.RowHover : Ui.RowFill;
            r.Host.FillColor = fill;
            r.Host.Invalidate();
            r.StatusDot.BackColor = fill;
            r.StatusDot.Invalidate();
            r.NameBox.BackColor = fill;
        }

        void AddRow(Row source, bool save)
        {
            if (rows.Count >= MaxRows) return;
            Row r = NewRow(source);
            rows.Add(r);
            AttachRow(r);
            LayoutAll();
            RefreshRow(r);
            if (save) SaveSettings();
        }

        void RemoveRow(Row r)
        {
            if (rows.Count <= 1) return;
            Controls.Remove(r.Host);
            r.Host.Dispose();
            if (r.Strip != null) r.Strip.Dispose();
            rows.Remove(r);
            LayoutAll();
            SaveSettings();
        }

        void LayoutAll()
        {
            if (titleLabel == null) return;
            int pad = S(12);
            int w = S(340);

            titleLabel.SetBounds(pad, S(10), w - pad * 2 - S(34), S(22));
            closeBtn.SetBounds(w - pad - S(24), S(9), S(24), S(24));

            int hostW = w - pad * 2;
            int rowH = S(34);
            int rowStep = S(40);
            int y = S(48);
            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                r.Host.Radius = S(10);
                r.Host.SetBounds(pad, y, hostW, rowH);
                r.StatusDot.SetBounds(S(11), S(13), S(8), S(8));
                r.NameBox.SetBounds(S(28), S(5), S(150), S(24));
                r.TimeLabel.SetBounds(S(184), 0, hostW - S(194), rowH);
                y += rowStep;
            }

            int addY = S(52) + rows.Count * rowStep - S(10) + S(10);
            addBtn.SetBounds(pad, addY, hostW, S(30));
            addBtn.Enabled = rows.Count < MaxRows;

            int catY = addY + S(38);
            catLabel.SetBounds(pad, catY, S(120), S(12));
            hintLabel.SetBounds(w - pad - S(170), catY, S(170), S(12));

            int notesY = catY + S(16);
            notesHost.SetBounds(pad, notesY, hostW, S(64));
            notesBox.SetBounds(S(9), S(7), hostW - S(18), S(50));

            int newH = notesY + S(64) + S(12);
            if (ClientSize.Height != newH || ClientSize.Width != w)
                ClientSize = new Size(w, newH);
        }

        void RefreshRow(Row r)
        {
            if (r.Ready)
            {
                r.TimeLabel.Text = "SIAP";
                r.TimeLabel.ForeColor = Ui.Ready;
                r.StatusDot.DotColor = Ui.DotReady;
                r.StatusDot.Invalidate();
                return;
            }
            if (r.Mode == "none")
            {
                r.TimeLabel.Text = "--:--:--";
                r.TimeLabel.ForeColor = Ui.Idle;
                r.StatusDot.DotColor = Ui.DotIdle;
                r.StatusDot.Invalidate();
                return;
            }
            TimeSpan rem = r.Target - DateTime.Now;
            if (rem <= TimeSpan.Zero)
            {
                r.Ready = true;
                r.TimeLabel.Text = "SIAP";
                r.TimeLabel.ForeColor = Ui.Ready;
                r.StatusDot.DotColor = Ui.DotReady;
                r.StatusDot.Invalidate();
                SaveSettings();
                return;
            }
            r.TimeLabel.Text = FormatRemaining(rem);
            r.TimeLabel.ForeColor = Ui.Count;
            r.StatusDot.DotColor = Ui.DotCounting;
            r.StatusDot.Invalidate();
        }

        void OnTick(object s, EventArgs e)
        {
            foreach (Row r in rows)
                RefreshRow(r);

            if (fieldDirty && Environment.TickCount - fieldChangedAt > 3000)
            {
                fieldDirty = false;
                SaveSettings();
            }
        }

        void PromptSetHours(Row r)
        {
            string def = r.Mode == "hours" ? r.Hours.ToString("0.##", CultureInfo.InvariantCulture) : "5";
            string input = Interaction.InputBox("Countdown berapa jam? (boleh desimal, contoh 5 atau 4.5)", "Hitung mundur", def);
            if (input == null || input.Trim().Length == 0) return;
            double n;
            string t = input.Trim().Replace(',', '.');
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out n) || n < 0.1 || n > 168)
            {
                MessageBox.Show("Masukkan angka jam antara 0.1 sampai 168.", "Limit LLM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            r.Mode = "hours";
            r.Hours = n;
            r.Target = DateTime.Now.AddHours(n);
            r.Ready = false;
            RefreshRow(r);
            SaveSettings();
        }

        void PromptSetUntil(Row r)
        {
            string def = r.Mode == "until" ? r.UntilText : "22:00";
            string input = Interaction.InputBox("Sampai jam berapa? Format 24 jam, contoh 22:00", "Sampai jam", def);
            if (input == null || input.Trim().Length == 0) return;
            int hh, mm;
            if (!TryParseClock(input, out hh, out mm))
            {
                MessageBox.Show("Format jam tidak valid. Contoh yang benar: 22:00 atau 5:30.", "Limit LLM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string nice = string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", hh, mm);
            DateTime t = DateTime.Today.AddHours(hh).AddMinutes(mm);
            if (t <= DateTime.Now)
            {
                DialogResult dr = MessageBox.Show("Jam " + nice + " sudah lewat hari ini. Hitung ke besok jam " + nice + "?", "Limit LLM", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.No)
                {
                    r.Mode = "until";
                    r.UntilText = nice;
                    r.Ready = true;
                    RefreshRow(r);
                    SaveSettings();
                    return;
                }
                t = t.AddDays(1);
            }
            r.Mode = "until";
            r.UntilText = nice;
            r.Target = t;
            r.Ready = false;
            RefreshRow(r);
            SaveSettings();
        }

        void ClearRow(Row r)
        {
            r.Mode = "none";
            r.Ready = false;
            r.Target = DateTime.MinValue;
            RefreshRow(r);
            SaveSettings();
        }

        Point DefaultLocation()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            return new Point(wa.Right - Width - S(48), wa.Top + S(80));
        }

        bool IsOnScreen(Point p)
        {
            Rectangle rect = new Rectangle(p, Size);
            foreach (Screen s in Screen.AllScreens)
            {
                if (s.WorkingArea.IntersectsWith(rect)) return true;
            }
            return false;
        }

        void AttachDrag(Control c)
        {
            c.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                SaveSettings();
            };
        }

        void ApplySettings()
        {
            applying = true;
            TopMost = topMostOn;
            applying = false;

            notesBox.Text = notesText;
            LayoutAll();
            if (hasPos && IsOnScreen(loadPos))
                Location = loadPos;
            else
                Location = DefaultLocation();

            foreach (Row r in rows)
                RefreshRow(r);

            applying = true;
            miTopMost.Checked = topMostOn;
            applying = false;
        }

        static bool TryParseClock(string s, out int hh, out int mm)
        {
            hh = 0; mm = 0;
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Trim();
            string[] formats = new string[] { "H:mm", "HH:mm", "H.mm", "HH.mm", "H mm", "HH mm" };
            DateTime dt;
            if (!DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return false;
            hh = dt.Hour;
            mm = dt.Minute;
            return true;
        }

        static string FormatRemaining(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return Two(t.Hours) + ":" + Two(t.Minutes) + ":" + Two(t.Seconds);
        }

        static string Two(int n)
        {
            return n.ToString("00", CultureInfo.InvariantCulture);
        }

        static string Esc(string s)
        {
            if (s == null) return "";
            s = s.Replace("\\", "\\\\");
            s = s.Replace("\r\n", "\\n");
            s = s.Replace("\n", "\\n");
            s = s.Replace("\r", "\\n");
            return s;
        }

        static string Unesc(string s)
        {
            if (s == null) return "";
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    if (s[i] == 'n') b.Append('\n');
                    else if (s[i] == '\\') b.Append('\\');
                    else { b.Append('\\'); b.Append(s[i]); }
                }
                else b.Append(s[i]);
            }
            return b.ToString();
        }

        void LoadSettings()
        {
            settingsPath = Path.Combine(Application.StartupPath, "settings.ini");
            fallbackSettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StickyCountdown", "settings.ini");
            if (!File.Exists(settingsPath) && File.Exists(fallbackSettingsPath))
                settingsPath = fallbackSettingsPath;

            int count = 3;
            if (!File.Exists(settingsPath))
            {
                for (int i = 0; i < count; i++) AddRow(null, false);
                return;
            }

            Dictionary<string, string> d = new Dictionary<string, string>();
            try
            {
                foreach (string raw in File.ReadAllLines(settingsPath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = line.Substring(eq + 1);
                    d[key] = val;
                }
            }
            catch { }

            string v;
            if (d.TryGetValue("topmost", out v)) topMostOn = v.Trim() == "1";
            if (d.TryGetValue("notes", out v)) notesText = Unesc(v);
            if (d.TryGetValue("x", out v))
            {
                int x;
                if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out x))
                {
                    string yv;
                    int y;
                    if (d.TryGetValue("y", out yv) && int.TryParse(yv, NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
                    {
                        loadPos = new Point(x, y);
                        hasPos = true;
                    }
                }
            }
            if (d.TryGetValue("count", out v))
            {
                int n;
                if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                    count = Math.Max(1, Math.Min(MaxRows, n));
            }

            for (int i = 1; i <= count; i++)
            {
                Row r = new Row();
                string p = "row" + i.ToString(CultureInfo.InvariantCulture) + ".";
                string s;
                if (d.TryGetValue(p + "name", out s)) r.NameBoxText = Unesc(s);
                if (d.TryGetValue(p + "mode", out s) && (s == "hours" || s == "until")) r.Mode = s;
                if (d.TryGetValue(p + "hours", out s))
                {
                    double h;
                    if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out h) && h >= 0.1 && h <= 168) r.Hours = h;
                }
                if (d.TryGetValue(p + "until", out s))
                {
                    int hh, mm;
                    if (TryParseClock(s, out hh, out mm))
                        r.UntilText = string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", hh, mm);
                }
                if (d.TryGetValue(p + "target", out s))
                {
                    DateTime t;
                    if (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
                        r.Target = t;
                }
                if (r.Mode != "none" && r.Target != DateTime.MinValue && r.Target <= DateTime.Now)
                    r.Ready = true;
                AddRow(r, false);
            }

            if (rows.Count == 0)
            {
                for (int i = 0; i < 3; i++) AddRow(null, false);
            }
        }

        void SaveSettings()
        {
            if (titleLabel == null) return;

            notesText = notesBox.Text;

            StringBuilder sb = new StringBuilder();
            sb.Append("; Limit LLM settings - ubah lewat klik kanan pada baris\r\n");
            sb.Append("topmost=").Append(topMostOn ? "1" : "0").Append("\r\n");
            sb.Append("x=").Append(Location.X.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            sb.Append("y=").Append(Location.Y.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            sb.Append("notes=").Append(Esc(notesText)).Append("\r\n");
            sb.Append("count=").Append(rows.Count.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                string p = "row" + (i + 1).ToString(CultureInfo.InvariantCulture) + ".";
                sb.Append(p).Append("name=").Append(Esc(r.NameBox.Text)).Append("\r\n");
                sb.Append(p).Append("mode=").Append(r.Mode).Append("\r\n");
                sb.Append(p).Append("hours=").Append(r.Hours.ToString("0.##", CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append(p).Append("until=").Append(r.UntilText).Append("\r\n");
                sb.Append(p).Append("target=").Append(r.Target == DateTime.MinValue ? "" : r.Target.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                sb.Append("\r\n");
            }

            string content = sb.ToString();
            try
            {
                File.WriteAllText(settingsPath, content);
            }
            catch
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(fallbackSettingsPath));
                    File.WriteAllText(fallbackSettingsPath, content);
                    settingsPath = fallbackSettingsPath;
                }
                catch { }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettings();
            base.OnFormClosing(e);
        }
    }
}

