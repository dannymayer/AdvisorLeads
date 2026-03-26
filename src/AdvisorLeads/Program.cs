using AdvisorLeads.Forms;
using System.Text;

namespace AdvisorLeads;

static class Program
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AdvisorLeads",
        "startup.log");

    /// <summary>
    ///  The main entry point for the AdvisorLeads application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        Log("Process start");

        Application.ThreadException += (_, args) => LogException("Application.ThreadException", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogException("AppDomain.CurrentDomain.UnhandledException", args.ExceptionObject as Exception);

        try
        {
            ApplicationConfiguration.Initialize();
            Log("ApplicationConfiguration initialized");

            using var mainForm = new MainForm();
            mainForm.Load += (_, _) => Log("MainForm.Load");
            mainForm.Shown += (_, _) => Log($"MainForm.Shown Visible={mainForm.Visible} WindowState={mainForm.WindowState} Bounds={mainForm.Bounds}");
            mainForm.FormClosed += (_, _) => Log("MainForm.FormClosed");

            Log("Application.Run entering");
            Application.Run(mainForm);
            Log("Application.Run exited normally");
        }
        catch (Exception ex)
        {
            LogException("Main catch", ex);
            throw;
        }
    }

    private static void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
        File.AppendAllText(LogPath, line, Encoding.UTF8);
    }

    private static void LogException(string source, Exception? exception)
    {
        if (exception == null)
        {
            Log($"{source}: null exception");
            return;
        }

        Log($"{source}: {exception}");
    }
}