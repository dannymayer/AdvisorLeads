using AdvisorLeads.Forms;

namespace AdvisorLeads;

static class Program
{
    /// <summary>
    ///  The main entry point for the AdvisorLeads application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}