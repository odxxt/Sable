using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace SableLatch.Desktop;

public partial class App : Application
{
    private Mutex? instance;
    private FileStream? vaultLease;
    public static string DataDirectory { get; private set; } = "";
    public static bool PreviewMode { get; private set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.FirstOrDefault() == "--rdp-session")
        {
            // A helper never opens a vault, creates a tray icon or acquires the vault's instance lease.
            if (e.Args.Length != 3 || !System.Text.RegularExpressions.Regex.IsMatch(e.Args[1], "^SableLatch-[A-F0-9]{64}$") || !int.TryParse(e.Args[2], out var parent) || parent <= 0) { Shutdown(1); return; }
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            DispatcherUnhandledException += (_, args) => { args.Handled = true; MessageBox.Show("The Remote Desktop component could not continue. Close this connection and try again.", "SableLatch · Remote Desktop"); Shutdown(1); };
            try { MainWindow = new RdpSessionWindow(e.Args[1], parent); MainWindow.Show(); }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or System.Reflection.TargetInvocationException)
            { MessageBox.Show("Microsoft's Remote Desktop component is unavailable on this Windows installation.", "SableLatch · Remote Desktop"); Shutdown(1); }
            return;
        }
        // This small, mostly static popup does not need a resident Direct3D pipeline.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        Resources[SystemParameters.VerticalScrollBarWidthKey] = 6d;
        Resources[SystemParameters.HorizontalScrollBarHeightKey] = 6d;
        Localizer.LoadEnglish();
        ScrollAffordances.Register();
        CaptureProtection.Register();
#if DEBUG
        PreviewMode = e.Args.Contains("--preview");
#endif
        var dataIndex = Array.IndexOf(e.Args, "--data-dir");
        DataDirectory = dataIndex >= 0 && dataIndex + 1 < e.Args.Length ? Path.GetFullPath(e.Args[dataIndex + 1]) : Path.Combine(AppContext.BaseDirectory, "SableLatch-data");
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DataDirectory.ToUpperInvariant())))[..24];
        instance = new Mutex(true, "Local\\SableLatch-" + id, out var created);
        if (!created) { MessageBox.Show("SableLatch is already running for this vault. Open it from the tray.", "SableLatch"); Shutdown(); return; }
        try
        {
            Directory.CreateDirectory(DataDirectory);
            vaultLease = new FileStream(Path.Combine(DataDirectory, ".session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException) { MessageBox.Show("This vault folder is already open or is not writable. Close its other instance, or extract SableLatch into a writable local folder.", "SableLatch"); Shutdown(); return; }
        catch (UnauthorizedAccessException) { MessageBox.Show("SableLatch needs a writable folder for its portable vault. Extract it into a folder you own.", "SableLatch"); Shutdown(); return; }
        DispatcherUnhandledException += (_, args) => { MessageBox.Show("SableLatch could not complete that action. Your encrypted vault has been preserved. Restart the app if needed.", "SableLatch"); args.Handled = true; };
        var window = new MainWindow(); MainWindow = window;
        if (PreviewMode) window.Loaded += (_, _) => window.ShowPreview();
        if (e.Args.Contains("--tray") && !PreviewMode)
            new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
        else window.Show();
    }
    protected override void OnExit(ExitEventArgs e) { vaultLease?.Dispose(); instance?.Dispose(); base.OnExit(e); }
}
