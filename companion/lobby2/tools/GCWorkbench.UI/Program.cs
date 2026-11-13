using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GCWorkbench.UI;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var app = new Application();
            if (args is ["--tools"])
            {
                bool missingDependencies = ToolStatus.MissingStartupDependencies();
                var picker = new ToolWindow(ToolPages.Initial(missingDependencies), tools: true, dependenciesMissing: missingDependencies);
                app.Run(picker);
                // Contract with GC_ToolsPlugin: selection only, no operations in this window.
                return picker.Result.Action switch { "submit" => 10, "update" => 100 + ToolPages.ModMask(picker.Result.Values), "install" => 12, _ => 2 };
            }
            if (args is ["--preview"])
            {
                app.Run(new ToolWindow(Examples.Dependencies(), preview: true));
                return 0;
            }
            if (args is ["--render" or "--render-tools", var folder] && Path.IsPathFullyQualified(folder))
            {
                Directory.CreateDirectory(folder);
                foreach (var (name, spec) in (args[0] == "--render-tools" ? ToolPages.All() : Examples.All()))
                {
                    var window = new ToolWindow(spec, preview: args[0] == "--render", tools: args[0] == "--render-tools");
                    var content = (FrameworkElement)window.Content;
                    window.Content = null;
                    content.Resources = window.Resources;
                    System.Windows.Documents.TextElement.SetFontFamily(content, window.FontFamily);
                    System.Windows.Documents.TextElement.SetFontSize(content, window.FontSize);
                    content.Measure(new Size(880, 560));
                    content.Arrange(new Rect(0, 0, 880, 560));
                    content.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(880, 560, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(content);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(folder, name + ".png"));
                    encoder.Save(file);
                }
                return 0;
            }
            if (args is not ["--request", var request, "--result", var result] ||
                !Path.IsPathFullyQualified(request) || !Path.IsPathFullyQualified(result))
                throw new ArgumentException("Use --preview, or --request <absolute JSON path> --result <new absolute JSON path>.");
            if (new FileInfo(request).Length > 128 * 1024) throw new ArgumentException("Dialog request is too large.");
            if (File.Exists(result)) throw new IOException("The result file already exists.");
            var specFromFile = JsonSerializer.Deserialize<DialogSpec>(File.ReadAllText(request), Json)
                ?? throw new ArgumentException("Missing dialog request.");
            var dialog = new ToolWindow(specFromFile);
            app.Run(dialog);
            using var output = new FileStream(result, FileMode.CreateNew, FileAccess.Write);
            JsonSerializer.Serialize(output, dialog.Result, Json);
            return dialog.Result.Action == "cancel" ? 2 : 0;
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "GC Workbench", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }
}
