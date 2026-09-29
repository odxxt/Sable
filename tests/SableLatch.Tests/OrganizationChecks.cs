using SableLatch.Core;
using System.Text.Json;

internal static class OrganizationChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        var a = new VaultFolder { Name = "A" }; var b = new VaultFolder { Name = "B", Order = 1 }; var c = new VaultFolder { Name = "C", Order = 2 };
        var child = new VaultFolder { Name = "Child", ParentId = a.Id }; var nested = new VaultFolder { Name = "Nested", ParentId = child.Id };
        var doc = new VaultDocument { Folders = [a, b, c, child, nested] };
        check(!FolderOperations.CanMove(doc, a.Id, a.Id) && !FolderOperations.CanMove(doc, a.Id, nested.Id), "Folder destinations exclude self and every descendant");
        reject(() => FolderOperations.Move(doc, a.Id, nested.Id), "Folder cycle rejected before mutation");
        check(a.ParentId is null && nested.ParentId == child.Id, "Rejected folder move leaves hierarchy intact");
        check(!FolderOperations.CanMove(doc, Guid.NewGuid(), null) && !FolderOperations.CanMove(doc, a.Id, Guid.NewGuid()), "Unknown folder sources and destinations rejected");
        FolderOperations.Move(doc, c.Id, null, 0);
        check(doc.Folders.Where(f => f.ParentId is null).OrderBy(f => f.Order).Select(f => f.Id).SequenceEqual(new[] { c.Id, a.Id, b.Id }), "Folder reorder normalizes sibling order");
        var source = new VaultItem { Label = "Login", ContentType = "sable.login", IsSensitive = true, FolderId = nested.Id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, Payload = JsonSerializer.SerializeToElement(new { username = "fixture", password = "fixture-secret", html = "<b>fixture</b>" }), Attachments = [Guid.NewGuid()], ModuleSchema = "schema-v1", Advanced = new() { Color = "sage" } };
        doc.Items.Add(source);
        FolderOperations.Move(doc, a.Id, b.Id);
        check(a.ParentId == b.Id && child.ParentId == a.Id && nested.ParentId == child.Id && source.FolderId == nested.Id, "Moving a folder preserves nested folders and entry membership");
        FolderOperations.Move(doc, a.Id, null); doc.Validate();
        check(a.ParentId is null && a.Order == 2, "Folder can move back to root after its siblings");
        FolderOperations.Move(doc, nested.Id, b.Id); doc.Validate();
        check(nested.ParentId == b.Id && nested.Order == 0 && source.FolderId == nested.Id, "Folder moves across parents without changing entry identity");
        var copy = ItemOperations.DuplicateDraft(doc, source.Id);
        check(doc.Items.Count == 1 && copy.Id != source.Id && copy.CreatedAt is null && copy.UpdatedAt is null && copy.Label == "Login (copy)", "Duplicate is an unsaved draft with a fresh ID and no inherited timestamps");
        check(copy.Payload.GetRawText() == source.Payload.GetRawText() && copy.ModuleSchema == source.ModuleSchema && copy.ContentType == source.ContentType && copy.IsSensitive && copy.FolderId == source.FolderId && copy.Attachments.SequenceEqual(source.Attachments), "Duplicate preserves sensitive values, formatted payload, module schema and attachment references");
        copy.Advanced!.Color = "rose"; copy.Attachments.Clear();
        check(source.Advanced!.Color == "sage" && source.Attachments.Count == 1, "Duplicate mutable metadata and attachment lists do not alias the source");
        doc.Items.Add(copy);
        check(ItemOperations.DuplicateDraft(doc, source.Id).Label == "Login (copy 2)", "Repeated duplicates receive distinct labels");
        EntryMetadata.MarkSaved(copy, null, DateTimeOffset.UtcNow);
        check(copy.CreatedAt is not null && copy.UpdatedAt == copy.CreatedAt && source.Label == "Login", "Saving a duplicate gets its own timestamps without editing the original");
        source.Label = new string('x', 300);
        check(ItemOperations.DuplicateDraft(doc, source.Id).Label.Length == 300, "Duplicate label respects the editor length limit");
        var restored = Codec.Clone(doc); restored.Validate();
        check(restored.Folders.Single(f => f.Id == nested.Id).ParentId == b.Id && restored.Items.Count == 2, "Folder organization and duplicates survive document serialization");
    }
}
