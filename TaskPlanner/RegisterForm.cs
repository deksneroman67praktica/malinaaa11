using System;
using System.Text.RegularExpressions;
using System.Windows.Forms;

public class RegisterForm : Form
{
    TextBox tbLogin, tbFull, tbEmail, tbPass, tbPass2;
    public string RegisteredLogin = "";

    public RegisterForm()
    {
        Text = "Регистрация";
        Width = 430; Height = 310;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;

        tbLogin = Ui.Field(this, "Логин *", 20);
        tbFull = Ui.Field(this, "ФИО *", 55);
        tbEmail = Ui.Field(this, "Email *", 90);
        tbPass = Ui.Field(this, "Пароль *", 125, true);
        tbPass2 = Ui.Field(this, "Повтор пароля *", 160, true);
        tbLogin.MaxLength = 50; tbFull.MaxLength = 100; tbEmail.MaxLength = 100;

        var ok = Ui.Btn(this, "Зарегистрироваться", 120, 205, 160, Register);
        Ui.Btn(this, "Отмена", 290, 205, 100, () => DialogResult = DialogResult.Cancel);
        AcceptButton = ok;
    }

    void Register()
    {
        string login = tbLogin.Text.Trim();
        string full = tbFull.Text.Trim();
        string email = tbEmail.Text.Trim();
        string pass = tbPass.Text;

        if (login.Length < 3) { Ui.Error("Логин должен быть не короче 3 символов."); return; }
        if (full.Length == 0) { Ui.Error("Укажите ФИО."); return; }
        if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) { Ui.Error("Некорректный email. Пример: name@mail.ru"); return; }
        if (pass.Length < 6) { Ui.Error("Пароль должен быть не короче 6 символов."); return; }
        if (pass != tbPass2.Text) { Ui.Error("Пароли не совпадают."); return; }

        bool ok = Ui.Try(() => Db.Exec(
            "INSERT INTO users(login, password_hash, full_name, email, role_id) " +
            "VALUES (@login, @hash, @full, @email, (SELECT role_id FROM roles WHERE name = 'Пользователь'))",
            ("login", login), ("hash", Db.Md5(pass)), ("full", full), ("email", email)));
        if (!ok) return;

        RegisteredLogin = login;
        Ui.Info("Регистрация выполнена. Теперь войдите в систему.");
        DialogResult = DialogResult.OK;
    }
}
