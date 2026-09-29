namespace SableLatch.Core;

public static class FolderOperations
{
    public static bool CanMove(VaultDocument doc, Guid id, Guid? destination)
    {
        var folders = doc.Folders.ToDictionary(f => f.Id);
        if (!folders.ContainsKey(id)) return false;
        var visited = new HashSet<Guid>();
        while (destination is Guid parent)
        {
            if (parent == id || !visited.Add(parent) || !folders.TryGetValue(parent, out var folder)) return false;
            destination = folder.ParentId;
        }
        return true;
    }

    // Index is in the destination's siblings after removing the moving folder.
    public static void Move(VaultDocument doc, Guid id, Guid? destination, int? index = null)
    {
        if (!CanMove(doc, id, destination)) throw new InvalidDataException("A folder cannot move inside itself or one of its subfolders.");
        var moving = doc.Folders.Single(f => f.Id == id);
        var previous = moving.ParentId;
        var siblings = doc.Folders.Where(f => f.Id != id && f.ParentId == destination).OrderBy(f => f.Order).ToList();
        moving.ParentId = destination;
        siblings.Insert(Math.Clamp(index ?? siblings.Count, 0, siblings.Count), moving);
        for (var i = 0; i < siblings.Count; i++) siblings[i].Order = i;
        if (previous != destination)
        {
            var old = doc.Folders.Where(f => f.ParentId == previous).OrderBy(f => f.Order).ToList();
            for (var i = 0; i < old.Count; i++) old[i].Order = i;
        }
    }
}
