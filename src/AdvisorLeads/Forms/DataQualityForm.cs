using AdvisorLeads.Controls;
using AdvisorLeads.Services;

namespace AdvisorLeads.Forms;

public class DataQualityForm : Form
{
    public DataQualityForm(DataCleaningService cleaningService)
    {
        this.Text = "Data Quality Manager";
        this.Size = new Size(1100, 700);
        this.MinimumSize = new Size(800, 500);
        this.StartPosition = FormStartPosition.CenterParent;
        this.Font = new Font("Segoe UI", 9);
        this.Icon = SystemIcons.Shield;

        var panel = new DataCleaningPanel(cleaningService) { Dock = DockStyle.Fill };
        this.Controls.Add(panel);
    }
}
