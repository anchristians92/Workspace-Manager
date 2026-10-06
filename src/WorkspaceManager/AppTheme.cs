using Microsoft.Win32;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace WorkspaceManager;

internal static class AppTheme
{
    internal static bool? PreviewDark;
    static bool? nativeDark;
    public static string Detection { get; private set; } = "Not read";
    // Manual override: "System" (follow Windows), "Light" or "Dark".
    public static string Mode { get; set; } = "System";
    public static bool Dark { get; private set; }
    static Color Pick(Color dark, Color light, Color highContrast) => SystemInformation.HighContrast ? highContrast : Dark ? dark : light;
    public static Color Background => Pick(Color.FromArgb(15, 18, 26), Color.FromArgb(242, 244, 248), SystemColors.Control);
    public static Color Surface => Pick(Color.FromArgb(24, 29, 40), Color.White, SystemColors.Control);
    public static Color Input => Pick(Color.FromArgb(32, 38, 52), Color.White, SystemColors.Window);
    public static Color Hover => Pick(Color.FromArgb(43, 51, 69), Color.FromArgb(235, 239, 246), SystemColors.Control);
    public static Color Ink => Pick(Color.FromArgb(233, 237, 245), Color.FromArgb(22, 30, 46), SystemColors.ControlText);
    public static Color Muted => Pick(Color.FromArgb(148, 160, 181), Color.FromArgb(92, 104, 125), SystemColors.ControlText);
    public static Color Accent => Pick(Color.FromArgb(67, 97, 238), Color.FromArgb(67, 97, 238), SystemColors.Highlight);
    public static Color AccentHover => Pick(Color.FromArgb(52, 80, 214), Color.FromArgb(52, 80, 214), SystemColors.Highlight);
    public static Color AccentSoft => Pick(Color.FromArgb(33, 44, 86), Color.FromArgb(228, 235, 255), SystemColors.Highlight);
    public static Color AccentText => Pick(Color.FromArgb(150, 172, 255), Color.FromArgb(44, 72, 205), SystemColors.HighlightText);
    public static Color OnAccent => Pick(Color.White, Color.White, SystemColors.HighlightText);
    public static Color Border => Pick(Color.FromArgb(58, 68, 88), Color.FromArgb(204, 212, 225), SystemColors.WindowText);
    public static Color Line => Pick(Color.FromArgb(40, 48, 63), Color.FromArgb(226, 231, 240), SystemColors.WindowText);

