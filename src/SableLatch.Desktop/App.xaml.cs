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
        // Legacy embedded session helpers are no longer a supported launch path.
        if (e.Args.FirstOrDefault() == "--rdp-session") { Shutdown(1); return; }
        // This small, mostly static popup does not need a resident Direct3D pipeline.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        Resources[SystemParameters.VerticalScrollBarWidthKey] = 6d;
        Resources[SystemParameters.HorizontalScrollBarHeightKey] = 6d;
        Localizer.LoadEnglish();
        ScrollAffordances.Register();
#if DEBUG
        PreviewMode = e.Args.Contains("--preview");
        CaptureProtection.PreviewOpaque = PreviewMode && e.Args.Contains("--preview-opaque");
#endif
        CaptureProtection.Register();
        var dataIndex = Array.IndexOf(e.Args, "--data-dir");
        DataDirectory = dataIndex >= 0 && dataIndex + 1 < e.Args.Length ? Path.GetFullPath(e.Args[dataIndex + 1]) : PortableData.DefaultDirectory(AppContext.BaseDirectory);
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DataDirectory.ToUpperInvariant())))[..24];
        instance = new Mutex(true, "Local\\SableLatch-" + id, out var created);
        if (!created) { MessageBox.Show("Sable is already running for this vault. Open it from the tray.", "Sable"); Shutdown(); return; }
        try
        {
            Directory.CreateDirectory(DataDirectory);
            vaultLease = new FileStream(Path.Combine(DataDirectory, ".session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException) { MessageBox.Show("This vault folder is already open or is not writable. Close its other instance, or extract Sable into a writable local folder.", "Sable"); Shutdown(); return; }
        catch (UnauthorizedAccessException) { MessageBox.Show("Sable needs a writable folder for its portable vault. Extract it into a folder you own.", "Sable"); Shutdown(); return; }
        DispatcherUnhandledException += (_, args) => { MessageBox.Show("Sable could not complete that action. Your encrypted vault has been preserved. Restart the app if needed.", "Sable"); args.Handled = true; };
        try { NativeRdpSettings.Cleanup(NativeRdpLauncher.DirectoryFor(DataDirectory)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { /* Retry on the next launch; never open the vault to clean temporary files. */ }
        var window = new MainWindow(); MainWindow = window;
        if (PreviewMode) window.Loaded += (_, _) => window.ShowPreview();
        if (e.Args.Contains("--tray") && !PreviewMode)
            new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
        else window.Show();
    }
    protected override void OnExit(ExitEventArgs e) {
        NativeRdpSettings.ReleaseActive();
        if (DataDirectory.Length > 0) try { NativeRdpSettings.Cleanup(NativeRdpLauncher.DirectoryFor(DataDirectory)); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        vaultLease?.Dispose(); instance?.Dispose(); base.OnExit(e); }
}
