namespace SableLatch.Desktop;

public partial class MainWindow
{
    private void AddFolderDeletion(StackPanel page, VaultFolder target, bool focus)
    {
        var section = new StackPanel { Margin = new Thickness(0, 22, 0, 4) };
        var subtree = FolderOperations.Subtree(vault.Document!, target.Id);
        var count = vault.Document!.Items.Count(i => i.FolderId is Guid id && subtree.Contains(id));
        var warning = Text($"Delete “{target.Name}” with {count} {(count == 1 ? "entry" : "entries")} and {subtree.Count - 1} {(subtree.Count == 2 ? "subfolder" : "subfolders")}? Includes their Trash. This cannot be undone in Sable.", 11, true);
        warning.Visibility = Visibility.Collapsed;
        var armed = false; long readyAt = 0; var startEpoch = epoch; var owner = Body.Content;
        Button? remove = null;
        remove = AsyncButton("Delete folder", async () =>
        {
            if (!vault.IsUnlocked || busy || epoch != startEpoch || !ReferenceEquals(Body.Content, owner)) return;
            if (!armed)
            {
                armed = true; readyAt = Environment.TickCount64 + 600;
                warning.Visibility = Visibility.Visible;
                remove!.Content = new TextBlock { Text = "Delete permanently", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(239, 154, 161)) }; remove.IsEnabled = false;
                System.Windows.Automation.AutomationProperties.SetName(remove, "Delete permanently");
                warning.BringIntoView();
                await Task.Delay(600);
                if (ReferenceEquals(Body.Content, owner)) remove.IsEnabled = true;
                return;
            }
            if (Environment.TickCount64 < readyAt) return;
            if (Change(d => FolderOperations.DeleteTree(d, target.Id)))
            {
                vault.CleanOrphanAttachments(); if (folder is Guid current && subtree.Contains(current)) folder = target.ParentId;
                ShowList(); SetStatus("Folder and contents deleted");
            }
        }, false);
        section.Children.Add(warning); section.Children.Add(remove); page.Children.Add(section);
        if (focus) section.Loaded += (_, _) => Dispatcher.BeginInvoke(() => { section.BringIntoView(); remove.Focus(); });
    }
}
