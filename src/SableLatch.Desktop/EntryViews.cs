using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media.Animation;

namespace SableLatch.Desktop;

public sealed class RowModel : System.ComponentModel.INotifyPropertyChanged
{
    public required VaultItem Item { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string Symbol { get; init; }
    public bool DeleteReady { get; set; }
    public bool DeleteArmed { get; set; }
    public bool Copied { get; set; }
    public bool PendingPassword { get; set; }
    public string EditSymbol => Copied ? "\uE73E" : DeleteReady ? "\uE74D" : "\uE70F";
    public Visibility EditVisibility => DeleteArmed ? Visibility.Collapsed : Visibility.Visible;
    public Visibility DeleteVisibility => DeleteArmed ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ContentVisibility => DeleteArmed ? Visibility.Collapsed : Visibility.Visible;
    public Visibility SubtitleVisibility => LoginVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    public double UsernameOpacity => PendingPassword ? .45 : .85;
    public double PasswordOpacity => PendingPassword ? .95 : .45;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new(null));
    public Visibility LoginVisibility { get; init; }

    public bool Available { get; init; }
    public Brush EntryBackground => EntryColors.Brush(Item.Advanced?.Color);
    public bool ModuleAction { get; init; }
    public Visibility ActionVisibility => !DeleteArmed && Available && (ModuleAction || Item.Advanced?.Action?.Enabled == true) ? Visibility.Visible : Visibility.Collapsed;
    public bool RdpAction { get; init; }
    private bool CopiesOnAction => RdpAction ? Item.Field("copyOnLaunch") == "true" : Item.Advanced?.Action?.CopyOnRun == true;
    public string ActionLabel => RdpAction ? (CopiesOnAction ? "Launch & copy" : "Launch") : Item.Advanced?.Action?.CopyOnRun == true ? "Run & copy" : "Run";
    public Visibility ActionCopyVisibility => CopiesOnAction ? Visibility.Visible : Visibility.Collapsed;
}

