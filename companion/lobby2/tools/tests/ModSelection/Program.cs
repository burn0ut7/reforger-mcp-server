using GCWorkbench.UI;
using System.Windows;
using System.Windows.Controls;
static class Checks {
 static IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject {
  if(root is T value)yield return value;
  foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var result in Find<T>(child))yield return result;
 }
 [STAThread] static void Main(){
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  var spec=new DialogSpec{Title="Update mods",PrimaryAction="Update selected",Fields=[new(){Id="lobby",Label="Lobby2",Kind="check",Enabled=false,Checked=true,Help="Up to date"},new(){Id="core",Label="GC Core",Kind="check",Checked=false,Help="Update available"}]};
  var window=new ToolWindow(spec,tools:true,observeStatus:false);
  var checks=Find<CheckBox>(window).ToArray();var update=Find<Button>(window).Single(b=>Equals(b.Content,"Update selected"));
  if(checks[0].IsEnabled||checks[0].IsChecked==true||checks[0].Opacity>=1||update.IsEnabled)throw new Exception("Current mod or empty action enabled");
  checks[1].IsChecked=true;if(!update.IsEnabled)throw new Exception("Available selected mod did not enable update");
  checks[1].IsChecked=false;if(update.IsEnabled)throw new Exception("Empty selection remained enabled");
  checks[0].IsChecked=true;checks[1].IsChecked=true;
  update.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  if(window.Result.Action!="update"||(bool)window.Result.Values["lobby"]||!(bool)window.Result.Values["core"])throw new Exception("Disabled mod leaked into dispatch");
  var setup=new ToolWindow(new DialogSpec{Title="External dependencies",PrimaryAction="Install / update",SecondaryAction="Close"},tools:true,observeStatus:false);
  if(setup.Result.Action!="cancel")throw new Exception("Setup started without user action");
  var busy=(HashSet<string>)typeof(ToolWindow).GetField("checking",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(setup)!;
  busy.Add("External dependencies");
  typeof(ToolWindow).GetMethod("Render",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.DeclaredOnly)!.Invoke(setup,[new DialogSpec{Title="External dependencies",PrimaryAction="Install / update"}]);
  var install=Find<Button>(setup).Single(b=>Equals(b.Content,"Install / update"));
  if(!install.IsEnabled)throw new Exception("Install blocked by background status check");
  install.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  if(setup.Result.Action!="install")throw new Exception("Install action unavailable after notice");
  Console.WriteLine("PASS: current mods gray/unchecked/disabled, button follows eligible selection, disabled mods excluded from dispatch.");
 }
}
