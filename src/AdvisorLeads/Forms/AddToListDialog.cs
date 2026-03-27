using AdvisorLeads.Models;

namespace AdvisorLeads.Forms;

public class AddToListDialog : Form
{
    private ListBox _lstLists = null!;
    private Button _btnNew = null!;
    private Button _btnAdd = null!;
    private Button _btnCancel = null!;
    private Label _lblAdvisor = null!;

    private List<AdvisorList> _lists;
    public AdvisorList? SelectedList { get; private set; }
    public bool CreatedNewList { get; private set; }
    public string? NewListName { get; private set; }

    public AddToListDialog(List<AdvisorList> existingLists, string advisorName)
    {
        _lists = existingLists;
        Text = "Add to List";
        Size = new Size(360, 340);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };

        _lblAdvisor = new Label
        {
            Text = $"Adding: {advisorName}",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Dock = DockStyle.Fill,
            Height = 24,
            AutoSize = false
        };
        layout.Controls.Add(_lblAdvisor, 0, 0);

        layout.Controls.Add(new Label { Text = "Select a list:", Dock = DockStyle.Fill, Height = 20, AutoSize = false }, 0, 1);

        _lstLists = new ListBox { Dock = DockStyle.Fill, Height = 160, SelectionMode = SelectionMode.One };
        foreach (var l in _lists)
            _lstLists.Items.Add($"{l.Name} ({l.MemberCount} members)");
        if (_lstLists.Items.Count > 0) _lstLists.SelectedIndex = 0;
        _lstLists.DoubleClick += (_, _) => OnAdd(null, EventArgs.Empty);
        layout.Controls.Add(_lstLists, 0, 2);

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 6, 0, 0)
        };

        _btnNew = new Button { Text = "+ New List", Width = 90, Height = 28 };
        _btnNew.Click += OnNewList;
        _btnCancel = new Button { Text = "Cancel", Width = 70, Height = 28 };
        _btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _btnAdd = new Button
        {
            Text = "Add to List",
            Width = 90,
            Height = 28,
            BackColor = Color.FromArgb(70, 100, 180),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnAdd.FlatAppearance.BorderSize = 0;
        _btnAdd.Click += OnAdd;
        _btnAdd.Enabled = _lstLists.Items.Count > 0;

        btnPanel.Controls.AddRange(new Control[] { _btnNew, _btnAdd, _btnCancel });
        layout.Controls.Add(btnPanel, 0, 3);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Controls.Add(layout);
        CancelButton = _btnCancel;
    }

    private void OnNewList(object? sender, EventArgs e)
    {
        using var dlg = new CreateListDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            CreatedNewList = true;
            NewListName = dlg.ListName;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private void OnAdd(object? sender, EventArgs e)
    {
        if (_lstLists.SelectedIndex < 0 || _lstLists.SelectedIndex >= _lists.Count)
        {
            MessageBox.Show("Please select a list.", "No List Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        SelectedList = _lists[_lstLists.SelectedIndex];
        DialogResult = DialogResult.OK;
        Close();
    }
}
