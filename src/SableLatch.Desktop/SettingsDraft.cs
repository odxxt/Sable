using System.Windows.Media.Animation;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private Func<bool>? settingsChanged;
    private Button? settingsSave;
    private TextBlock? draftHint;
    private bool discardArmed;
    private object? draftOwner;
    private void ResetSettingsDraft() { settingsChanged = null; settingsSave = null; draftHint = null; draftOwner = null; discardArmed = false; }
    private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var nested in LogicalDescendants(child)) yield return nested;
        }
    }
    private void DraftEdited()
    {
        if (clearingEditors) return;
        discardArmed = false;
        if (draftHint is not null) draftHint.Visibility = Visibility.Collapsed;
    }
    private void SettingsSave(StackPanel scope, Button save, Func<bool>? extraChanges = null)
    {
        var owner = new object(); draftOwner = owner;
        var changed = new List<Func<bool>>();
        void Edited() { if (ReferenceEquals(draftOwner, owner)) DraftEdited(); }
        foreach (var control in LogicalDescendants(scope))
        {
            switch (control)
            {
                case TextBox box:
                    var text = box.Text; changed.Add(() => box.Text != text); box.TextChanged += (_, _) => Edited(); break;
                case PasswordBox box:
                    // Settings password fields start empty; never retain another credential copy.
                    changed.Add(() => box.Password.Length != 0); box.PasswordChanged += (_, _) => Edited(); break;
                case CheckBox box:
                    var check = box.IsChecked; changed.Add(() => box.IsChecked != check);
                    box.Checked += (_, _) => Edited(); box.Unchecked += (_, _) => Edited(); break;
                case ComboBox box:
                    var selection = box.SelectedItem; changed.Add(() => !Equals(box.SelectedItem, selection)); box.SelectionChanged += (_, _) => Edited(); break;
            }
        }
        if (extraChanges is not null) changed.Add(extraChanges);
        settingsChanged = () => changed.Any(read => read()); settingsSave = save;
        draftHint = Text("Save changes? Back again to discard.", 11, true);
        draftHint.Visibility = Visibility.Collapsed; draftHint.Margin = new Thickness(3, 0, 0, 4);
        var footer = new StackPanel(); footer.Children.Add(draftHint); footer.Children.Add(save); PageAction(footer);
    }
    private void NavigateSettings(Action destination)
    {
        if (busy) return;
        if (settingsChanged?.Invoke() == true && !discardArmed)
        {
            discardArmed = true;
            if (draftHint is not null) draftHint.Visibility = Visibility.Visible;
            if (settingsSave is not null)
            {
                var transform = new TranslateTransform(); settingsSave.RenderTransform = transform;
                var shake = new DoubleAnimationUsingKeyFrames();
                var offsets = new double[] { 0, -4, 4, -3, 3, 0 };
                for (var i = 0; i < offsets.Length; i++) shake.KeyFrames.Add(new LinearDoubleKeyFrame(offsets[i], KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i * 50))));
                transform.BeginAnimation(TranslateTransform.XProperty, shake); settingsSave.Focus();
            }
            return;
        }
        destination();
    }
}
