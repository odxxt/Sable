namespace SableLatch.Desktop;

public partial class MainWindow
{
    private ContextMenu? activeContextMenu;
    private bool contextOpen => activeContextMenu?.IsOpen == true;
    private void TrackContextMenu(ContextMenu menu)
    {
        var owner = Body.Content;
        menu.Opened += (_, _) =>
        {
            // A queued popup event from an old page cannot block Enter on the new one.
            if (!ReferenceEquals(owner, Body.Content)) { menu.IsOpen = false; return; }
            activeContextMenu = menu;
        };
        menu.Closed += (_, _) => { if (ReferenceEquals(activeContextMenu, menu)) activeContextMenu = null; };
    }
    private void CloseContextMenu()
    {
        var previous = activeContextMenu; activeContextMenu = null;
        if (previous is not null) previous.IsOpen = false;
    }
}
