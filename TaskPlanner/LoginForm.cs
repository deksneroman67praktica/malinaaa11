using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

public class LoginForm : Form
{
    TextBox tbLogin, tbPass;

    public LoginForm()
    {
        Text = "Планировщик задач — вход";
        Width = 430; Height = 260;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        Controls.Add(new Label
        {
            Text = "Вход в систему", Left = 20, Top = 15, Width = 300, Height = 25,
            Font = new Font(Font.FontFamily, 12, FontStyle.Bold)
        });
        tbLogin = Ui.Field(this, "Логин", 60);
        tbPass = Ui.Field(this, "Пароль", 95, true);
        var enter = Ui.Btn(this, "Войти", 150, 135, 100, DoLogin);
        Ui.Btn(this, "Регистрация", 260, 135, 130, DoRegister);
        Ui.Btn(this, "Настройки", 20, 135, 120, OpenSettings);
        AcceptButton = enter;
        Shown += (s, e) => { if (!Db.HasSettings) OpenSettings(); };
    }

    void DoLogin()
    {
        string login = tbLogin.Text.Trim();
        string pass = tbPass.Text;
        if (login.Length == 0 || pass.Length == 0)
        {
            Ui.Error("Введите логин и пароль.");
            return;
        }

        DataTable dt = null;
        bool ok = Ui.Try(() => dt = Db.Query(
            "SELECT u.user_id, u.full_name, r.name AS role " +
            "FROM users u JOIN roles r ON r.role_id = u.role_id " +
            "WHERE u.login = @login AND u.password_hash = @hash",
            ("login", login), ("hash", Db.Md5(pass))));
        if (!ok) return;

        if (dt.Rows.Count == 0)
        {
            Ui.Error("Неверный логин или пароль.");
            return;
        }

        Session.UserId = Convert.ToInt32(dt.Rows[0]["user_id"]);
        Session.FullName = Convert.ToString(dt.Rows[0]["full_name"]);
        Session.Role = Convert.ToString(dt.Rows[0]["role"]);
        DialogResult = DialogResult.OK;
    }

    void OpenSettings()
    {
        using var f = new SettingsForm();
        f.ShowDialog(this);
    }

    void DoRegister()
    {
        using var f = new RegisterForm();
        if (f.ShowDialog() == DialogResult.OK)
            tbLogin.Text = f.RegisteredLogin;
    }
}
