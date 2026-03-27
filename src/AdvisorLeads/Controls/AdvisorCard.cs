using AdvisorLeads.Models;

namespace AdvisorLeads.Controls;

/// <summary>
/// A compact card control that displays key advisor facts.
/// Used in the center panel grid — click to view full details.
/// </summary>
public class AdvisorCard : Panel
{
    private const int CardMargin = 6;
    private const int CardPadding = 12;
    private const int AccentBarWidth = 4;

    private readonly Label _lblName;
    private readonly Label _lblCrd;
    private readonly Label _lblFirm;
    private readonly Label _lblLocation;
    private readonly Label _lblType;
    private readonly Label _lblStatus;

    private Advisor? _advisor;
    private bool _isSelected;

    public Advisor? Advisor => _advisor;
    public event EventHandler? CardClicked;

    private static readonly Color SelectedBorder = Color.FromArgb(0, 120, 212);
    private static readonly Color HoverBg = Color.FromArgb(245, 247, 250);
    private static readonly Color DefaultBg = Color.White;
    private static readonly Color FavoriteBg = Color.FromArgb(255, 252, 220);
    private static readonly Color ExcludedBg = Color.FromArgb(248, 248, 248);
    private static readonly Color DisclosureAccent = Color.FromArgb(230, 126, 34);
    private static readonly Color ActiveIarAccent = Color.FromArgb(39, 174, 96);
    private static readonly Color ImportedAccent = Color.FromArgb(100, 60, 160);
    private static readonly Color FavoriteAccent = Color.FromArgb(255, 180, 0);
    private static readonly Color DefaultAccent = Color.FromArgb(180, 190, 200);
    private static readonly Color ExcludedText = Color.FromArgb(160, 160, 160);
    private static readonly Color SubtleText = Color.FromArgb(120, 130, 140);

