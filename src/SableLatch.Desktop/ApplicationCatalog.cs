using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace SableLatch.Desktop;

internal sealed record InstalledApplication(string Name, string Path, string Arguments = "", string Directory = "");

internal static class ApplicationCatalog
{
    // Read only registered paths and Start-menu shortcuts, on demand. No recursive disk scan or resident cache.
    public static Task<IReadOnlyList<InstalledApplication>> ReadAsync()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<InstalledApplication>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(Read()); } catch { completion.SetResult([]); }
        }) { IsBackground = true, Name = "SableLatch app picker" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
    private static IReadOnlyList<InstalledApplication> Read()
    {
        var found = new Dictionary<string, InstalledApplication>(StringComparer.OrdinalIgnoreCase);
        void Add(string name, string path, string arguments = "", string directory = "")
        {
            path = Environment.ExpandEnvironmentVariables(path.Trim('"', ' '));
            if (found.Count >= 2000 || !Path.IsPathFullyQualified(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return;
            found.TryAdd(path + "|" + arguments, new(name, path, arguments, directory));
        }
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var paths = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
                foreach (var key in paths?.GetSubKeyNames().Take(2000) ?? [])
                { using var entry = paths!.OpenSubKey(key); if (entry?.GetValue("") is string path) Add(Path.GetFileNameWithoutExtension(key), path); }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        }
        object? shellObject = null;
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell"); if (type is null) return found.Values.OrderBy(x => x.Name).ToList();
            shellObject = Activator.CreateInstance(type); dynamic shell = shellObject!;
            foreach (var folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) })
            {
                if (!Directory.Exists(folder)) continue;
                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
                foreach (var file in Directory.EnumerateFiles(folder, "*.lnk", options).Take(3000))
                {
                    object? linkObject = null;
                    try { linkObject = shell.CreateShortcut(file); dynamic link = linkObject; Add(Path.GetFileNameWithoutExtension(file), (string)link.TargetPath, (string)link.Arguments, (string)link.WorkingDirectory); }
                    catch (Exception ex) when (ex is COMException or IOException or ArgumentException) { }
                    finally { if (linkObject is not null && Marshal.IsComObject(linkObject)) Marshal.FinalReleaseComObject(linkObject); }
                }
            }
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException) { }
        finally { if (shellObject is not null && Marshal.IsComObject(shellObject)) Marshal.FinalReleaseComObject(shellObject); }
        return found.Values.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
