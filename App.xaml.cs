namespace MenuApp;

public partial class App
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        base.OnStartup(e);
    }
}
