namespace SableLatch.Desktop;

public interface IClipboardWriter
{
    string MediaType { get; }
    void Write(DataObject destination, ReadOnlyMemory<byte> bytes);
}
public interface IEntryLayout
{
    string Id { get; }
    string Name { get; }
    ListBox CreateHost();
}
public interface IEntryPresenter
{
    string ContentType { get; }
    DataTemplate Template { get; }
}
public sealed class EntryTemplateSelector(DesktopExtensions extensions, DataTemplate fallback) : DataTemplateSelector
{
    public override DataTemplate SelectTemplate(object item, DependencyObject container) => item is RowModel row && row.Available && extensions.Presenters.TryGetValue(row.Item.ContentType, out var presenter) ? presenter.Template : fallback;
}
public sealed class RowsLayout : IEntryLayout
{
    public string Id => "rows";
    public string Name => "Rows";
    public ListBox CreateHost() => new();
}
public sealed class FieldEditorFactory(string contentType, Func<VaultItem?, Action<VaultItem>, Action, FrameworkElement> build) : IEntryEditorFactory
{
    public string ContentType => contentType;
    public FrameworkElement Build(VaultItem? item, Action<VaultItem> save, Action cancel) => build(item, save, cancel);
}

// Explicit composition for trusted bundled features. This is not a dynamic DLL loader.
// An external plugin broker will validate permissions before invoking these contracts.
public sealed class DesktopExtensions
{
    public Dictionary<string, IEntryEditorFactory> Editors { get; } = [];
    public Dictionary<string, IEntryLayout> Layouts { get; } = [];
    public Dictionary<string, IEntryPresenter> Presenters { get; } = [];
    public Dictionary<string, IClipboardWriter> ClipboardWriters { get; } = [];
    public List<ToolbarContribution> Toolbar { get; } = [];
    public void RegisterEditor(IEntryEditorFactory editor) => Editors.Add(editor.ContentType, editor);
    public void RegisterLayout(IEntryLayout layout) => Layouts.Add(layout.Id, layout);
    public void RegisterPresenter(IEntryPresenter presenter) => Presenters.Add(presenter.ContentType, presenter);
    public void RegisterClipboard(IClipboardWriter writer) => ClipboardWriters.Add(writer.MediaType, writer);
    public void RegisterToolbar(ToolbarContribution action)
    {
        if (Toolbar.Any(x => x.Id == action.Id)) throw new InvalidOperationException("Toolbar action already registered.");
        Toolbar.Add(action);
    }
}
public sealed class TextClipboardWriter : IClipboardWriter
{
    public string MediaType => "text/plain";
    public void Write(DataObject destination, ReadOnlyMemory<byte> bytes) => destination.SetText(System.Text.Encoding.UTF8.GetString(bytes.Span), TextDataFormat.UnicodeText);
}
public sealed class PngClipboardWriter : IClipboardWriter
{
    public string MediaType => "image/png";
    public void Write(DataObject destination, ReadOnlyMemory<byte> bytes)
    {
        using var stream = new MemoryStream(bytes.ToArray(), false);
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit(); bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
        destination.SetImage(bitmap);
        destination.SetData("PNG", new MemoryStream(bytes.ToArray(), false));
    }
}
