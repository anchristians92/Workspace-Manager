using System.Drawing.Drawing2D;

namespace WorkspaceManager.Addon.Vault;

// The hits of a search as a list of cards: a round badge with the first letter, the name, and below it user and folder.
// Drawn here instead of with a standard list so it follows the app's colors, scales with the screen and scrolls smoothly.
internal sealed class ResultList : Control
{
    static readonly Color[] Badges =
    [
        Color.FromArgb(67, 97, 238), Color.FromArgb(14, 165, 133), Color.FromArgb(217, 119, 6),
        Color.FromArgb(190, 70, 160), Color.FromArgb(37, 132, 214), Color.FromArgb(120, 100, 220),
    ];

    readonly List<VaultEntry> entries = [];
    int selected = -1, hover = -1, offset;
    bool dragging; int dragFrom, dragOffset;

    // What is shown while there are no hits.
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string EmptyText { get => emptyText; set { emptyText = value; Invalidate(); } }
    string emptyText = "";

    public event EventHandler? SelectedIndexChanged;

    public ResultList()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
        TabStop = true; Tag = "list"; Cursor = Cursors.Hand;
    }

    public int Count => entries.Count;
    public VaultEntry? Selected => selected >= 0 && selected < entries.Count ? entries[selected] : null;
    public VaultEntry EntryAt(int index) => entries[index];

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => selected;
        set
        {
            var next = entries.Count == 0 ? -1 : Math.Clamp(value, 0, entries.Count - 1);
            if (next == selected) return;
            selected = next; EnsureVisible(); Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetEntries(IReadOnlyList<VaultEntry> found)
    {
        entries.Clear(); entries.AddRange(found);
        offset = 0; hover = -1; selected = -1;
        SelectedIndex = found.Count > 0 ? 0 : -1;
        Invalidate();
    }

    float Zoom => DeviceDpi / 96f;
    int Row => (int)(64 * Zoom);
    int Pad => (int)(8 * Zoom);
    int Content => entries.Count * Row + 2 * Pad;
    int MaxOffset => Math.Max(0, Content - Height);
    bool Scrolls => Content > Height;

    // "Root/Firma/Support/Clients/" becomes "Support › Clients".
    internal static string ShortPath(string path)
    {
        var parts = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parts.Count > 0 && parts[0].Equals("Root", StringComparison.OrdinalIgnoreCase)) parts.RemoveAt(0);
        return string.Join("  ›  ", parts.TakeLast(2));
    }

    int IndexAt(Point point)
    {
        var index = (point.Y + offset - Pad) / Row;
        return point.Y + offset >= Pad && index >= 0 && index < entries.Count ? index : -1;
    }

    void EnsureVisible()
    {
        if (selected < 0) return;
        var top = Pad + selected * Row; var bottom = top + Row;
        if (top < offset + Pad) offset = Math.Max(0, top - Pad);
        else if (bottom > offset + Height - Pad) offset = Math.Min(MaxOffset, bottom - Height + Pad);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(Parent?.BackColor ?? AppTheme.Surface);
        var frame = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1); var radius = 12 * Zoom;
        using (var path = Shapes.Rounded(frame, radius))
        {
            using var fill = new SolidBrush(AppTheme.Input); g.FillPath(fill, path);
            using var line = new Pen(AppTheme.Border); g.DrawPath(line, path);
            g.SetClip(path);
        }

        if (entries.Count == 0)
        {
            TextRenderer.DrawText(g, emptyText, Font, new Rectangle(Pad * 2, 0, Width - Pad * 4, Height), AppTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        var scrollWidth = Scrolls ? (int)(12 * Zoom) : 0;
        using var bold = new Font("Segoe UI", 10.5f, FontStyle.Bold); using var small = new Font("Segoe UI", 9f); using var letter = new Font("Segoe UI", 11f, FontStyle.Bold);
        var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        for (var i = 0; i < entries.Count; i++)
        {
            var top = Pad + i * Row - offset;
            if (top + Row < 0 || top > Height) continue;
            var entry = entries[i];
            var card = new RectangleF(Pad, top, Width - 2 * Pad - scrollWidth, Row - 4 * Zoom);
            var active = i == selected;
            if (active || i == hover)
            {
                using var path = Shapes.Rounded(card, 10 * Zoom);
                using var fill = new SolidBrush(active ? AppTheme.AccentSoft : AppTheme.Hover); g.FillPath(fill, path);
                if (active) { using var edge = new Pen(AppTheme.Accent, Math.Max(1, Zoom)); g.DrawPath(edge, path); }
            }

            var badge = (int)(40 * Zoom);
            var circle = new Rectangle((int)card.X + (int)(12 * Zoom), (int)(card.Y + (card.Height - badge) / 2), badge, badge);
            using (var color = new SolidBrush(Badges[(int)((uint)StringComparer.OrdinalIgnoreCase.GetHashCode(entry.Name) % Badges.Length)])) g.FillEllipse(color, circle);
            var initial = entry.Name.Trim().Length > 0 ? char.ToUpperInvariant(entry.Name.Trim()[0]).ToString() : "?";
            TextRenderer.DrawText(g, initial, letter, circle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            var textX = circle.Right + (int)(14 * Zoom);
            var textWidth = (int)card.Right - textX - (int)(12 * Zoom);
            var half = (int)(card.Height / 2);
            TextRenderer.DrawText(g, entry.Name, bold, new Rectangle(textX, (int)card.Y + (int)(6 * Zoom), textWidth, half - (int)(4 * Zoom)), active ? AppTheme.AccentText : AppTheme.Ink, flags);
            var user = entry.Username.Length > 0 ? entry.Username : "kein Benutzername";
            var where = ShortPath(entry.Path);
            var detail = where.Length > 0 ? $"{user}     {where}" : user;
            TextRenderer.DrawText(g, detail, small, new Rectangle(textX, (int)card.Y + half, textWidth, half - (int)(6 * Zoom)), AppTheme.Muted, flags);
        }

        if (Scrolls)
        {
            var track = Height - 2 * Pad; var thumb = Math.Max((int)(36 * Zoom), (int)((long)track * Height / Content));
            var y = Pad + (MaxOffset == 0 ? 0 : (int)((long)(track - thumb) * offset / MaxOffset));
            using var path = Shapes.Rounded(new RectangleF(Width - 9 * Zoom, y, 5 * Zoom, thumb), 2.5f * Zoom);
            using var brush = new SolidBrush(dragging ? AppTheme.Muted : AppTheme.Border); g.FillPath(brush, path);
        }
    }

    Rectangle ThumbArea()
    {
        var track = Height - 2 * Pad; var thumb = Math.Max((int)(36 * Zoom), (int)((long)track * Height / Content));
        var y = Pad + (MaxOffset == 0 ? 0 : (int)((long)(track - thumb) * offset / MaxOffset));
        return new Rectangle(Width - (int)(16 * Zoom), y, (int)(16 * Zoom), thumb);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging)
        {
            var track = Height - 2 * Pad - ThumbArea().Height;
            if (track > 0) { offset = Math.Clamp(dragOffset + (int)((long)(e.Y - dragFrom) * MaxOffset / track), 0, MaxOffset); Invalidate(); }
            return;
        }
        var index = IndexAt(e.Location);
        if (index != hover) { hover = index; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (hover != -1) { hover = -1; Invalidate(); } }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus();
        if (Scrolls && e.Button == MouseButtons.Left && ThumbArea().Contains(e.Location)) { dragging = true; dragFrom = e.Y; dragOffset = offset; Capture = true; Invalidate(); return; }
        var index = IndexAt(e.Location);
        if (index >= 0) SelectedIndex = index;
    }

    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (dragging) { dragging = false; Capture = false; Invalidate(); } }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        offset = Math.Clamp(offset - e.Delta * Row / 120, 0, MaxOffset); Invalidate();
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); offset = Math.Clamp(offset, 0, MaxOffset); }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var page = Math.Max(1, Height / Row - 1);
        switch (e.KeyCode)
        {
            case Keys.Down: SelectedIndex = selected + 1; e.Handled = true; break;
            case Keys.Up: SelectedIndex = selected - 1; e.Handled = true; break;
            case Keys.PageDown: SelectedIndex = selected + page; e.Handled = true; break;
            case Keys.PageUp: SelectedIndex = selected - page; e.Handled = true; break;
            case Keys.Home: SelectedIndex = 0; e.Handled = true; break;
            case Keys.End: SelectedIndex = entries.Count - 1; e.Handled = true; break;
        }
    }
}

// The website of the chosen entry as a link: a click opens it in the standard browser.
internal sealed class WebLink : Control
{
    string address = "";

    public WebLink()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.StandardClick, true);
        Cursor = Cursors.Hand; Tag = "link"; TabStop = false; Visible = false;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Address { get => address; set { address = value; Visible = value.Length > 0; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Parent?.BackColor ?? AppTheme.Surface);
        if (address.Length == 0) return;
        using var icon = new Font(Branding.IconFont, 11f); using var text = new Font("Segoe UI", 10f, FontStyle.Underline);
        var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        var gap = (int)(10 * DeviceDpi / 96f); var iconWidth = (int)(22 * DeviceDpi / 96f);
        TextRenderer.DrawText(g, "", icon, new Rectangle(0, 0, iconWidth, Height), AppTheme.Muted, flags);
        TextRenderer.DrawText(g, address, text, new Rectangle(iconWidth + gap / 2, 0, Width - iconWidth - gap / 2, Height), AppTheme.AccentText, flags | TextFormatFlags.EndEllipsis);
    }
}
