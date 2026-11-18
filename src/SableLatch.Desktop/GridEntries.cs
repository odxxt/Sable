namespace SableLatch.Desktop;

public partial class MainWindow
{
    private const string GridTemplate = """
    <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:local="clr-namespace:SableLatch.Desktop;assembly=Sable">
      <Border x:Name="Tile" Background="{Binding EntryBackground}" CornerRadius="8" Margin="2" Height="64">
        <Grid>
          <Border Background="#0AFFFFFF" CornerRadius="8"/>
          <Border x:Name="TileHover" Background="#0FFFFFFF" CornerRadius="8" Opacity="0" IsHitTestVisible="False"/>
          <Grid Visibility="{Binding ContentVisibility}">
            <Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
            <Button Tag="copy" Style="{DynamicResource EntryButton}" Padding="10,8" HorizontalContentAlignment="Stretch" IsEnabled="{Binding Available}" ToolTip="{Binding HoverDetails}">
              <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="26"/><ColumnDefinition/></Grid.ColumnDefinitions>
              <Grid Width="20" Height="20" VerticalAlignment="Center" HorizontalAlignment="Left">
                <local:EntryIdentity Kind="{Binding IconKind}" Interactive="True" Visibility="{Binding GridIconVisibility}" Width="20" Height="20"/>
                <TextBlock Tag="copy-feedback" Text="&#xE73E;" FontFamily="Segoe MDL2 Assets" FontSize="14" Foreground="#FF99C3A4" Visibility="{Binding CopyVisibility}" HorizontalAlignment="Center" VerticalAlignment="Center" IsHitTestVisible="False"/>
              </Grid>
              <StackPanel Grid.Column="1" VerticalAlignment="Center">
                <TextBlock Text="{Binding Title}" FontSize="12" FontWeight="Medium" TextTrimming="CharacterEllipsis"/>
                <TextBlock Text="{Binding CompactSubtitle}" Visibility="{Binding SubtitleVisibility}" FontSize="11" Foreground="{DynamicResource Muted}" Margin="0,4,0,0" TextTrimming="CharacterEllipsis"/>
                <StackPanel Orientation="Horizontal" Margin="0,4,0,0" Visibility="{Binding LoginVisibility}">
                  <TextBlock Tag="Username" Text="username" FontSize="10" Opacity="{Binding UsernameOpacity}"/>
                  <TextBlock Text=" · " FontSize="10" Opacity=".4"/>
                  <TextBlock Tag="Password" Text="password" FontSize="10" Opacity="{Binding PasswordOpacity}"/>
                </StackPanel>
              </StackPanel>
              </Grid>
            </Button>
            <Grid Grid.Column="1" Width="28" Visibility="{Binding GridSecondaryVisibility}" VerticalAlignment="Center" Margin="0,0,4,0">
              <Button Tag="entry-action" Visibility="{Binding ActionVisibility}" Style="{DynamicResource EntryRunButton}" ToolTip="{Binding ActionLabel}" AutomationProperties.Name="{Binding ActionLabel}" Opacity=".7">
                <local:EntryIdentity Kind="{Binding ActionIconKind}" Interactive="True" Width="20" Height="20"/>
              </Button>
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
