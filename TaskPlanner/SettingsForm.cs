using System;
using System.Windows.Forms;

public class SettingsForm : Form
{
    TextBox tbHost, tbPort, tbDb, tbUser, tbPass;
    readonly string[] old;

    public SettingsForm()
    {
        old = new[] { Db.Host, Db.Port, Db.Database, Db.Username, Db.Password };

        Text = "Настройки подключения к базе";
        Width = 430; Height = 300;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;

        tbHost = Ui.Field(this, "Сервер (Host)", 20);
        tbPort = Ui.Field(this, "Порт", 55);
        tbDb = Ui.Field(this, "База данных", 90);
        tbUser = Ui.Field(this, "Пользователь", 125);
        tbPass = Ui.Field(this, "Пароль", 160, true);

        tbHost.Text = Db.Host;
        tbPort.Text = Db.Port;
        tbDb.Text = Db.Database;
        tbUser.Text = Db.Username;
        tbPass.Text = Db.Password;

        Ui.Btn(this, "Проверить", 20, 210, 110, Check);
        Ui.Btn(this, "Сохранить", 140, 210, 110, SaveAndClose);
        Ui.Btn(this, "Отмена", 260, 210, 110, Cancel);
    }

    void Apply()
    {
        Db.Host = tbHost.Text.Trim();
        Db.Port = tbPort.Text.Trim();
        Db.Database = tbDb.Text.Trim();
        Db.Username = tbUser.Text.Trim();
        Db.Password = tbPass.Text;
    }

    void Check()
    {
        Apply();
        Ui.Try(() =>
        {
            int users = Convert.ToInt32(Db.Scalar("SELECT count(*) FROM users"));
            int tasks = Convert.ToInt32(Db.Scalar("SELECT count(*) FROM tasks"));
            Ui.Info("Подключение успешно. Пользователей: " + users + ", задач: " + tasks + ".");
        });
    }

    void SaveAndClose()
    {
        Apply();
        if (Ui.Try(Db.SaveSettings))
            DialogResult = DialogResult.OK;
    }

    void Cancel()
    {
        Db.Host = old[0]; Db.Port = old[1]; Db.Database = old[2]; Db.Username = old[3]; Db.Password = old[4];
        DialogResult = DialogResult.Cancel;
    }
}
