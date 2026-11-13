using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shell;

namespace GCWorkbench.UI;

public sealed class ToolWindow : Window
{
    private readonly bool preview;
    private readonly bool tools;
    private readonly Dictionary<string, Dictionary<string, object>> drafts = [];
    private readonly Grid body = new();
    private readonly Dictionary<string, FrameworkElement> inputs = [];
    private readonly Dictionary<string, TextBlock> errors = [];
    private readonly List<(Button Button, DialogSpec Spec)> navigation = [];
    private DialogSpec spec;
    public DialogResult Result { get; private set; } = new("cancel", []);

    public ToolWindow(DialogSpec specification, bool preview = false, bool tools = false)
    {
        spec = specification;
        this.preview = preview;
        this.tools = tools;
        Title = "GC Workbench";
        Width = preview || tools ? 880 : 680;
        Height = 560;
        MinWidth = preview || tools ? 760 : 540;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 14;
        Background = Brush("#191e18");
        Foreground = Brush("#f0e9d5");
        UseLayoutRounding = true;
        Resources = (ResourceDictionary)XamlReader.Parse(Styles);
        WindowStyle = WindowStyle.None;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 40, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0) });
        var shell = new Grid { Background = Background };
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        shell.RowDefinitions.Add(new RowDefinition());
        var chrome = new DockPanel { LastChildFill = false, Background = Background };
        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (label, name, action) in new (string, string, Action)[] {
            ("\u2212", "Minimize", () => WindowState = WindowState.Minimized),
            ("\u25a1", "Maximize or restore", () => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized),
            ("\u00d7", "Close", Close) })
        {
            var control = new Button { Content = label, Width = 44, MinHeight = 0, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 18 };
            AutomationProperties.SetName(control, name);
            WindowChrome.SetIsHitTestVisibleInChrome(control, true);
            control.Click += (_, _) => action();
            controls.Children.Add(control);
        }
        DockPanel.SetDock(controls, Dock.Right);
        chrome.Children.Add(controls);
        var brand = new Border
        {
            Width = preview || tools ? 216 : 200, Background = preview || tools ? Brush("#242b20") : Background,
            Child = Text(preview ? "GC TOOLS / PREVIEW" : "GC TOOLS", 12, "#f0e9d5", bold: true, margin: new Thickness(24, 12, 0, 0))
        };
        DockPanel.SetDock(brand, Dock.Left);
        chrome.Children.Add(brand);
        shell.Children.Add(chrome);
        var root = new Grid { Background = Background };
        Grid.SetRow(root, 1);
        shell.Children.Add(root);
        if (preview || tools)
        {
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(216) });
            root.ColumnDefinitions.Add(new ColumnDefinition());
            root.Children.Add(PreviewNavigation());
            Grid.SetColumn(body, 1);
        }
        root.Children.Add(body);
        Content = shell;
        Render(spec);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }

    private FrameworkElement PreviewNavigation()
    {
        var panel = new StackPanel { Margin = new Thickness(12, 24, 12, 16) };
        foreach (var (name, example) in (tools ? ToolPages.All() : Examples.All()))
        {
            var label = name switch { "dependencies" => "Dependencies", "updates" => "Update mods", _ => "Submit mission" };
            var button = new Button
            {
                Content = label, HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent, Foreground = Brush("#f0e9d5"), BorderBrush = Brushes.Transparent,
                Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(12, 14, 12, 14)
            };
            button.Click += (_, _) => { SaveDraft(); Render(example); };
            navigation.Add((button, example));
            panel.Children.Add(button);
        }
        return new Border { Background = Brush("#242b20"), Child = panel };
    }

    private void Render(DialogSpec next)
    {
        next.Validate();
        spec = next;
        Resources["GcAccent"] = Brush(spec.Accent);
        foreach (var (button, page) in navigation)
        {
            bool selected = page.Title == spec.Title;
            button.Background = Brush(selected ? "#191e18" : "#242b20");
            button.BorderBrush = selected ? Brush(spec.Accent) : Brushes.Transparent;
            button.BorderThickness = new Thickness(3, 0, 0, 0);
            AutomationProperties.SetHelpText(button, selected ? "Selected" : "");
        }
        inputs.Clear();
        errors.Clear();
        body.Children.Clear();
        body.RowDefinitions.Clear();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition());
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Margin = new Thickness(24, 24, 24, 16) };
        header.Children.Add(Text(spec.Title, 28, "#f0e9d5", bold: true, margin: new Thickness(0, 0, 0, 8)));
        if (spec.Description.Length > 0) header.Children.Add(Text(spec.Description, 14, "#a9b09b"));
        body.Children.Add(header);

        var content = new StackPanel { Margin = new Thickness(24, 0, 24, 16) };
        if (spec.Section.Length > 0) content.Children.Add(Text(spec.Section, 14, "#f0e9d5", bold: true, margin: new Thickness(0, 0, 0, 12)));
        foreach (var item in spec.Items) content.Children.Add(StatusCard(item));
        foreach (var field in spec.Fields)
        {
            var restored = field;
            if (drafts.TryGetValue(spec.Title, out var draft) && draft.TryGetValue(field.Id, out var value))
                restored = field.Kind == "check" ? field with { Checked = (bool)value } : field with { Value = (string)value };
            content.Children.Add(Field(restored));
        }
        var feedback = Text("", 13, "#a9b09b", margin: new Thickness(0, 14, 0, 0));
        feedback.Visibility = Visibility.Collapsed;
        AutomationProperties.SetLiveSetting(feedback, AutomationLiveSetting.Polite);
        content.Children.Add(feedback);
        var scroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1);
        body.Children.Add(scroll);

        var footer = new StackPanel { Margin = new Thickness(24, 16, 24, 24) };
        if (spec.Note.Length > 0) footer.Children.Add(Text(spec.Note, 12, "#a9b09b", margin: new Thickness(0, 0, 0, 18)));
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = spec.SecondaryAction, MinWidth = 92, Margin = new Thickness(0, 0, 10, 0) };
        cancel.Click += (_, _) => Close();
        var confirm = new Button { Content = spec.PrimaryAction, Background = Brush(spec.Accent), BorderBrush = Brush(spec.Accent), Foreground = Brush("#1b2119"), Margin = new Thickness(0) };
        confirm.Click += (_, _) =>
        {
            var values = ReadValues();
            if (values is null) return;
            if (preview)
            {
                feedback.Text = "Preview complete. No changes made.";
                feedback.Visibility = Visibility.Visible;
                feedback.BringIntoView();
                return;
            }
            if (tools && spec.Title == "Update mods" && !values.Values.OfType<bool>().Any(value => value))
            {
                feedback.Text = "Select at least one mod.";
                feedback.Visibility = Visibility.Visible;
                return;
            }
            Result = new DialogResult(tools ? ToolPages.Action(spec.Title) : "confirm", values);
            Close();
        };
        if (spec.SecondaryAction.Length > 0) buttons.Children.Add(cancel);
        foreach (var action in spec.Actions)
        {
            var button = new Button { Content = action.Label, Margin = new Thickness(0, 0, 10, 0) };
            button.Click += (_, _) => { var values = ReadValues(); if (values is null) return; Result = new DialogResult(action.Id, values); Close(); };
            buttons.Children.Add(button);
        }
        if (spec.Actions.Count == 0) buttons.Children.Add(confirm);
        footer.Children.Add(buttons);
        var footerBorder = new Border { Background = Background, Child = footer };
        Grid.SetRow(footerBorder, 2);
        body.Children.Add(footerBorder);
    }

    private FrameworkElement StatusCard(StatusItem item)
    {
        var panel = new StackPanel();
        var color = item.State switch { "ready" => "#a9b09b", "error" => "#e5a198", "warning" => "#d4b874", _ => "#a9b09b" };
        panel.Children.Add(Text(item.Title, 15, "#f0e9d5", bold: true));
        if (item.Description.Length > 0) panel.Children.Add(Text(item.Description, 13, "#a9b09b", margin: new Thickness(0, 5, 0, 0)));
        panel.Children.Add(Text(item.Status, 12, color, bold: true, margin: new Thickness(0, 8, 0, 0)));
        return Card(panel);
    }

    private FrameworkElement Field(InputField field)
    {
        var panel = new StackPanel();
        FrameworkElement input;
        if (field.Kind == "check")
        {
            var label = new StackPanel();
            label.Children.Add(Text(field.Label, 15, "#f0e9d5", bold: true));
            if (field.Help.Length > 0) label.Children.Add(Text(field.Help, 12, "#a9b09b", margin: new Thickness(0, 5, 0, 0)));
            input = new CheckBox { Content = label, IsChecked = field.Checked, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(10, 2, 0, 2) };
            panel.Children.Add(input);
        }
        else
        {
            panel.Children.Add(Text(field.Label + (field.Required ? " *" : ""), 14, "#f0e9d5", bold: true, margin: new Thickness(0, 0, 0, 8)));
            var text = new TextBox { Text = field.Value, Foreground = Brush("#f0e9d5"), CaretBrush = Brush("#f0e9d5"), Padding = new Thickness(12, 10, 12, 10), BorderBrush = Brush("#404735"), Background = Brush("#242b20"), BorderThickness = new Thickness(1), MaxLength = 8192 };
            text.IsReadOnly = field.ReadOnly;
            if (field.Kind == "multiline")
            {
                text.AcceptsReturn = true;
                text.TextWrapping = TextWrapping.Wrap;
                text.Height = 106;
                text.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            panel.Children.Add(text);
            input = text;
            if (field.Help.Length > 0) panel.Children.Add(Text(field.Help, 12, "#a9b09b", margin: new Thickness(0, 7, 0, 0)));
        }
        AutomationProperties.SetName(input, field.Label);
        AutomationProperties.SetHelpText(input, field.Help);
        inputs.Add(field.Id, input);
        var error = Text("", 12, "#e5a198", margin: new Thickness(0, 8, 0, 0));
        error.Visibility = Visibility.Collapsed;
        errors.Add(field.Id, error);
        panel.Children.Add(error);
        return field.Kind == "check" ? Card(panel) : new Border { Child = panel, Margin = new Thickness(0, 0, 0, 20) };
    }

    private void SaveDraft()
    {
        drafts[spec.Title] = inputs.ToDictionary(pair => pair.Key, pair => pair.Value is CheckBox check ? (object)(check.IsChecked == true) : ((TextBox)pair.Value).Text);
    }

    private Dictionary<string, object>? ReadValues()
    {
        var values = new Dictionary<string, object>();
        FrameworkElement? firstInvalid = null;
        foreach (var field in spec.Fields)
        {
            var control = inputs[field.Id];
            object value = control is CheckBox check ? check.IsChecked == true : ((TextBox)control).Text.Trim();
            values[field.Id] = value;
            bool missing = field.Required && (value is bool flag ? !flag : string.IsNullOrWhiteSpace((string)value));
            errors[field.Id].Text = missing ? "Please complete this field." : "";
            errors[field.Id].Visibility = missing ? Visibility.Visible : Visibility.Collapsed;
            if (missing) firstInvalid ??= control;
        }
        if (firstInvalid is null) return values;
        firstInvalid.Focus();
        firstInvalid.BringIntoView();
        return null;
    }

    private static Border Card(UIElement content) => new()
    {
        Child = content, Padding = new Thickness(0, 16, 0, 16),
        Background = Brushes.Transparent, BorderBrush = Brush("#404735"), BorderThickness = new Thickness(0, 0, 0, 1)
    };

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static TextBlock Text(string text, double size, string color, bool bold = false, Thickness margin = default) => new()
    {
        Text = text, FontSize = size, Foreground = Brush(color), TextWrapping = TextWrapping.Wrap,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = margin
    };

    private const string Styles = """
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Style TargetType="ScrollBar">
            <Setter Property="Width" Value="10"/>
            <Setter Property="Background" Value="#191e18"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar">
              <Grid Background="{TemplateBinding Background}">
                <Track x:Name="PART_Track" IsDirectionReversed="True" Orientation="{TemplateBinding Orientation}">
                  <Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False"/></Track.DecreaseRepeatButton>
                  <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="#404735" CornerRadius="3" Margin="2"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                  <Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False"/></Track.IncreaseRepeatButton>
                </Track>
              </Grid>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="TextBox">
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox">
              <Border x:Name="Frame" CornerRadius="3" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}">
                <ScrollViewer x:Name="PART_ContentHost"/>
              </Border>
              <ControlTemplate.Triggers><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Frame" Property="BorderBrush" Value="#d4b874"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="CheckBox">
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox">
              <Grid>
                <Grid.ColumnDefinitions><ColumnDefinition Width="22"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                <Border x:Name="Box" Width="20" Height="20" VerticalAlignment="Center" Background="#242b20" BorderBrush="#a9b09b" BorderThickness="1" CornerRadius="4">
                  <Path x:Name="Tick" Visibility="Collapsed" Data="M 3,9 L 7,13 L 15,4" Stroke="#1b2119" StrokeThickness="2"/>
                </Border>
                <ContentPresenter Grid.Column="1" Margin="12,0,0,0" VerticalAlignment="Center" RecognizesAccessKey="True"/>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property="IsChecked" Value="True"><Setter TargetName="Tick" Property="Visibility" Value="Visible"/><Setter TargetName="Box" Property="Background" Value="{DynamicResource GcAccent}"/><Setter TargetName="Box" Property="BorderBrush" Value="#a9b09b"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Box" Property="BorderBrush" Value="#d4b874"/><Setter TargetName="Box" Property="BorderThickness" Value="2"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="Button">
            <Setter Property="Foreground" Value="#f0e9d5"/>
            <Setter Property="Padding" Value="18,11"/>
            <Setter Property="MinHeight" Value="42"/>
            <Setter Property="Background" Value="#242b20"/>
            <Setter Property="BorderBrush" Value="#404735"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
              <Setter.Value><ControlTemplate TargetType="Button">
                <Border x:Name="Frame" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="3" Padding="{TemplateBinding Padding}">
                  <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="Center" RecognizesAccessKey="True"/>
                </Border>
                <ControlTemplate.Triggers>
                  <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Frame" Property="Opacity" Value="0.8"/></Trigger>
                  <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Frame" Property="BorderBrush" Value="#d4b874"/></Trigger>
                  <Trigger Property="IsEnabled" Value="False"><Setter TargetName="Frame" Property="Opacity" Value="0.45"/></Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate></Setter.Value>
            </Setter>
          </Style>
        </ResourceDictionary>
        """;
}
