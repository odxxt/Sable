namespace SableLatch.Core;

public static class PortableData
{
    public static string DefaultDirectory(string root) => Directory.Exists(Path.Combine(root, "SableLatch-data"))
        ? Path.Combine(root, "SableLatch-data") : Path.Combine(root, "Sable-data");
}