    public AdvisorCard()
    {
        this.Margin = new Padding(CardMargin);
        this.BackColor = DefaultBg;
        this.Cursor = Cursors.Hand;
        this.DoubleBuffered = true;

        _lblName = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 40, 50),
            Location = new Point(CardPadding + AccentBarWidth + 4, CardPadding),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _lblCrd = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 8f),
            ForeColor = SubtleText,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _lblFirm = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(50, 60, 70),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _lblLocation = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = SubtleText,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _lblType = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(0, 150, 136),
            Padding = new Padding(5, 1, 5, 1),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        _lblStatus = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(76, 175, 80),
            Padding = new Padding(5, 1, 5, 1),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        this.Controls.AddRange(new Control[] { _lblName, _lblCrd, _lblFirm, _lblLocation, _lblType, _lblStatus });

        // Wire click on all children
        foreach (Control c in this.Controls)
        {
            c.Click += (_, _) => OnCardClicked();
            c.MouseEnter += (_, _) => OnMouseHover(true);
            c.MouseLeave += (_, _) => OnMouseHover(false);
            c.Cursor = Cursors.Hand;
        }
        this.Click += (_, _) => OnCardClicked();
        this.MouseEnter += (_, _) => OnMouseHover(true);
        this.MouseLeave += (_, _) => OnMouseHover(false);

        this.Resize += (_, _) => LayoutLabels();
    }

    public void SetAdvisor(Advisor advisor)
    {
        _advisor = advisor;
        _isSelected = false;

        _lblName.Text = advisor.FullName;
        _lblCrd.Text = !string.IsNullOrEmpty(advisor.CrdNumber) ? $"CRD# {advisor.CrdNumber}" : "";

        _lblFirm.Text = advisor.CurrentFirmName ?? "";

        var locationParts = new List<string>();
        if (!string.IsNullOrEmpty(advisor.City)) locationParts.Add(advisor.City);
        if (!string.IsNullOrEmpty(advisor.State)) locationParts.Add(advisor.State);
        if (advisor.YearsOfExperience.HasValue && advisor.YearsOfExperience > 0)
            locationParts.Add($"{advisor.YearsOfExperience} yrs exp.");
        _lblLocation.Text = string.Join(" · ", locationParts);

        // Type badge
        var type = advisor.RecordType ?? "";
        _lblType.Text = type switch
        {
            "Investment Advisor Representative" => "IAR",
            "Registered Representative" => "RR",
            _ => type.Length > 12 ? type[..12] : type
        };
        _lblType.Visible = !string.IsNullOrEmpty(type);

        // Status badge
        var status = advisor.RegistrationStatus ?? "";
        _lblStatus.Text = status;
        _lblStatus.Visible = !string.IsNullOrEmpty(status);
        _lblStatus.BackColor = status == "Active"
            ? Color.FromArgb(76, 175, 80)
            : Color.FromArgb(158, 158, 158);

        // Colors based on advisor state
        if (advisor.IsExcluded)
        {
            _lblName.ForeColor = ExcludedText;
            _lblName.Font = new Font(_lblName.Font, FontStyle.Strikeout | FontStyle.Bold);
        }
        else
        {
            _lblName.ForeColor = advisor.IsImportedToCrm
                ? ImportedAccent
                : Color.FromArgb(30, 40, 50);
            _lblName.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        }

        LayoutLabels();
        Invalidate();
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        Invalidate();
    }

    private void LayoutLabels()
    {
        int left = CardPadding + AccentBarWidth + 4;
        int usable = Math.Max(50, Width - left - CardPadding);
        int y = CardPadding;

        _lblName.SetBounds(left, y, usable, 22);
        y += 22;

        _lblCrd.SetBounds(left, y, usable, 16);
        y += 18;

        _lblFirm.SetBounds(left, y, usable, 18);
        y += 20;

        _lblLocation.SetBounds(left, y, usable, 16);

        // Badges at bottom
        int badgeY = Math.Max(y + 22, Height - CardPadding - 18);
        _lblType.Location = new Point(left, badgeY);
        _lblStatus.Location = new Point(left + _lblType.Width + 6, badgeY);
    }

    private Color GetAccentColor()
    {
        if (_advisor == null) return DefaultAccent;
        if (_advisor.IsExcluded) return ExcludedText;
        if (_advisor.IsFavorited) return FavoriteAccent;
        if (_advisor.HasDisclosures) return DisclosureAccent;
        if (!_advisor.IsImportedToCrm
            && _advisor.RegistrationStatus == "Active"
            && _advisor.RecordType == "Investment Advisor Representative")
            return ActiveIarAccent;
        if (_advisor.IsImportedToCrm) return ImportedAccent;
        return DefaultAccent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;

        // Card background
        var bg = _advisor?.IsExcluded == true ? ExcludedBg
               : _advisor?.IsFavorited == true ? FavoriteBg
               : DefaultBg;
        using var bgBrush = new SolidBrush(bg);
        g.FillRectangle(bgBrush, ClientRectangle);

        // Left accent bar
        var accent = GetAccentColor();
        using var accentBrush = new SolidBrush(accent);
        g.FillRectangle(accentBrush, 0, 0, AccentBarWidth, Height);

        // Border
        var borderColor = _isSelected ? SelectedBorder : Color.FromArgb(220, 225, 230);
        var borderWidth = _isSelected ? 2 : 1;
        using var pen = new Pen(borderColor, borderWidth);
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        g.DrawRectangle(pen, rect);

        // Favorite star indicator (top-right corner)
        if (_advisor != null && _advisor.IsFavorited)
        {
            using var starFont = new Font("Segoe UI", 12f, FontStyle.Bold);
            using var starBrush = new SolidBrush(FavoriteAccent);
            var starSize = g.MeasureString("★", starFont);
            g.DrawString("★", starFont, starBrush,
                Width - (int)starSize.Width - 6, CardPadding - 4);
        }

        // Disclosure count indicator (top-right corner, offset when star present)
        if (_advisor != null && _advisor.HasDisclosures && _advisor.DisclosureCount > 0)
        {
            var text = _advisor.DisclosureCount.ToString();
            using var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            var size = g.MeasureString(text, font);
            int offset = _advisor.IsFavorited ? 28 : 0;
            int cx = Width - (int)size.Width - 12 - offset;
            int cy = CardPadding;
            using var discBg = new SolidBrush(Color.FromArgb(230, 126, 34));
            g.FillEllipse(discBg, cx - 3, cy - 1, size.Width + 6, size.Height + 2);
            using var discFg = new SolidBrush(Color.White);
            g.DrawString(text, font, discFg, cx, cy);
        }
    }

    private void OnCardClicked()
    {
        CardClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnMouseHover(bool hovering)
    {
        if (_isSelected) return;
        BackColor = hovering
            ? HoverBg
            : (_advisor?.IsFavorited == true ? FavoriteBg
               : _advisor?.IsExcluded == true ? ExcludedBg
               : DefaultBg);
    }
}
