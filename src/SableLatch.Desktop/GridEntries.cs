namespace SableLatch.Desktop;

public partial class MainWindow
{
    private const string GridTemplate = """
    <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
      <Border x:Name="Tile" Background="{Binding EntryBackground}" CornerRadius="8" Margin="2" Height="64">
        <Grid>
          <Border Background="#0AFFFFFF" CornerRadius="8"/>
          <Border x:Name="TileHover" Background="#0FFFFFFF" CornerRadius="8" Opacity="0" IsHitTestVisible="False"/>
          <Grid Visibility="{Binding ContentVisibility}">
            <Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width="32"/></Grid.ColumnDefinitions>
            <Button Tag="copy" Grid.ColumnSpan="2" Style="{DynamicResource EntryButton}" Padding="12,8,36,8" HorizontalContentAlignment="Stretch" IsEnabled="{Binding Available}" ToolTip="{Binding Title}">
              <StackPanel VerticalAlignment="Center">
                <TextBlock Text="{Binding Title}" FontSize="12" FontWeight="Medium" TextTrimming="CharacterEllipsis"/>
                <TextBlock Text="{Binding CompactSubtitle}" Visibility="{Binding SubtitleVisibility}" FontSize="11" Foreground="{DynamicResource Muted}" Margin="0,4,0,0" TextTrimming="CharacterEllipsis"/>
                <StackPanel Orientation="Horizontal" Margin="0,4,0,0" Visibility="{Binding LoginVisibility}">
                  <TextBlock Tag="Username" Text="username" FontSize="10" Opacity="{Binding UsernameOpacity}"/>
                  <TextBlock Text=" · " FontSize="10" Opacity=".4"/>
                  <TextBlock Tag="Password" Text="password" FontSize="10" Opacity="{Binding PasswordOpacity}"/>
                </StackPanel>
              </StackPanel>
            </Button>
            <Grid Grid.Column="1" VerticalAlignment="Center" Margin="0,0,4,0">
              <Button Tag="entry-action" Visibility="{Binding ActionVisibility}" Style="{DynamicResource EntryRunButton}" ToolTip="{Binding ActionLabel}" AutomationProperties.Name="{Binding ActionLabel}" Opacity=".7">
                <Grid Width="18" Height="18"><Path Data="M 2,5 L 10,10 2,15 Z" Stroke="{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}" StrokeThickness="1.3" StrokeLineJoin="Round"/><Path Visibility="{Binding ActionCopyVisibility}" Data="M 11,1 L 16,1 16,6 M 9,3 L 14,3 14,8 9,8 Z" Stroke="{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}" StrokeThickness="1.1" StrokeLineJoin="Round"/></Grid>
              </Button>
              <TextBlock Tag="copy-feedback" Text="&#xE73E;" FontFamily="Segoe MDL2 Assets" FontSize="14" Foreground="#FF99C3A4" Visibility="{Binding CopyVisibility}" HorizontalAlignment="Center" VerticalAlignment="Center" IsHitTestVisible="False"/>
            </Grid>
          </Grid>
          <Border Visibility="{Binding DeleteVisibility}" Background="{DynamicResource ControlSurface}" CornerRadius="8">
            <Grid><Button Tag="confirm-delete" Content="Delete" Style="{DynamicResource InlineDeleteButton}" Width="80" HorizontalAlignment="Center" VerticalAlignment="Center"/><Button Tag="cancel-delete" Content="&#xE711;" Style="{DynamicResource RowIcon}" HorizontalAlignment="Right" VerticalAlignment="Center" Width="24" ToolTip="Cancel delete"/></Grid>
          </Border>
        </Grid>
      </Border>
      <DataTemplate.Triggers>
        <Trigger SourceName="Tile" Property="IsMouseOver" Value="True"><Setter TargetName="TileHover" Property="Opacity" Value="1"/></Trigger>
        <Trigger SourceName="Tile" Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="TileHover" Property="Opacity" Value="1"/></Trigger>
      </DataTemplate.Triggers>
    </DataTemplate>
    """;
}
