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

    class Row
    {
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
        static readonly Color NoteColor = Color.FromArgb(254, 243, 160);
        static readonly Color FoldColor = Color.FromArgb(236, 220, 132);
        static readonly Color BorderColor = Color.FromArgb(214, 199, 116);
        static readonly Color InkColor = Color.FromArgb(62, 57, 38);
        static readonly Color SubColor = Color.FromArgb(146, 137, 96);
        static readonly Color ReadyColor = Color.FromArgb(46, 125, 50);
        static readonly Color ButtonBackColor = Color.FromArgb(246, 232, 143);
        static readonly Color ButtonBorderColor = Color.FromArgb(200, 186, 104);
        static readonly Color ButtonHoverColor = Color.FromArgb(240, 224, 124);
        static readonly Color ButtonDownColor = Color.FromArgb(232, 214, 112);

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
        bool nameDirty;
        int nameChangedAt;
        bool applying;

        Label titleLabel;
        Label hintLabel;
        Button closeBtn;
        Button addBtn;
        ContextMenuStrip globalMenu;
        ToolStripMenuItem miTopMost;
        ToolTip tip;
        System.Windows.Forms.Timer tick;

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
            BackColor = NoteColor;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);

            tip = new ToolTip();
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
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Region old = Region;
            Region = new Region(RoundRect(new Rectangle(0, 0, Width, Height), S(10)));
            if (old != null) old.Dispose();
            LayoutAll();
            Invalidate();
        }

        static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0) d = 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.Clear(NoteColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen pen = new Pen(BorderColor, S(2)))
            using (GraphicsPath path = RoundRect(new Rectangle(S(1), S(1), Width - S(3), Height - S(3)), S(9)))
                g.DrawPath(pen, path);

            using (Pen pen = new Pen(Color.FromArgb(110, 205, 190, 110), 1))
                g.DrawLine(pen, S(12), S(34), Width - S(12), S(34));

            int f = S(20);
            Point[] fold = new Point[]
            {
                new Point(Width - f, Height - S(2)),
                new Point(Width - S(2), Height - f),
                new Point(Width - S(2), Height - S(2))
            };
            using (Brush b = new SolidBrush(FoldColor))
                g.FillPolygon(b, fold);
            using (Pen pen = new Pen(BorderColor, S(2)))
                g.DrawLine(pen, fold[0], fold[1]);
        }

        void BuildUi()
        {
            using (Graphics g = CreateGraphics())
                scale = g.DpiX / 96f;

            titleLabel = new Label();
            titleLabel.AutoSize = false;
            titleLabel.BackColor = Color.Transparent;
            titleLabel.ForeColor = InkColor;
            titleLabel.Font = new Font("Segoe UI Semibold", 11.5f);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.Text = "Limit LLM";
            titleLabel.Cursor = Cursors.SizeAll;

            closeBtn = MakeButton("\u00D7");
            closeBtn.Font = new Font("Segoe UI", 15f);
            closeBtn.ForeColor = SubColor;
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 170, 160);
            closeBtn.Click += delegate(object s, EventArgs e) { Close(); };
            closeBtn.MouseEnter += delegate(object s, EventArgs e) { closeBtn.ForeColor = InkColor; };
            closeBtn.MouseLeave += delegate(object s, EventArgs e) { closeBtn.ForeColor = SubColor; };

            addBtn = MakeButton("+ Tambah model");
            addBtn.Click += delegate(object s, EventArgs e) { AddRow(null, true); };

            hintLabel = new Label();
            hintLabel.AutoSize = false;
            hintLabel.BackColor = Color.Transparent;
            hintLabel.ForeColor = SubColor;
            hintLabel.Font = new Font("Segoe UI", 7.5f);
            hintLabel.TextAlign = ContentAlignment.MiddleRight;
            hintLabel.Text = "klik kanan = atur";
            hintLabel.Cursor = Cursors.SizeAll;

            Controls.Add(titleLabel);
            Controls.Add(closeBtn);
            Controls.Add(addBtn);
            Controls.Add(hintLabel);

            BuildGlobalMenu();

            AttachDrag(titleLabel);
            AttachDrag(hintLabel);
            AttachDrag(this);

            tip.SetToolTip(titleLabel, "Drag: pindah \u2022 Klik kanan: menu");
            tip.SetToolTip(addBtn, "Tambah baris model baru");

            foreach (Row r in rows)
                AttachRow(r);
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

        Button MakeButton(string text)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = ButtonBackColor;
            b.ForeColor = InkColor;
            b.Font = new Font("Segoe UI", 9f);
            b.UseVisualStyleBackColor = false;
            b.TabStop = false;
            b.FlatAppearance.BorderColor = ButtonBorderColor;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = ButtonHoverColor;
            b.FlatAppearance.MouseDownBackColor = ButtonDownColor;
            return b;
        }

        Row NewRow(Row source)
        {
            Row r = new Row();

            r.NameBox = new TextBox();
            r.NameBox.BorderStyle = BorderStyle.None;
            r.NameBox.BackColor = NoteColor;
            r.NameBox.ForeColor = InkColor;
            r.NameBox.Font = new Font("Segoe UI", 10f);
            r.NameBox.MaxLength = 40;
            r.NameBox.TextChanged += delegate(object s, EventArgs e) { nameDirty = true; nameChangedAt = Environment.TickCount; };

            r.TimeLabel = new Label();
            r.TimeLabel.AutoSize = false;
            r.TimeLabel.BackColor = Color.Transparent;
            r.TimeLabel.ForeColor = SubColor;
            r.TimeLabel.Font = new Font("Segoe UI Semibold", 11f);
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

            BuildRowStrip(r);
            Controls.Add(r.NameBox);
            Controls.Add(r.TimeLabel);

            try { SendMessage(r.NameBox.Handle, EM_SETCUEBANNER, (IntPtr)1, "Model"); }
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
            r.NameBox.ContextMenuStrip = r.Strip;
            r.TimeLabel.ContextMenuStrip = r.Strip;
            r.TimeLabel.MouseUp += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                    r.Strip.Show(Cursor.Position);
            };
            tip.SetToolTip(r.TimeLabel, "Klik untuk atur reset");
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
            Controls.Remove(r.NameBox);
            Controls.Remove(r.TimeLabel);
            if (r.Strip != null) r.Strip.Dispose();
            rows.Remove(r);
            LayoutAll();
            SaveSettings();
        }

        void LayoutAll()
        {
            if (titleLabel == null) return;
            int pad = S(12);
            int w = ClientSize.Width;

            titleLabel.SetBounds(pad, S(7), w - pad * 2 - S(26), S(24));
            closeBtn.SetBounds(w - pad - S(22), S(8), S(22), S(22));

            int rowH = S(30);
            int nameW = S(130);
            int y = S(40);
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].NameBox.SetBounds(pad, y + S(3), nameW, S(24));
                rows[i].TimeLabel.SetBounds(pad + nameW + S(8), y, w - (pad + nameW + S(8)) - pad, rowH);
                y += rowH;
            }

            int footerY = y + S(6);
            int btnW = S(150);
            addBtn.SetBounds(pad, footerY, btnW, S(26));
            hintLabel.SetBounds(pad + btnW + S(8), footerY, w - (pad + btnW + S(8)) - pad, S(26));
            addBtn.Enabled = rows.Count < MaxRows;

            int newH = footerY + S(26) + S(12);
            int newW = S(330);
            if (ClientSize.Height != newH || ClientSize.Width != newW)
                ClientSize = new Size(newW, newH);
        }

        void RefreshRow(Row r)
        {
            if (r.Ready)
            {
                r.TimeLabel.Text = "SIAP";
                r.TimeLabel.ForeColor = ReadyColor;
                return;
            }
            if (r.Mode == "none")
            {
                r.TimeLabel.Text = "--:--:--";
                r.TimeLabel.ForeColor = SubColor;
                return;
            }
            TimeSpan rem = r.Target - DateTime.Now;
            if (rem <= TimeSpan.Zero)
            {
                r.Ready = true;
                r.TimeLabel.Text = "SIAP";
                r.TimeLabel.ForeColor = ReadyColor;
                SaveSettings();
                return;
            }
            r.TimeLabel.Text = FormatRemaining(rem);
            r.TimeLabel.ForeColor = InkColor;
        }

        void OnTick(object s, EventArgs e)
        {
            foreach (Row r in rows)
                RefreshRow(r);

            if (nameDirty && Environment.TickCount - nameChangedAt > 3000)
            {
                nameDirty = false;
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

            StringBuilder sb = new StringBuilder();
            sb.Append("; Limit LLM settings - ubah lewat klik kanan pada baris\r\n");
            sb.Append("topmost=").Append(topMostOn ? "1" : "0").Append("\r\n");
            sb.Append("x=").Append(Location.X.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            sb.Append("y=").Append(Location.Y.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
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
