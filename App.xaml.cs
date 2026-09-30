using Microsoft.UI.Xaml;
namespace FreeTV;
public partial class App : Application
{
 private Window? window;
 public App() { InitializeComponent(); UnhandledException += (_, e) => { try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "FreeTV-crash.txt"), e.Exception.ToString()); } catch { } }; }
 protected override void OnLaunched(LaunchActivatedEventArgs args) { window = new MainWindow(); window.Activate(); }
}
