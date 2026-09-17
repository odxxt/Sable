using SableLatch.Core;

namespace SableLatch.Examples;

// Trusted composition example, not an external executable plugin or screen capturer.
// A future permission broker supplies an attachment service scoped to the selected item.
public sealed class ScreenshotProvider(IAttachmentStore attachments) : IContentProvider
{
    public ContentDescriptor Descriptor => new("example.screenshot", 1, "Screenshot", "\uE91B", false);
    public IReadOnlyList<string> CopyActions => ["Copy"];
    public CopyValue Copy(VaultItem item, string action)
    {
        if (item.Attachments.Count != 1) throw new InvalidDataException("A screenshot requires one image attachment.");
        return new("image/png", attachments.ReadAttachment(item.Attachments[0]), item.IsSensitive, false);
    }
}