public partial class MainWindow
{
    private ListBox? entryList;
    private static readonly string RowTemplate = """
    <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
      <Border x:Name="Row" Background="{Binding EntryBackground}" CornerRadius="10" Height="56">
        <Grid><Grid Visibility="{Binding ContentVisibility}"><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
          <Border x:Name="HoverShade" Grid.ColumnSpan="3" Background="#12FFFFFF" CornerRadius="9" Opacity="0" IsHitTestVisible="False"/>
          <Button Tag="copy" Style="{DynamicResource RowButton}" HorizontalContentAlignment="Stretch" Padding="9,7" IsEnabled="{Binding Available}">
            <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="27"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
              <Grid Width="20" Height="20" HorizontalAlignment="Left" VerticalAlignment="Center">
                <TextBlock x:Name="SymbolGlyph" Text="{Binding Symbol}" FontFamily="Segoe MDL2 Assets" FontSize="15" Foreground="#FFC0C0C7" VerticalAlignment="Center"/>
                <Path x:Name="SshIcon" Visibility="Collapsed" Stroke="#FFC0C0C7" StrokeThickness="1.2" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round" Data="M 2,3 L 18,3 18,16 2,16 Z M 5,7 L 8,10 5,13 M 11,13 L 15,13"/><Path x:Name="NoteIcon" Visibility="Collapsed" Stroke="#FFC0C0C7" StrokeThickness="1" StrokeStartLineCap="Round" StrokeEndLineCap="Round" Data="M 4,4 L 16,4 16,17 4,17 Z M 7,2 L 7,6 M 10,2 L 10,6 M 13,2 L 13,6 M 7,9 L 13,9 M 7,12 L 13,12 M 7,15 L 11,15"/>
              </Grid>
              <StackPanel Grid.Column="1"><TextBlock Text="{Binding Title}" FontSize="13" FontWeight="Medium" TextTrimming="CharacterEllipsis"/><TextBlock Text="{Binding Subtitle}" Visibility="{Binding SubtitleVisibility}" FontSize="10" Foreground="#FFA3A3AA" Margin="0,4,0,0" TextTrimming="CharacterEllipsis"/><StackPanel Orientation="Horizontal" Margin="0,4,0,0" Visibility="{Binding LoginVisibility}"><TextBlock Tag="Username" Text="username" FontSize="10" Opacity="{Binding UsernameOpacity}"/><TextBlock Text="  ·  " FontSize="10" Opacity=".35"/><TextBlock Tag="Password" Text="password" FontSize="10" Opacity="{Binding PasswordOpacity}"/></StackPanel></StackPanel>
            </Grid>
          </Button>
          <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center">
            <Button Tag="entry-action" Visibility="{Binding ActionVisibility}" Opacity=".7" Style="{DynamicResource RowIcon}" ToolTip="{Binding ActionLabel}" AutomationProperties.Name="{Binding ActionLabel}"><Grid Width="18" Height="18"><Path Data="M 2,5 L 10,10 2,15 Z" Stroke="{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}" StrokeThickness="1.3" StrokeLineJoin="Round"/><Path Visibility="{Binding ActionCopyVisibility}" Data="M 11,1 L 16,1 16,6 M 9,3 L 14,3 14,8 9,8 Z" Stroke="{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}" StrokeThickness="1.1" StrokeLineJoin="Round"/></Grid></Button>
            <Button Tag="edit" Content="{Binding EditSymbol}" Visibility="{Binding EditVisibility}" Opacity=".45" Style="{DynamicResource RowIcon}" ToolTip="Edit · hold here for delete"/>
          </StackPanel>
        </Grid>
          <Border Visibility="{Binding DeleteVisibility}" Background="{DynamicResource ControlSurface}" CornerRadius="10">
            <Grid>
              <Button Tag="confirm-delete" Style="{DynamicResource InlineDeleteButton}" HorizontalAlignment="Center" VerticalAlignment="Center" ToolTip="{Binding Title}" AutomationProperties.Name="Confirm delete entry">
                <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                  <Path Width="13" Height="14" Margin="0,0,7,0" VerticalAlignment="Center" Stroke="{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}" StrokeThickness="1.1" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round" Data="M 1,3 L 12,3 M 4,3 L 4,1 9,1 9,3 M 2,3 L 3,13 10,13 11,3 M 5,6 L 5,10 M 8,6 L 8,10"/>
                  <TextBlock Text="Delete" FontSize="12" VerticalAlignment="Center" Foreground="{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}"/>
                </StackPanel>
              </Button>
              <Button Tag="cancel-delete" Content="&#xE711;" Style="{DynamicResource RowIcon}" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,7,0" Opacity=".65" ToolTip="Cancel delete · Esc" AutomationProperties.Name="Cancel delete"/>
            </Grid>
          </Border>
        </Grid>
      </Border>
      <DataTemplate.Triggers><DataTrigger Binding="{Binding Item.ContentType}" Value="module.sablelatch.putty"><Setter TargetName="SymbolGlyph" Property="Visibility" Value="Collapsed"/><Setter TargetName="SshIcon" Property="Visibility" Value="Visible"/></DataTrigger><DataTrigger Binding="{Binding Item.ContentType}" Value="sable.text"><Setter TargetName="SymbolGlyph" Property="Visibility" Value="Collapsed"/><Setter TargetName="NoteIcon" Property="Visibility" Value="Visible"/></DataTrigger><Trigger SourceName="Row" Property="IsMouseOver" Value="True"><Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="HoverShade" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.12"/></Storyboard></BeginStoryboard></Trigger.EnterActions><Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="HoverShade" Storyboard.TargetProperty="Opacity" To="0" Duration="0:0:0.18"/></Storyboard></BeginStoryboard></Trigger.ExitActions></Trigger><Trigger SourceName="Row" Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="HoverShade" Property="Opacity" Value=".45"/></Trigger></DataTemplate.Triggers>
    </DataTemplate>
    """;
    private void ShowList()
    {
        if (!vault.IsUnlocked) { ShowUnlock(); return; }
        var grid = new Grid();
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.VerticalAlignment = VerticalAlignment.Stretch;
        SetBody(grid, "list");
        var find = new TextBox { Text = search, Height = 36, ToolTip = "Search entry labels across all folders" };
        System.Windows.Automation.AutomationProperties.SetName(find, "Search vault");
        find.Tag = "Search vault";
        var searchHost = new Grid { Margin = new Thickness(0, 0, 0, 8), Visibility = searchVisible ? Visibility.Visible : Visibility.Collapsed }; searchHost.Children.Add(find);
        var placeholder = Text("Search your vault…", 12, true); placeholder.Margin = new Thickness(11, 0, 0, 0); placeholder.VerticalAlignment = VerticalAlignment.Center; placeholder.IsHitTestVisible = false; placeholder.Visibility = string.IsNullOrEmpty(search) ? Visibility.Visible : Visibility.Collapsed;
        searchHost.Children.Add(placeholder); grid.Children.Add(searchHost);
        var navigation = new StackPanel { Margin = new Thickness(0, 0, 0, 5) }; Grid.SetRow(navigation, 1); grid.Children.Add(navigation);
        var navRow = new DockPanel(); navigation.Children.Add(navRow);
        if (folder is null) navRow.Children.Add(new TextBlock { Text = "All items", FontSize = 12, Margin = new Thickness(10, 10, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        else
        {
            var parent = vault.Document!.Folders.Single(x => x.Id == folder).ParentId;
            var breadcrumb = Button("‹  " + FolderPath(folder.Value), () => { folder = parent; ShowList(); });
            AttachFolderDrop(breadcrumb, parent); navRow.Children.Add(new System.Windows.Documents.AdornerDecorator { Child = breadcrumb });
        }
        var createMenu = new ContextMenu { MinWidth = 160, Placement = System.Windows.Controls.Primitives.PlacementMode.Custom, CustomPopupPlacementCallback = (popup, target, offset) => [new(new Point(target.Width - popup.Width, target.Height), System.Windows.Controls.Primitives.PopupPrimaryAxis.Horizontal)] };
        var addEntry = new MenuItem { Header = "New entry" }; addEntry.Click += (_, _) => ShowEditor(advanced: false);
        var addFolder = new MenuItem { Header = "New folder" }; addFolder.Click += (_, _) => ShowFolderEditor();
        createMenu.Items.Add(addEntry); createMenu.Items.Add(addFolder);
        var create = Button("＋", () => createMenu.IsOpen = true); create.ToolTip = "Create entry or folder";
        System.Windows.Automation.AutomationProperties.SetName(create, "Create entry or folder");
        createMenu.PlacementTarget = create; create.ContextMenu = createMenu;
        create.HorizontalAlignment = HorizontalAlignment.Right; navRow.Children.Add(create);
        var chips = new WrapPanel(); folderChips = chips;
        navigation.Children.Add(new System.Windows.Documents.AdornerDecorator { Child = chips });
        AttachFolderPanelDrop(chips);
        foreach (var f in vault.Document!.Folders.Where(f => f.ParentId == folder).OrderBy(f => f.Order))
        {
            var chip = Button(f.Name, () => { folder = f.Id; search = ""; ShowList(); }); chip.Tag = f.Id; chip.Content = FolderIcons.Label(f); chip.MaxWidth = 150; chip.ToolTip = f.Name; AttachFolderDrop(chip, f.Id); AttachFolderDrag(chip, f.Id);
            var menu = new ContextMenu(); var rename = new MenuItem { Header = "Edit folder" }; rename.Click += (_, _) => ShowFolderEditor(f); menu.Items.Add(rename);
            var moveFolder = new MenuItem { Header = "Move folder…" }; moveFolder.Click += (_, _) => ShowMoveFolder(f); menu.Items.Add(moveFolder);
            menu.Opened += (_, _) => contextOpen = true; menu.Closed += (_, _) => contextOpen = false;
            var delete = new MenuItem { Header = "Delete empty folder" }; delete.Click += (_, _) =>
            {
                if (vault.Document.Items.Any(i => i.FolderId == f.Id) || vault.Document.Folders.Any(i => i.ParentId == f.Id)) { ShowError("Move the folder's entries and subfolders first, including trashed entries."); return; }
                if (Change(d => d.Folders.RemoveAll(x => x.Id == f.Id))) ShowList();
            };
            menu.Items.Add(delete); chip.ContextMenu = menu; chips.Children.Add(chip);
        }
        entryList = (extensions.Layouts.GetValueOrDefault(vault.Document.Settings.Layout) ?? extensions.Layouts["rows"]).CreateHost();
        entryList.VerticalAlignment = VerticalAlignment.Stretch;
        entryList.Background = Brushes.Transparent; entryList.BorderThickness = new Thickness(0); entryList.HorizontalContentAlignment = HorizontalAlignment.Stretch; entryList.ItemTemplateSelector = new EntryTemplateSelector(extensions, (DataTemplate)XamlReader.Parse(RowTemplate));
        var itemStyle = new Style(typeof(ListBoxItem)); itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch)); itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0))); itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent)); itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0))); itemStyle.Setters.Add(new Setter(Control.TemplateProperty, XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ListBoxItem'><ContentPresenter/></ControlTemplate>"))); entryList.ItemContainerStyle = itemStyle;
        ScrollViewer.SetHorizontalScrollBarVisibility(entryList, ScrollBarVisibility.Disabled); ScrollViewer.SetVerticalScrollBarVisibility(entryList, ScrollBarVisibility.Auto);
        ScrollViewer.SetCanContentScroll(entryList, true); VirtualizingPanel.SetScrollUnit(entryList, ScrollUnit.Pixel);
        VirtualizingPanel.SetIsVirtualizing(entryList, true); VirtualizingPanel.SetVirtualizationMode(entryList, VirtualizationMode.Recycling);
        var viewport = new Grid(); Grid.SetRow(viewport, 2); grid.Children.Add(viewport);
        viewport.Children.Add(new System.Windows.Documents.AdornerDecorator { Child = entryList });
        var mountedList = entryList;
        mountedList.Loaded += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (mountedList.IsLoaded) foreach (var viewer in Descendants<ScrollViewer>(mountedList)) ScrollAffordances.Attach(viewer);
        }));
        entryList.AddHandler(System.Windows.Controls.Button.ClickEvent, new RoutedEventHandler(RowClick));
        entryList.PreviewMouseRightButtonDown += EntryContext;
        entryList.PreviewMouseLeftButtonDown += BeginRowDrag; entryList.AllowDrop = true; entryList.Drop += DropRow;
        entryList.PreviewMouseMove += TrackRowPointer;
        entryList.MouseLeave += (_, _) => CancelEditHover();
        entryList.DragOver += PreviewRowDrop;
        entryList.DragLeave += (_, _) => ClearDropMarker();
        entryList.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Delete && FindParent<ListBoxItem>(Keyboard.FocusedElement as DependencyObject)?.DataContext is RowModel row) { ArmDelete(row); e.Handled = true; }
            if (Keyboard.Modifiers == ModifierKeys.Alt && Keyboard.FocusedElement is Button { Tag: "copy", DataContext: RowModel moving } && e.Key is Key.Up or Key.Down) { MoveEntry(moving.Item.Id, e.Key == Key.Up ? -1 : 1); e.Handled = true; }
        };
        find.TextChanged += (_, _) => { if (clearingEditors) return; search = find.Text; placeholder.Visibility = string.IsNullOrEmpty(search) ? Visibility.Visible : Visibility.Collapsed; armedDelete = null; PopulateRows(); };
        eraseEditors.Add(find.Clear);
        PopulateRows(); UpdateStatus();
        if (searchVisible) Dispatcher.BeginInvoke(() => find.Focus());
    }
    private void PopulateRows()
    {
        if (!vault.IsUnlocked || entryList is null) return;
        var doc = vault.Document!;
        var items = string.IsNullOrWhiteSpace(search) ? doc.Items.Where(i => i.DeletedAt is null && i.FolderId == folder) : ItemOperations.Search(doc, search);
        entryList.ItemsSource = items.OrderBy(i => i.Order).Select(i =>
        {
            var provider = content.Find(i.ContentType); var available = provider is not null && i.ContentVersion == provider.Descriptor.Version && !doc.Settings.DisabledModules.Contains(i.ContentType) && ModuleAvailable(i);
            return new RowModel
            {
                Item = i, Title = string.IsNullOrWhiteSpace(i.Label) ? i.IsSensitive ? "Secret" : i.Field("text").Split('\n')[0] : i.Label,
                Subtitle = !available ? "Module unavailable" : i.IsSensitive ? "Secret · ••••••••" : string.IsNullOrWhiteSpace(i.Label) ? "Text" : i.Field("text").Replace('\n', ' '),
                Symbol = provider?.Descriptor.Symbol ?? "\uE74C", DeleteArmed = armedDelete == i.Id, Available = available, PendingPassword = pendingLogin == i.Id,
                ModuleAction = provider is DeclarativeProvider, RdpAction = provider is DeclarativeProvider { Package.Manifest.Handler: "rdp" },
                LoginVisibility = i.ContentType == "sable.login" ? Visibility.Visible : Visibility.Collapsed
            };
        }).ToList();
    }
    private async void RowClick(object sender, RoutedEventArgs e)
    {
        if (busy || !vault.IsUnlocked || e.OriginalSource is not Button b || b.DataContext is not RowModel row) return;
        var action = b.Tag as string;
        if (action is not "edit" and not "confirm-delete") armedDelete = null;
        switch (action)
        {
            case "entry-action": if (row.Available) { if (content.Find(row.Item.ContentType) is DeclarativeProvider module) { if (module.Package.Manifest.Handler == "putty") RunPutty(row.Item); else if (module.Package.Manifest.Handler == "rdp") RunRdp(row.Item); else ShowModuleDetails(row.Item); } else ReviewEntryAction(row.Item); } break;
            case "edit":
                if (row.DeleteReady) ArmDelete(row);
                else if (row.Available) ShowEditor(row.Item);
                else ShowError("Enable or install the matching content provider to edit this entry.");
                break;
            case "confirm-delete": if (armedDelete == row.Item.Id) DeleteEntry(row.Item); break;
            case "cancel-delete": CancelDelete(); break;
            case "up": MoveEntry(row.Item.Id, -1); break;
            case "down": MoveEntry(row.Item.Id, 1); break;
            case "copy":
                if (content.Find(row.Item.ContentType) is DeclarativeProvider selectedModule) { if (selectedModule.Package.Manifest.Handler == "rdp") await CopyEntry(row.Item, "Password", b); else ShowModuleDetails(row.Item); }
                else if (row.Item.ContentType == "sable.login") await CopyEntry(row.Item, pendingLogin == row.Item.Id ? "Password" : "Username", b);
                else if (content.Find(row.Item.ContentType)?.CopyActions.Count > 1) ShowContentActions(row.Item);
                else await CopyEntry(row.Item, content.Find(row.Item.ContentType)?.CopyActions.FirstOrDefault() ?? "Copy", b);
                break;
            case "username": await CopyEntry(row.Item, "Username", b); break;
            case "password": await CopyEntry(row.Item, "Password", b); break;
        }
        e.Handled = true;
    }
    private async Task CopyEntry(VaultItem item, string action, Button source)
    {
        if (!vault.IsUnlocked || busy) return;
        var current = vault.Document!.Items.FirstOrDefault(i => i.Id == item.Id && i.DeletedAt is null);
        if (current is null || !ModuleAvailable(current) || vault.Document.Settings.DisabledModules.Contains(current.ContentType)) return;
        item = current;
        var provider = content.Find(item.ContentType); if (provider is null) return;
        var value = provider.Copy(item, action); var startEpoch = epoch; var generation = ++copyGeneration;
        try
        {
            clipboard.Copy(value, vault.Document!.Settings);
            holdForPassword = value.KeepPanelOpen;
            pendingLogin = item.ContentType == "sable.login" && value.KeepPanelOpen ? item.Id : null;
            Topmost = session.IsPinned || holdForPassword;
            if (entryList is not null) foreach (var row in entryList.Items.Cast<RowModel>()) { row.PendingPassword = row.Item.Id == pendingLogin; row.Copied = row.Item.Id == item.Id; row.Refresh(); }
            SetStatus(value.KeepPanelOpen ? "Username copied · ready for the password" : "Copied");
            // Crossfade the edit marker into a checkmark while the row flashes.
            var rowElement = FindParent<ListBoxItem>(source);
            var marker = rowElement is null ? null : Descendants<Button>(rowElement).FirstOrDefault(x => (x.Tag as string) == "edit");
            if (marker is not null) marker.BeginAnimation(OpacityProperty, new DoubleAnimation(0, .8, TimeSpan.FromMilliseconds(130)) { FillBehavior = FillBehavior.Stop });
            var caption = rowElement is null ? null : Descendants<TextBlock>(rowElement).FirstOrDefault(x => x.Tag as string == action);
            caption?.BeginAnimation(OpacityProperty, new DoubleAnimation(.1, 1, TimeSpan.FromMilliseconds(160)) { AutoReverse = true, RepeatBehavior = new RepeatBehavior(2), FillBehavior = FillBehavior.Stop });
            source.BeginAnimation(OpacityProperty, new DoubleAnimation(0.45, 1, TimeSpan.FromMilliseconds(200)));
            await Task.Delay(230);
            if (startEpoch != epoch || !vault.IsUnlocked || generation != copyGeneration) return;
            if (!session.IsPinned && !value.KeepPanelOpen && vault.Document!.Settings.CloseAfterCopy) Hide();
            await Task.Delay(500);
            if (startEpoch == epoch && source.DataContext is RowModel copiedRow) { copiedRow.Copied = false; copiedRow.Refresh(); }
        }
        catch (System.Runtime.InteropServices.ExternalException) { ShowError("The clipboard is busy. Try copying again."); }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or ArgumentException) { ShowError("This entry has invalid formatting. Edit it as plain text before copying."); }
        finally { CryptographicOperations.ZeroMemory(value.Data); }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T value) yield return value; foreach (var descendant in Descendants<T>(child)) yield return descendant; }
    }
    private void ShowContentActions(VaultItem item)
    {
        var provider = content.Find(item.ContentType); if (provider is null) return;
        var stack = Form(item.Label, provider.Descriptor.Name, "actions");
        foreach (var action in provider.CopyActions)
        {
            Button? button = null;
            button = AsyncButton(action, () => CopyEntry(item, action, button!));
            stack.Children.Add(button);
        }
        stack.Children.Add(Button("Edit entry", () => ShowEditor(item)));
    }
    private void EntryContext(object sender, MouseButtonEventArgs e)
    {
        var li = FindParent<ListBoxItem>(e.OriginalSource as DependencyObject); if (li?.DataContext is not RowModel row) return;
        var menu = BuildEntryContext(row);
        li.ContextMenu = menu; menu.IsOpen = true; e.Handled = true;
    }
    private ContextMenu BuildEntryContext(RowModel row)
    {
        var menu = new ContextMenu();
        if (content.Find(row.Item.ContentType) is DeclarativeProvider { Package.Manifest.Handler: "rdp" })
        {
            var copyPassword = new MenuItem { Header = "Copy password", IsEnabled = row.Available }; copyPassword.Click += async (_, _) => await CopyEntry(row.Item, "Password", new Button()); menu.Items.Add(copyPassword);
            var launch = new MenuItem { Header = "Launch", IsEnabled = row.Available }; launch.Click += (_, _) => RunRdp(row.Item, false); menu.Items.Add(launch);
            var launchCopy = new MenuItem { Header = "Launch & copy", IsEnabled = row.Available }; launchCopy.Click += (_, _) => RunRdp(row.Item, true); menu.Items.Add(launchCopy);
            var details = new MenuItem { Header = "Connection details", IsEnabled = row.Available }; details.Click += (_, _) => ShowModuleDetails(row.Item); menu.Items.Add(details);
        }
        if (row.ActionVisibility == Visibility.Visible && !row.RdpAction)
        {
            var run = new MenuItem { Header = row.ActionLabel, IsEnabled = row.Available };
            run.Click += (_, _) => { if (content.Find(row.Item.ContentType) is DeclarativeProvider module) { if (module.Package.Manifest.Handler == "putty") RunPutty(row.Item); else if (module.Package.Manifest.Handler == "rdp") RunRdp(row.Item); else ShowModuleDetails(row.Item); } else ReviewEntryAction(row.Item); }; menu.Items.Add(run); menu.Items.Add(new Separator());
        }
        var edit = new MenuItem { Header = "Edit", IsEnabled = row.Available }; edit.Click += (_, _) => ShowEditor(row.Item); menu.Items.Add(edit);
        var duplicate = new MenuItem { Header = "Duplicate", IsEnabled = row.Available }; duplicate.Click += (_, _) => DuplicateEntry(row.Item); menu.Items.Add(duplicate);
        if (row.Item.Advanced is null) { var advanced = new MenuItem { Header = "Advanced options…", IsEnabled = row.Available }; advanced.Click += (_, _) => ShowEditor(row.Item, advanced: true); menu.Items.Add(advanced); }
        var move = new MenuItem { Header = "Move to folder…" }; move.Click += (_, _) => ShowMove(row.Item); menu.Items.Add(move);
        var del = new MenuItem { Header = "Delete…" }; del.Click += (_, _) => ArmDelete(row); menu.Items.Add(del);
        menu.Opened += (_, _) => contextOpen = true; menu.Closed += (_, _) => contextOpen = false;
        return menu;
    }
    private void DeleteEntry(VaultItem item)
    {
        if (Change(d => ItemOperations.Delete(d, item.Id, DateTimeOffset.UtcNow)))
        {
            armedDelete = null; vault.CleanOrphanAttachments(); ShowList();
            SetStatus(item.IsSensitive && vault.Document!.Settings.TrashSecrets || !item.IsSensitive && vault.Document!.Settings.TrashNormal ? "Moved to encrypted Trash" : "Entry deleted");
        }
    }
    private void DuplicateEntry(VaultItem item)
    {
        if (!vault.IsUnlocked || busy || !ModuleAvailable(item)) return;
        editorTestedAction = null;
        ShowEditor(ItemOperations.DuplicateDraft(vault.Document!, item.Id));
    }
    private void MoveEntry(Guid id, int offset)
    {
        var positions = RowPositions();
        if (Change(d =>
        {
            var siblings = d.Items.Where(i => i.DeletedAt is null && i.FolderId == folder).OrderBy(i => i.Order).ToList();
            var from = siblings.FindIndex(x => x.Id == id); if (from < 0) return; var to = Math.Clamp(from + offset, 0, siblings.Count - 1);
            var item = siblings[from]; siblings.RemoveAt(from); siblings.Insert(to, item);
            for (var i = 0; i < siblings.Count; i++) siblings[i].Order = i;
        })) { PopulateRows(); AnimateRows(positions); }
    }
    private string FolderPath(Guid id)
    {
        var names = new List<string>(); Guid? next = id;
        while (next is Guid current) { var f = vault.Document!.Folders.Single(x => x.Id == current); names.Insert(0, f.Name); next = f.ParentId; }
        return string.Join(" / ", names);
    }
    private void ShowFolderEditor(VaultFolder? existing = null)
    {
        var stack = Form(existing is null ? "New folder" : "Edit folder"); var name = Input(stack, "Folder name", existing?.Name ?? "");
        var selected = existing?.Icon ?? "folder";
        var options = new System.Windows.Controls.Primitives.UniformGrid { Columns = 5, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 8) };
        var choices = new Dictionary<string, Button>();
        foreach (var pair in FolderIcons.Choices)
        {
            var iconLabel = new StackPanel(); var symbol = FolderIcons.Create(pair.Key); symbol.HorizontalAlignment = HorizontalAlignment.Center; iconLabel.Children.Add(symbol);
            iconLabel.Children.Add(new TextBlock { Text = pair.Value, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 7, 0, 0) });
            var choice = Button(pair.Value, () => { selected = pair.Key; foreach (var item in choices) item.Value.Background = item.Key == selected ? (Brush)FindResource("HoverSurface") : Brushes.Transparent; });
            choice.Content = iconLabel; choice.Padding = new Thickness(5, 10, 5, 8); choice.Margin = new Thickness(1); choice.HorizontalContentAlignment = HorizontalAlignment.Center;
            choice.Background = pair.Key == selected ? (Brush)FindResource("HoverSurface") : Brushes.Transparent;
            System.Windows.Automation.AutomationProperties.SetName(choice, pair.Value + " icon"); choices.Add(pair.Key, choice); options.Children.Add(choice);
        }
        stack.Children.Add(Button("Customize", () => options.Visibility = options.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible)); stack.Children.Add(options);
        var destinations = FolderDestinations(existing?.Id);
        var currentParent = existing?.ParentId ?? folder;
        // Root is a real destination, including when editing a root-level folder from search.
        if (existing is not null) currentParent = existing.ParentId;
        var parent = Select(stack, "Parent folder", destinations.Keys, destinations.First(p => p.Value == currentParent).Key);
        PageAction(Button("Save folder", () =>
        {
            var text = name.Text.Trim(); if (text.Length is < 1 or > 100) { ShowError("Use a folder name with 1–100 characters."); return; }
            var destination = destinations[(string)parent.SelectedItem];
            if (Change(d => { if (existing is null) d.Folders.Add(new() { Name = text, Icon = selected, ParentId = destination, Order = d.Folders.Count }); else { var item = d.Folders.Single(x => x.Id == existing.Id); item.Name = text; item.Icon = selected; if (item.ParentId != destination) FolderOperations.Move(d, item.Id, destination); } })) ShowList();
        }, true));
    }
    private void ShowMove(VaultItem item)
    {
        var stack = Form("Move entry.", item.Label); var destinations = new Dictionary<string, Guid?> { ["All items (root)"] = null };
        foreach (var f in vault.Document!.Folders) destinations[FolderPath(f.Id) + " · " + f.Id.ToString()[..4]] = f.Id;
        var pick = Select(stack, "Destination folder", destinations.Keys, destinations.Keys.First());
        pick.Loaded += (_, _) => { pick.Focus(); pick.IsDropDownOpen = true; };
        PageAction(Button("Move here", () => { if (Change(d => d.Items.Single(x => x.Id == item.Id).FolderId = destinations[(string)pick.SelectedItem])) ShowList(); }, true));
    }
}
