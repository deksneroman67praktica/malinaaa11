using System;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        while (true)
        {
            using (var login = new LoginForm())
            {
                if (login.ShowDialog() != DialogResult.OK) return;
            }
            using (var main = new MainForm())
            {
                main.ShowDialog();
                if (!main.LoggedOut) return;
            }
        }
    }
}