    public static bool ReadDark()
    {
        if (SystemInformation.HighContrast) { Detection = "High contrast"; return false; }
        if (PreviewDark.HasValue) return PreviewDark.Value;
        if (Mode == "Light") { Detection = "Manual light"; return false; }
        if (Mode == "Dark") { Detection = "Manual dark"; return true; }
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var raw = key?.GetValue("AppsUseLightTheme");
            Detection = $"AppsUseLightTheme={raw ?? "missing"}; type={raw?.GetType().Name ?? "none"}";
            return raw is int value && value == 0;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException) { Detection = e.GetType().Name; return false; }
    }
    public static void Refresh()
    {
        Dark = ReadDark();
        if (nativeDark == Dark) return;
        nativeDark = Dark;
        // Native controls/dialogs plus our custom palette. No Windows preference is modified.
#pragma warning disable WFO5001
        Application.SetColorMode(Dark ? SystemColorMode.Dark : SystemColorMode.Classic);
#pragma warning restore WFO5001
    }
    public static void Bind(Form form)
    {
        Refresh(); Apply(form);
        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        var previous = (Dark, SystemInformation.HighContrast);
        timer.Tick += (_, _) =>
        {
            Refresh(); var current = (Dark, SystemInformation.HighContrast);
            if (current == previous && form.BackColor.ToArgb() == Background.ToArgb()) return;
            previous = current; Apply(form);
        };
        form.HandleCreated += (_, _) => Apply(form);
        // Apply again after native form initialization has completed.
        form.Shown += (_, _) => form.BeginInvoke(() => { Refresh(); Apply(form); });
        form.Disposed += (_, _) => timer.Dispose();
        timer.Start();
    }
    public static void Apply(Control control)
    {
        if (control.IsDisposed) return;
        var role = control.Tag as string;
        control.ForeColor = role == "muted" ? Muted : Ink;
        control.BackColor = control is Form ? Background : role == "surface" ? Surface : control.Parent?.BackColor ?? Background;
        if (control is TextBoxBase or ComboBox) { control.BackColor = Input; control.ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : Ink; }
        if (role == "selectable") { control.BackColor = control.Parent?.BackColor ?? Surface; control.ForeColor = Muted; }
        if (control is Button button)
        {
            var primary = role == "primary";
            button.BackColor = primary ? Accent : Input; button.ForeColor = primary ? OnAccent : Ink;
            button.FlatAppearance.BorderColor = primary ? Accent : Border;
            button.FlatAppearance.MouseOverBackColor = primary ? Accent : Input;
        }
        foreach (Control child in control.Controls) Apply(child);
        if (control is Form form && form.IsHandleCreated)
        {
            var enabled = Dark ? 1 : 0;
            DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int));
        }
        control.Invalidate();
    }
    public static void ApplyMenu(ContextMenuStrip menu)
    {
        Refresh();
        menu.BackColor = Surface; menu.ForeColor = Ink;
        menu.Renderer = new ToolStripProfessionalRenderer(new MenuColors());
        foreach (ToolStripItem item in menu.Items) { item.BackColor = Surface; item.ForeColor = Ink; }
    }
    sealed class MenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color MenuItemSelected => Input;
        public override Color MenuItemBorder => Border;
        public override Color MenuBorder => Border;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Surface;
        public override Color CheckBackground => Input;
        public override Color CheckSelectedBackground => Input;
    }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}

internal static class Branding
{
    public static readonly string IconFont = new System.Drawing.Text.InstalledFontCollection().Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
    static Stream Open(string name) => typeof(Branding).Assembly.GetManifestResourceStream(name) ?? throw new FileNotFoundException(name);
    public static Icon Window() { using var s = Open("app.ico"); return new Icon(s); }
    public static Icon Tray() { using var s = Open("tray.ico"); return new Icon(s, SystemInformation.SmallIconSize); }
    public static Image Logo() { using var s = Open("logo.png"); using var raw = Image.FromStream(s); return new Bitmap(raw); }
}

internal static class Shapes
{
    public static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var path = new GraphicsPath();
        if (d <= 0) { path.AddRectangle(r); return path; }
        path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
}

// Rounded card with a hairline border.
internal sealed class CardPanel : TableLayoutPanel
{
    public CardPanel() { DoubleBuffered = true; ResizeRedraw = true; }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Parent?.BackColor ?? AppTheme.Background); g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Shapes.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), 12 * DeviceDpi / 96f);
        using var fill = new SolidBrush(AppTheme.Surface); using var pen = new Pen(AppTheme.Line);
        g.FillPath(fill, path); g.DrawPath(pen, path);
    }
}

// Sidebar with a hairline on its right edge.
internal sealed class SidePanel : TableLayoutPanel
{
    public SidePanel() { DoubleBuffered = true; ResizeRedraw = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(AppTheme.Line); e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);
    }
}

// List row with a hairline underneath.
internal sealed class RowPanel : TableLayoutPanel
{
    public RowPanel() { DoubleBuffered = true; ResizeRedraw = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(AppTheme.Line); e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
    }
}

