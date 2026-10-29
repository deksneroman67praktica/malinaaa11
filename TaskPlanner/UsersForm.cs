using System;
using System.Data;
using System.Windows.Forms;

public class UsersForm : Form
{
    DataGridView grid;

    public UsersForm()
    {
        Text = "Пользователи (администратор)";
        Width = 800; Height = 500;
        StartPosition = FormStartPosition.CenterScreen;

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(8, 8, 0, 0) };
        grid = Ui.Grid();
        Ui.Btn(bottom, "Сменить роль", 0, 0, 130, ChangeRole);
        Ui.Btn(bottom, "Закрыть", 0, 0, 100, Close);
        Controls.Add(grid);
        Controls.Add(bottom);
        grid.BringToFront();

        Load += (s, e) => Ui.Try(Reload);
    }

    void Reload()
    {
        grid.DataSource = Db.Query(
            "SELECT u.user_id AS \"ID\", u.login AS \"Логин\", u.full_name AS \"ФИО\", " +
            "u.email AS \"Email\", r.name AS \"Роль\" " +
            "FROM users u JOIN roles r ON r.role_id = u.role_id ORDER BY u.user_id");
    }

    void ChangeRole()
    {
        if (grid.CurrentRow == null) { Ui.Info("Выберите пользователя."); return; }
        int id = Convert.ToInt32(grid.CurrentRow.Cells["ID"].Value);
        if (id == Session.UserId) { Ui.Error("Нельзя изменить роль самому себе."); return; }

        Ui.Try(() =>
        {
            var roles = Ui.LoadItems("SELECT role_id, name FROM roles ORDER BY role_id");
            var pick = Ui.Pick("Новая роль", roles);
            if (pick == null) return;
            Db.Exec("UPDATE users SET role_id = @r WHERE user_id = @id", ("r", pick.Id), ("id", id));
            Reload();
        });
    }
}
