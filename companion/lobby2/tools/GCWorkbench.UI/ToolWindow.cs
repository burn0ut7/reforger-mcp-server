using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace GCWorkbench.UI;

public sealed class ToolWindow : Window
{
    private readonly bool preview;
    private readonly Grid body = new();
    private readonly Dictionary<string, FrameworkElement> inputs = [];
    private readonly Dictionary<string, TextBlock> errors = [];
    private readonly List<(Button Button, DialogSpec Spec)> navigation = [];
    private DialogSpec spec;
    public DialogResult Result { get; private set; } = new("cancel", []);

    public ToolWindow(DialogSpec specification, bool preview = false)
    {
        spec = specification;
        this.preview = preview;
        Title = "GC Workbench";
        Width = preview ? 880 : 680;
        Height = 620;
        MinWidth = preview ? 760 : 540;
        MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 14;
        Background = Brush("#F5F6F8");
        Foreground = Brush("#17212B");
        UseLayoutRounding = true;
        Resources = (ResourceDictionary)XamlReader.Parse(Styles);
        var root = new Grid { Background = Background };
        if (preview)
        {
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            root.ColumnDefinitions.Add(new ColumnDefinition());
            root.Children.Add(PreviewNavigation());
            Grid.SetColumn(body, 1);
        }
        root.Children.Add(body);
        Content = root;
        Render(spec);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }

    private FrameworkElement PreviewNavigation()
    {
        var panel = new StackPanel { Margin = new Thickness(20, 28, 20, 20) };
        panel.Children.Add(Text("GC", 27, "#F2C94C", bold: true));
        panel.Children.Add(Text("WORKBENCH TOOLS", 10, "#B4BDC8", margin: new Thickness(0, 6, 0, 38)));
        foreach (var (name, example) in Examples.All())
        {
            var label = name switch { "dependencies" => "Dependencies", "updates" => "Update mods", _ => "Submit mission" };
            var button = new Button
            {
                Content = label, HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brush("#293440"), Foreground = Brushes.White, BorderBrush = Brush("#3C4753"),
                Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(12)
            };
            button.Click += (_, _) => Render(example);
            navigation.Add((button, example));
            panel.Children.Add(button);
        }
        panel.Children.Add(Text("PREVIEW ONLY", 10, "#F2C94C", margin: new Thickness(0, 32, 0, 8)));
        return new Border { Background = Brush("#1D2732"), Child = panel };
    }