// Draw explicitly so changing native color mode cannot leave cached button text colors behind.
internal sealed class ThemeButton : Button
{
    bool hover, pressed;
    // Optional icon (Segoe Fluent Icons code point). With IconOnly the text is hidden and the icon is centered.
    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string? Glyph { get; set; }
    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IconOnly { get; set; }
    public ThemeButton() => SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs mevent) { pressed = true; Invalidate(); base.OnMouseDown(mevent); }
    protected override void OnMouseUp(MouseEventArgs mevent) { pressed = false; Invalidate(); base.OnMouseUp(mevent); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; var role = Tag as string; var back = Parent?.BackColor ?? AppTheme.Background; var active = hover || pressed;
        g.Clear(back); g.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill, line, ink;
        switch (role)
        {
            case "primary": fill = line = active ? AppTheme.AccentHover : AppTheme.Accent; ink = AppTheme.OnAccent; break;
            case "navActive": fill = line = AppTheme.AccentSoft; ink = AppTheme.AccentText; break;
            case "nav": fill = line = active ? AppTheme.Hover : back; ink = active ? AppTheme.Ink : AppTheme.Muted; break;
            default: fill = active ? AppTheme.Hover : AppTheme.Input; line = AppTheme.Border; ink = AppTheme.Ink; break;
        }
        if (!Enabled) ink = AppTheme.Muted;
        var scale = DeviceDpi / 96f;
        using (var path = Shapes.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), 8 * scale))
        {
            using var brush = new SolidBrush(fill); g.FillPath(brush, path);
            if (line != fill) { using var pen = new Pen(line); g.DrawPath(pen, path); }
        }
        var area = Rectangle.FromLTRB(Padding.Left, Padding.Top, Width - Padding.Right, Height - Padding.Bottom);
        if (Glyph is not null)
        {
            using var iconFont = new Font(Branding.IconFont, 12f);
            var box = IconOnly ? new Rectangle(0, 0, Width, Height) : new Rectangle(Padding.Left, 0, (int)(24 * scale), Height);
            TextRenderer.DrawText(g, Glyph, iconFont, box, ink, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            area = Rectangle.FromLTRB(Padding.Left + (int)(36 * scale), Padding.Top, Width - Padding.Right, Height - Padding.Bottom);
        }
        if (!IconOnly)
        {
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | (TextAlign == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, Text, Font, area, ink, flags);
        }
        if (Focused && ShowFocusCues)
        {
            using var path = Shapes.Rounded(new RectangleF(2, 2, Width - 5, Height - 5), 6 * scale); using var pen = new Pen(AppTheme.Accent, 2 * scale);
            g.DrawPath(pen, path);
        }
    }
}

// Rounded text field: borderless TextBox inside a frame that highlights while focused.
internal sealed class InputFrame : Panel
{
    readonly TextBox box;
    public InputFrame(TextBox box)
    {
        this.box = box; DoubleBuffered = true; Padding = new Padding(12, 9, 12, 9); Cursor = Cursors.IBeam;
        box.BorderStyle = BorderStyle.None; box.Dock = DockStyle.Fill; Controls.Add(box);
        Height = box.PreferredHeight + Padding.Vertical;
        box.Enter += (_, _) => Invalidate(); box.Leave += (_, _) => Invalidate();
        Click += (_, _) => box.Focus();
    }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Height = box.PreferredHeight + Padding.Vertical; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Parent?.BackColor ?? AppTheme.Background); g.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = DeviceDpi / 96f; var focused = box.Focused;
        using var path = Shapes.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), 8 * scale);
        using var fill = new SolidBrush(AppTheme.Input); using var pen = new Pen(focused ? AppTheme.Accent : AppTheme.Border, focused ? 1.5f * scale : 1f);
        g.FillPath(fill, path); g.DrawPath(pen, path);
    }
}

// Scroll area that repaints without flicker and moves a sensible distance per wheel notch.
internal sealed class ScrollPanel : Panel
{
    public ScrollPanel() { DoubleBuffered = true; AutoScroll = true; }
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x02000000; return cp; } } // WS_EX_COMPOSITED
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Only the step per wheel notch is changed. LargeChange is the page size of the scroll bar and has to stay the
        // height of the visible area, otherwise the page can be scrolled far past its end.
        VerticalScroll.SmallChange = Math.Max(1, 36 * DeviceDpi / 96);
    }
}

