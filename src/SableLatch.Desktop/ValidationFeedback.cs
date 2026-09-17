using System.Runtime.CompilerServices;

namespace SableLatch.Desktop;

internal static class ValidationFeedback
{
    private sealed class State { public int Attempts; public TextBlock? Hint; }
    private static readonly ConditionalWeakTable<Control, State> States = new();
    public static void Failed(Control field, string message)
    {
        // The lock screen owns its five-attempt countdown and its dedicated feedback.
        if (field.Tag as string == "bare-secret") return;
        var state = States.GetValue(field, _ =>
        {
            var value = new State();
            void Edited() { if (value.Hint is not null) value.Hint.Visibility = Visibility.Collapsed; }
            if (field is TextBox text) text.TextChanged += (_, _) => Edited();
            if (field is PasswordBox password) password.PasswordChanged += (_, _) => Edited();
            return value;
        });
        if (++state.Attempts < 3) return;
        if (state.Hint is null && field.Parent is StackPanel parent)
        {
            state.Hint = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(221, 151, 158)), Margin = new Thickness(3, 0, 3, 9) };
            parent.Children.Insert(parent.Children.IndexOf(field) + 1, state.Hint);
        }
        if (state.Hint is not null) { state.Hint.Text = message; state.Hint.Visibility = Visibility.Visible; }
    }
}
