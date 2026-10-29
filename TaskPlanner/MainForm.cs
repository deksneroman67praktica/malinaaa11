using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;

public class MainForm : Form
{
    public bool LoggedOut;

    TextBox tbSearch;
    ComboBox cbCat, cbPri, cbStat, cbSort1, cbSort2;
    DataGridView grid;
    Label lblCount;
    DataTable table;

    static readonly Dictionary<string, string> Sorts = new Dictionary<string, string>
    {
        ["Срок: раньше"] = "t.due_date ASC NULLS LAST",
        ["Срок: позже"] = "t.due_date DESC NULLS LAST",
        ["Приоритет: высокий сначала"] = "t.priority_id DESC",
        ["Название: А–Я"] = "t.title ASC",
        ["Новые сначала"] = "t.created_at DESC",
        ["Статус"] = "t.status_id ASC"
    };

    public MainForm()
    {
        Text = "Планировщик задач — " + Session.FullName + " (" + Session.Role + ")";
        Width = 1100; Height = 650;
        StartPosition = FormStartPosition.CenterScreen;

        var top = new Panel { Dock = DockStyle.Top, Height = 105 };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(8, 8, 0, 0) };
        grid = Ui.Grid();

        tbSearch = new TextBox();
        cbCat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cbPri = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cbStat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cbSort1 = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cbSort2 = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

        Place(top, "Поиск (название, описание, автор)", tbSearch, 10, 8, 240);
        Place(top, "Категория", cbCat, 260, 8, 150);
        Place(top, "Приоритет", cbPri, 420, 8, 130);
        Place(top, "Статус", cbStat, 560, 8, 130);
        Place(top, "Сортировка 1", cbSort1, 10, 52, 240);
        Place(top, "Сортировка 2", cbSort2, 260, 52, 240);
        Ui.Btn(top, "Найти", 510, 68, 90, () => Ui.Try(Reload));
        Ui.Btn(top, "Сбросить", 610, 68, 90, ResetFilters);
        lblCount = new Label { Left = 720, Top = 75, Width = 250, Height = 20 };
        top.Controls.Add(lblCount);