internal sealed class SmoothStack : TableLayoutPanel
{
    public SmoothStack() => DoubleBuffered = true;
}

// Multi-line editor that lets the page scroll unless it has been clicked into.
internal sealed class WheelPassTextBox : TextBox
{
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!Focused || ScrollBars == ScrollBars.None)
        {
            var parent = Parent;
            while (parent is not null and not ScrollableControl { AutoScroll: true }) parent = parent.Parent;
            if (parent is ScrollableControl scroll)
            {
                scroll.AutoScrollPosition = new Point(-scroll.AutoScrollPosition.X, -scroll.AutoScrollPosition.Y - e.Delta * scroll.VerticalScroll.SmallChange * 3 / 120);
                if (e is HandledMouseEventArgs handled) handled.Handled = true;
                return;
            }
        }
        base.OnMouseWheel(e);
    }
}

// Drop-down list drawn in the app style (rounded field, chevron, themed menu).
internal sealed class ThemeDropdown : Control
{
    int selected = -1; bool hover, open;
    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public List<string> Items { get; } = [];
    public event EventHandler? SelectedIndexChanged;
    public ThemeDropdown()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.StandardClick, true);
        TabStop = true; Cursor = Cursors.Hand; Height = 40;
    }
    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => selected;
        set
        {
            value = Items.Count == 0 ? -1 : Math.Clamp(value, -1, Items.Count - 1);
            if (selected == value) return;
            selected = value; Invalidate(); SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    void FitHeight() => Height = TextRenderer.MeasureText("Ag", Font).Height + 20 * DeviceDpi / 96;
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); FitHeight(); }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); FitHeight(); }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down || base.IsInputKey(keyData);
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnMouseDown(MouseEventArgs e) { Focus(); if (e.Button == MouseButtons.Left) Open(); base.OnMouseDown(e); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter or Keys.F4) { Open(); e.Handled = true; }
        else if (e.KeyCode == Keys.Down) { SelectedIndex++; e.Handled = true; }
        else if (e.KeyCode == Keys.Up) { SelectedIndex--; e.Handled = true; }
        base.OnKeyDown(e);
    }
    void Open()
    {
        if (open || Items.Count == 0) return;
        var menu = new ContextMenuStrip { Font = Font, ShowImageMargin = false, MinimumSize = new Size(Width, 0) };
        for (var i = 0; i < Items.Count; i++)
        {
            var index = i; var item = new ToolStripMenuItem(Items[i]) { Padding = new Padding(4, 6, 4, 6) };
            if (i == selected) item.Font = new Font(Font, FontStyle.Bold);
            item.Click += (_, _) => SelectedIndex = index; menu.Items.Add(item);
        }
        AppTheme.ApplyMenu(menu); open = true; Invalidate();
        menu.Closed += (_, _) => { open = false; Invalidate(); if (!IsDisposed) BeginInvoke(menu.Dispose); };
        menu.Show(this, new Point(0, Height + 4));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; var scale = DeviceDpi / 96f; var active = Focused || open;
        g.Clear(Parent?.BackColor ?? AppTheme.Background); g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Shapes.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), 8 * scale))
        {
            using var fill = new SolidBrush(AppTheme.Input);
            using var pen = new Pen(active ? AppTheme.Accent : hover ? AppTheme.Muted : AppTheme.Border, active ? 1.5f * scale : 1f);
            g.FillPath(fill, path); g.DrawPath(pen, path);
        }
        var text = selected >= 0 ? Items[selected] : "";
        var area = new Rectangle((int)(12 * scale), 0, Width - (int)(48 * scale), Height);
        TextRenderer.DrawText(g, text, Font, area, AppTheme.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        float cx = Width - 22 * scale, cy = Height / 2f;
        using var chevron = new Pen(AppTheme.Muted, 1.8f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(chevron, [new PointF(cx - 5 * scale, cy - 2 * scale), new PointF(cx, cy + 3 * scale), new PointF(cx + 5 * scale, cy - 2 * scale)]);
    }
}
