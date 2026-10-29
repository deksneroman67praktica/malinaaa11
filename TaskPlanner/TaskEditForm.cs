using System;
using System.Data;
using System.Windows.Forms;

public class TaskEditForm : Form
{
    readonly int? taskId;
    TextBox tbId, tbTitle, tbDesc;
    DateTimePicker dtpDue;
    ComboBox cbCat, cbPri;

    public TaskEditForm(int? id)
    {
        taskId = id;
        Text = id == null ? "Новая задача" : "Изменение задачи";
        Width = 450; Height = 390;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;

        tbId = Ui.Field(this, "ID", 20);
        tbId.ReadOnly = true;
        tbId.Text = id.HasValue ? id.Value.ToString() : "(автоматически)";

        tbTitle = Ui.Field(this, "Название *", 55);
        tbTitle.MaxLength = 100;

        Controls.Add(new Label { Text = "Описание", Left = 20, Top = 90, Width = 130, Height = 20 });
        tbDesc = new TextBox { Left = 150, Top = 87, Width = 240, Height = 80, Multiline = true, MaxLength = 500 };
        Controls.Add(tbDesc);

        Controls.Add(new Label { Text = "Срок (по желанию)", Left = 20, Top = 185, Width = 130, Height = 20 });
        dtpDue = new DateTimePicker { Left = 150, Top = 182, Width = 240, Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
        Controls.Add(dtpDue);

        cbCat = Ui.Combo(this, "Категория *", 225);
        cbPri = Ui.Combo(this, "Приоритет *", 260);
        cbCat.Items.AddRange(Ui.LoadItems("SELECT category_id, name FROM categories ORDER BY name").ToArray());
        cbPri.Items.AddRange(Ui.LoadItems("SELECT priority_id, name FROM priorities ORDER BY priority_id").ToArray());

        if (id == null)
        {
            if (cbCat.Items.Count > 0) cbCat.SelectedIndex = 0;
            if (cbPri.Items.Count > 0) cbPri.SelectedIndex = 0;
        }
        else
        {
            DataTable dt = Db.Query(
                "SELECT title, description, due_date, category_id, priority_id FROM tasks WHERE task_id = @id",
                ("id", id.Value));
            if (dt.Rows.Count == 0) throw new UserMessageException("Задача не найдена.");
            DataRow r = dt.Rows[0];
            tbTitle.Text = Convert.ToString(r["title"]);
            tbDesc.Text = r["description"] == DBNull.Value ? "" : Convert.ToString(r["description"]);
            if (r["due_date"] != DBNull.Value)
            {
                dtpDue.Value = r["due_date"] is DateOnly dOnly ? dOnly.ToDateTime(TimeOnly.MinValue) : Convert.ToDateTime(r["due_date"]);
                dtpDue.Checked = true;
            }
            SelectId(cbCat, Convert.ToInt32(r["category_id"]));
            SelectId(cbPri, Convert.ToInt32(r["priority_id"]));
        }

        Ui.Btn(this, "Сохранить", 150, 305, 110, Save);
        Ui.Btn(this, "Отмена", 270, 305, 110, () => DialogResult = DialogResult.Cancel);
    }

    static void SelectId(ComboBox cb, int id)
    {
        foreach (Item it in cb.Items)
            if (it.Id == id) { cb.SelectedItem = it; return; }
    }

    void Save()
    {
        string title = tbTitle.Text.Trim();
        if (title.Length == 0) { Ui.Error("Введите название задачи."); return; }
        if (cbCat.SelectedItem == null || cbPri.SelectedItem == null) { Ui.Error("Выберите категорию и приоритет."); return; }

        object due = null;
        if (dtpDue.Checked)
        {
            DateOnly d = DateOnly.FromDateTime(dtpDue.Value);
            if (taskId == null && d < DateOnly.FromDateTime(DateTime.Today))
            {
                Ui.Error("Срок выполнения не может быть в прошлом.");
                return;
            }
            due = d;
        }

        string desc = tbDesc.Text.Trim();
        object descObj = desc.Length == 0 ? null : desc;
        int cat = ((Item)cbCat.SelectedItem).Id;
        int pri = ((Item)cbPri.SelectedItem).Id;

        bool ok = Ui.Try(() =>
        {
            if (taskId == null)
            {
                using var c = Db.Open();
                using var tx = c.BeginTransaction();
                int newId = Convert.ToInt32(Db.Scalar(c, tx,
                    "INSERT INTO tasks(title, description, due_date, author_id, category_id, priority_id, status_id) " +
                    "VALUES (@title, @desc, @due, @me, @cat, @pri, 1) RETURNING task_id",
                    ("title", title), ("desc", descObj), ("due", due), ("me", Session.UserId), ("cat", cat), ("pri", pri)));
                Db.Exec(c, tx,
                    "INSERT INTO task_history(user_id, task_id, old_status_id, new_status_id) VALUES (@me, @id, NULL, 1)",
                    ("me", Session.UserId), ("id", newId));
                tx.Commit();
            }
            else
            {
                int n = Db.Exec(
                    "UPDATE tasks SET title = @title, description = @desc, due_date = @due, " +
                    "category_id = @cat, priority_id = @pri " +
                    "WHERE task_id = @id AND (@admin OR (author_id = @me AND status_id = 1))",
                    ("title", title), ("desc", descObj), ("due", due), ("cat", cat), ("pri", pri),
                    ("id", taskId.Value), ("admin", Session.Role == "Администратор"), ("me", Session.UserId));
                if (n == 0)
                    throw new UserMessageException("Изменить задачу нельзя: она уже принята менеджером или у вас нет прав.");
            }
        });
        if (ok) DialogResult = DialogResult.OK;
    }
}