        tbSearch.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Ui.Try(Reload); }
        };

        string role = Session.Role;
        var bCreate = Ui.Btn(bottom, "Создать", 0, 0, 100, Create);
        var bEdit = Ui.Btn(bottom, "Изменить", 0, 0, 100, Edit);
        var bDelete = Ui.Btn(bottom, "Удалить", 0, 0, 100, Delete);
        var bAccept = Ui.Btn(bottom, "Принять", 0, 0, 100, Accept);
        var bStatus = Ui.Btn(bottom, "Сменить статус", 0, 0, 130, ChangeStatus);
        Ui.Btn(bottom, "История", 0, 0, 100, History);
        Ui.Btn(bottom, "Отчёт Excel", 0, 0, 110, Report);
        var bUsers = Ui.Btn(bottom, "Пользователи", 0, 0, 120, OpenUsers);
        Ui.Btn(bottom, "Выйти", 0, 0, 90, () => { LoggedOut = true; Close(); });

        bCreate.Visible = role != "Менеджер";
        bEdit.Visible = role != "Менеджер";
        bDelete.Visible = role != "Менеджер";
        bAccept.Visible = role == "Менеджер";
        bStatus.Visible = role != "Пользователь";
        bUsers.Visible = role == "Администратор";

        Controls.Add(grid);
        Controls.Add(top);
        Controls.Add(bottom);
        grid.BringToFront();

        Load += (s, e) => Ui.Try(Init);
    }

    static void Place(Panel p, string label, Control c, int x, int y, int w)
    {
        p.Controls.Add(new Label { Text = label, Left = x, Top = y, Width = w, Height = 18 });
        c.Left = x; c.Top = y + 18; c.Width = w;
        p.Controls.Add(c);
    }

    void FillCombo(ComboBox cb, string sql)
    {
        cb.Items.Clear();
        cb.Items.Add(new Item(0, "Все"));
        foreach (var it in Ui.LoadItems(sql)) cb.Items.Add(it);
        cb.SelectedIndex = 0;
    }

    void Init()
    {
        FillCombo(cbCat, "SELECT category_id, name FROM categories ORDER BY name");
        FillCombo(cbPri, "SELECT priority_id, name FROM priorities ORDER BY priority_id");
        FillCombo(cbStat, "SELECT status_id, name FROM statuses ORDER BY status_id");
        foreach (var cb in new[] { cbSort1, cbSort2 })
        {
            cb.Items.Add("— нет —");
            foreach (var k in Sorts.Keys) cb.Items.Add(k);
            cb.SelectedIndex = 0;
        }
        cbSort1.SelectedItem = "Новые сначала";
        Reload();
    }

    void ResetFilters()
    {
        tbSearch.Text = "";
        cbCat.SelectedIndex = 0;
        cbPri.SelectedIndex = 0;
        cbStat.SelectedIndex = 0;
        cbSort1.SelectedItem = "Новые сначала";
        cbSort2.SelectedIndex = 0;
        Ui.Try(Reload);
    }

    void Reload()
    {
        var where = new List<string>();
        var ps = new List<(string, object)>();

        if (Session.Role == "Администратор")
            where.Add("TRUE");
        else if (Session.Role == "Менеджер")
        {
            where.Add("(t.status_id = 1 OR t.manager_id = @me)");
            ps.Add(("me", Session.UserId));
        }
        else
        {
            where.Add("t.author_id = @me");
            ps.Add(("me", Session.UserId));
        }

        string search = tbSearch.Text.Trim();
        if (search.Length > 0)
        {
            where.Add("(t.title ILIKE @search OR t.description ILIKE @search OR a.full_name ILIKE @search)");
            ps.Add(("search", "%" + search + "%"));
        }
        if (cbCat.SelectedItem is Item ci && ci.Id != 0) { where.Add("t.category_id = @cat"); ps.Add(("cat", ci.Id)); }
        if (cbPri.SelectedItem is Item pi && pi.Id != 0) { where.Add("t.priority_id = @pri"); ps.Add(("pri", pi.Id)); }
        if (cbStat.SelectedItem is Item si && si.Id != 0) { where.Add("t.status_id = @st"); ps.Add(("st", si.Id)); }

        var order = new List<string>();
        foreach (var cb in new[] { cbSort1, cbSort2 })
            if (cb.SelectedItem is string key && Sorts.ContainsKey(key)) order.Add(Sorts[key]);
        order.Add("t.task_id");

        string sql =
            "SELECT t.task_id AS \"ID\", t.title AS \"Название\", c.name AS \"Категория\", " +
            "p.name AS \"Приоритет\", s.name AS \"Статус\", t.due_date AS \"Срок\", " +
            "a.full_name AS \"Автор\", m.full_name AS \"Менеджер\" " +
            "FROM tasks t " +
            "JOIN categories c ON c.category_id = t.category_id " +
            "JOIN priorities p ON p.priority_id = t.priority_id " +
            "JOIN statuses s ON s.status_id = t.status_id " +
            "JOIN users a ON a.user_id = t.author_id " +
            "LEFT JOIN users m ON m.user_id = t.manager_id " +
            "WHERE " + string.Join(" AND ", where) +
            " ORDER BY " + string.Join(", ", order);

        table = Db.Query(sql, ps.ToArray());
        grid.DataSource = table;
        if (grid.Columns["Срок"] != null)
            grid.Columns["Срок"].DefaultCellStyle.Format = "dd.MM.yyyy";
        lblCount.Text = "Найдено задач: " + table.Rows.Count;
    }

    int? SelectedId()
    {
        if (grid.CurrentRow == null)
        {
            Ui.Info("Выберите задачу в таблице.");
            return null;
        }
        return Convert.ToInt32(grid.CurrentRow.Cells["ID"].Value);
    }

    void Create()
    {
        Ui.Try(() =>
        {
            using var f = new TaskEditForm(null);
            if (f.ShowDialog(this) == DialogResult.OK) Reload();
        });
    }

    void Edit()
    {
        var id = SelectedId();
        if (id == null) return;
        if (Session.Role == "Пользователь" && Convert.ToString(grid.CurrentRow.Cells["Статус"].Value) != "Новая")
        {
            Ui.Error("Изменить можно только новую задачу, которую ещё не принял менеджер.");
            return;
        }
        Ui.Try(() =>
        {
            using var f = new TaskEditForm(id);
            if (f.ShowDialog(this) == DialogResult.OK) Reload();
        });
    }

    void Delete()
    {
        var id = SelectedId();
        if (id == null) return;
        if (!Ui.Confirm("Удалить задачу №" + id + "?")) return;
        Ui.Try(() =>
        {
            int n = Db.Exec(
                "DELETE FROM tasks WHERE task_id = @id AND (@admin OR (author_id = @me AND status_id = 1))",
                ("id", id.Value), ("admin", Session.Role == "Администратор"), ("me", Session.UserId));
            if (n == 0)
                throw new UserMessageException("Удалить можно только свою новую задачу.");
            Reload();
        });
    }

    void Accept()
    {
        var id = SelectedId();
        if (id == null) return;
        Ui.Try(() =>
        {
            using var c = Db.Open();
            using var tx = c.BeginTransaction();
            int n = Db.Exec(c, tx,
                "UPDATE tasks SET manager_id = @me, status_id = 2 WHERE task_id = @id AND status_id = 1",
                ("me", Session.UserId), ("id", id.Value));
            if (n == 0)
                throw new UserMessageException("Задача уже принята.");
            Db.Exec(c, tx,
                "INSERT INTO task_history(user_id, task_id, old_status_id, new_status_id) VALUES (@me, @id, 1, 2)",
                ("me", Session.UserId), ("id", id.Value));
            tx.Commit();
            Ui.Info("Задача принята.");
            Reload();
        });
    }

    void ChangeStatus()
    {
        var id = SelectedId();
        if (id == null) return;
        Ui.Try(() =>
        {
            var items = Ui.LoadItems("SELECT status_id, name FROM statuses WHERE name <> 'Новая' ORDER BY status_id");
            var pick = Ui.Pick("Новый статус", items);
            if (pick == null) return;

            using var c = Db.Open();
            using var tx = c.BeginTransaction();
            var dt = Db.Query(c, tx, "SELECT status_id, manager_id FROM tasks WHERE task_id = @id FOR UPDATE", ("id", id.Value));
            if (dt.Rows.Count == 0)
                throw new UserMessageException("Задача не найдена.");

            int old = Convert.ToInt32(dt.Rows[0]["status_id"]);
            bool mine = dt.Rows[0]["manager_id"] != DBNull.Value &&
                        Convert.ToInt32(dt.Rows[0]["manager_id"]) == Session.UserId;
            if (Session.Role != "Администратор" && !mine)
                throw new UserMessageException("Менять статус можно только у задач, которые вы приняли.");
            if (old == pick.Id)
                throw new UserMessageException("У задачи уже установлен этот статус.");

            Db.Exec(c, tx, "UPDATE tasks SET status_id = @st WHERE task_id = @id", ("st", pick.Id), ("id", id.Value));
            Db.Exec(c, tx,
                "INSERT INTO task_history(user_id, task_id, old_status_id, new_status_id) VALUES (@me, @id, @old, @new)",
                ("me", Session.UserId), ("id", id.Value), ("old", old), ("new", pick.Id));
            tx.Commit();
            Reload();
        });
    }

    void History()
    {
        var id = SelectedId();
        if (id == null) return;
        Ui.Try(() =>
        {
            var dt = Db.Query(
                "SELECT h.changed_at AS \"Дата\", u.full_name AS \"Кто изменил\", " +
                "COALESCE(os.name, '—') AS \"Был статус\", ns.name AS \"Стал статус\" " +
                "FROM task_history h " +
                "JOIN users u ON u.user_id = h.user_id " +
                "LEFT JOIN statuses os ON os.status_id = h.old_status_id " +
                "JOIN statuses ns ON ns.status_id = h.new_status_id " +
                "WHERE h.task_id = @id ORDER BY h.changed_at, h.history_id",
                ("id", id.Value));
            Ui.ShowTable("История задачи №" + id, dt);
        });
    }

    void Report()
    {
        if (table == null || table.Rows.Count == 0)
        {
            Ui.Info("Нет данных для отчёта.");
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = "Отчёт по задачам " + DateTime.Now.ToString("yyyy-MM-dd") + ".xlsx"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        Ui.Try(() =>
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Задачи");
            for (int c = 0; c < table.Columns.Count; c++)
            {
                ws.Cell(1, c + 1).Value = table.Columns[c].ColumnName;
                ws.Cell(1, c + 1).Style.Font.Bold = true;
            }
            for (int r = 0; r < table.Rows.Count; r++)
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    object v = table.Rows[r][c];
                    if (v == DBNull.Value) continue;
                    string text = v is DateTime d ? d.ToString("dd.MM.yyyy") : v is DateOnly dOnly ? dOnly.ToString("dd.MM.yyyy") : Convert.ToString(v);
                    ws.Cell(r + 2, c + 1).Value = text;
                }

            var st = wb.Worksheets.Add("Статистика");
            st.Cell(1, 1).Value = "Статус";
            st.Cell(1, 2).Value = "Количество задач";
            st.Cell(1, 1).Style.Font.Bold = true;
            st.Cell(1, 2).Style.Font.Bold = true;
            int row = 2;
            foreach (var g in table.AsEnumerable()
                         .GroupBy(x => Convert.ToString(x["Статус"]))
                         .OrderByDescending(grp => grp.Count()))
            {
                st.Cell(row, 1).Value = g.Key;
                st.Cell(row, 2).Value = g.Count();
                row++;
            }

            ws.Columns().AdjustToContents();
            st.Columns().AdjustToContents();
            wb.SaveAs(dlg.FileName);
            Ui.Info("Отчёт сохранён.");
        });
    }

    void OpenUsers()
    {
        using var f = new UsersForm();
        f.ShowDialog(this);
    }
}