    private void Render(DialogSpec next)
    {
        next.Validate();
        spec = next;
        Resources["GcAccent"] = Brush(spec.Accent);
        foreach (var (button, page) in navigation)
        {
            bool selected = page.Title == spec.Title;
            button.Background = Brush(selected ? "#364658" : "#293440");
            button.BorderBrush = Brush(selected ? spec.Accent : "#3C4753");
            AutomationProperties.SetHelpText(button, selected ? "Current preview" : "Open preview");
        }
        inputs.Clear();
        errors.Clear();
        body.Children.Clear();
        body.RowDefinitions.Clear();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition());
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Margin = new Thickness(32, 30, 32, 22) };
        header.Children.Add(Text(preview ? "GC TOOLS  /  PREVIEW" : "GC TOOLS", 11, "#66717D", bold: true));
        header.Children.Add(Text(spec.Title, 28, "#17212B", bold: true, margin: new Thickness(0, 12, 0, 8)));
        if (spec.Description.Length > 0) header.Children.Add(Text(spec.Description, 14, "#586575"));
        body.Children.Add(header);

        var content = new StackPanel { Margin = new Thickness(32, 0, 32, 24) };
        if (spec.Section.Length > 0) content.Children.Add(Text(spec.Section, 14, "#17212B", bold: true, margin: new Thickness(0, 0, 0, 12)));
        foreach (var item in spec.Items) content.Children.Add(StatusCard(item));
        foreach (var field in spec.Fields) content.Children.Add(Field(field));
        var feedback = Text("", 13, "#276347", margin: new Thickness(0, 14, 0, 0));
        feedback.Visibility = Visibility.Collapsed;
        AutomationProperties.SetLiveSetting(feedback, AutomationLiveSetting.Polite);
        content.Children.Add(feedback);
        var scroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1);
        body.Children.Add(scroll);

        var footer = new StackPanel { Margin = new Thickness(32, 20, 32, 24) };
        if (spec.Note.Length > 0) footer.Children.Add(Text(spec.Note, 12, "#586575", margin: new Thickness(0, 0, 0, 18)));
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = spec.SecondaryAction, MinWidth = 92, Margin = new Thickness(0, 0, 10, 8) };
        cancel.Click += (_, _) => Close();
        var confirm = new Button { Content = spec.PrimaryAction, Background = Brush(spec.Accent), BorderBrush = Brush(spec.Accent), Foreground = Brush("#17212B"), Margin = new Thickness(0, 0, 0, 8) };
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
            Result = new DialogResult("confirm", values);
            Close();
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        footer.Children.Add(buttons);
        var footerBorder = new Border { Background = Brushes.White, BorderBrush = Brush("#DFE4EA"), BorderThickness = new Thickness(0, 1, 0, 0), Child = footer };
        Grid.SetRow(footerBorder, 2);
        body.Children.Add(footerBorder);
    }

    private FrameworkElement StatusCard(StatusItem item)
    {
        var panel = new StackPanel();
        var color = item.State switch { "ready" => "#276347", "error" => "#A73535", "warning" => "#8A600E", _ => "#596879" };
        panel.Children.Add(Text(item.Title, 15, "#17212B", bold: true));
        if (item.Description.Length > 0) panel.Children.Add(Text(item.Description, 13, "#586575", margin: new Thickness(0, 5, 0, 0)));
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
            label.Children.Add(Text(field.Label, 15, "#17212B", bold: true));
            if (field.Help.Length > 0) label.Children.Add(Text(field.Help, 12, "#586575", margin: new Thickness(0, 5, 0, 0)));
            input = new CheckBox { Content = label, IsChecked = field.Checked, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(10, 2, 0, 2) };
            panel.Children.Add(input);
        }
        else
        {
            panel.Children.Add(Text(field.Label + (field.Required ? " *" : ""), 14, "#17212B", bold: true, margin: new Thickness(0, 0, 0, 8)));
            var text = new TextBox { Text = field.Value, Padding = new Thickness(12, 10, 12, 10), BorderBrush = Brush("#BAC4CF"), Background = Brushes.White, BorderThickness = new Thickness(1), MaxLength = 8192 };
            if (field.Kind == "multiline")
            {
                text.AcceptsReturn = true;
                text.TextWrapping = TextWrapping.Wrap;
                text.Height = 106;
                text.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            panel.Children.Add(text);
            input = text;
            if (field.Help.Length > 0) panel.Children.Add(Text(field.Help, 12, "#586575", margin: new Thickness(0, 7, 0, 0)));
        }
        AutomationProperties.SetName(input, field.Label);
        AutomationProperties.SetHelpText(input, field.Help);
        inputs.Add(field.Id, input);
        var error = Text("", 12, "#A73535", margin: new Thickness(0, 8, 0, 0));
        error.Visibility = Visibility.Collapsed;
        errors.Add(field.Id, error);
        panel.Children.Add(error);
        return field.Kind == "check" ? Card(panel) : new Border { Child = panel, Margin = new Thickness(0, 0, 0, 20) };
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
        Child = content, Padding = new Thickness(18, 16, 18, 16), Margin = new Thickness(0, 0, 0, 10),
        Background = Brushes.White, BorderBrush = Brush("#DFE4EA"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8)
    };

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static TextBlock Text(string text, double size, string color, bool bold = false, Thickness margin = default) => new()
    {
        Text = text, FontSize = size, Foreground = Brush(color), TextWrapping = TextWrapping.Wrap,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = margin
    };

    private const string Styles = """
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Style TargetType="TextBox">
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox">
              <Border x:Name="Frame" CornerRadius="6" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}">
                <ScrollViewer x:Name="PART_ContentHost"/>
              </Border>
              <ControlTemplate.Triggers><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Frame" Property="BorderBrush" Value="#355E95"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="CheckBox">
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox">
              <Grid>
                <Grid.ColumnDefinitions><ColumnDefinition Width="22"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                <Border x:Name="Box" Width="20" Height="20" VerticalAlignment="Center" Background="White" BorderBrush="#8998A8" BorderThickness="1" CornerRadius="4">
                  <Path x:Name="Tick" Visibility="Collapsed" Data="M 3,9 L 7,13 L 15,4" Stroke="#17212B" StrokeThickness="2"/>
                </Border>
                <ContentPresenter Grid.Column="1" Margin="12,0,0,0" VerticalAlignment="Center" RecognizesAccessKey="True"/>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property="IsChecked" Value="True"><Setter TargetName="Tick" Property="Visibility" Value="Visible"/><Setter TargetName="Box" Property="Background" Value="{DynamicResource GcAccent}"/><Setter TargetName="Box" Property="BorderBrush" Value="#8998A8"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Box" Property="BorderBrush" Value="#355E95"/><Setter TargetName="Box" Property="BorderThickness" Value="2"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="Button">
            <Setter Property="Padding" Value="18,11"/>
            <Setter Property="MinHeight" Value="42"/>
            <Setter Property="Background" Value="White"/>
            <Setter Property="BorderBrush" Value="#BAC4CF"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
              <Setter.Value><ControlTemplate TargetType="Button">
                <Border x:Name="Frame" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="6" Padding="{TemplateBinding Padding}">
                  <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="Center" RecognizesAccessKey="True"/>
                </Border>
                <ControlTemplate.Triggers>
                  <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Frame" Property="Opacity" Value="0.8"/></Trigger>
                  <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Frame" Property="BorderBrush" Value="#355E95"/><Setter TargetName="Frame" Property="BorderThickness" Value="2"/></Trigger>
                  <Trigger Property="IsEnabled" Value="False"><Setter TargetName="Frame" Property="Opacity" Value="0.45"/></Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate></Setter.Value>
            </Setter>
          </Style>
        </ResourceDictionary>
        """;
}
