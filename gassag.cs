using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

public class Item
{
    public int Id;
    public string Name;
    public Item(int id, string name) { Id = id; Name = name; }
    public override string ToString() { return Name; }
}

public static class Ui
{
    public static void Error(string text)
    {
        MessageBox.Show(text, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    public static void Info(string text)
    {
        MessageBox.Show(text, "Планировщик задач", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static bool Confirm(string text)
    {
        return MessageBox.Show(text, "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }

    public static bool Try(Action a)
    {
        try { a(); return true; }
        catch (Exception ex) { Error(Db.Friendly(ex)); return false; }
    }

    public static TextBox Field(Control parent, string label, int y, bool password = false)
    {
        parent.Controls.Add(new Label { Text = label, Left = 20, Top = y, Width = 130, Height = 20 });
        var tb = new TextBox { Left = 150, Top = y - 3, Width = 240 };
        if (password) tb.UseSystemPasswordChar = true;
        parent.Controls.Add(tb);
        return tb;
    }

    public static ComboBox Combo(Control parent, string label, int y)
    {
        parent.Controls.Add(new Label { Text = label, Left = 20, Top = y, Width = 130, Height = 20 });
        var cb = new ComboBox { Left = 150, Top = y - 3, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
        parent.Controls.Add(cb);
        return cb;
    }

    public static Button Btn(Control parent, string text, int x, int y, int w, Action click)
    {
        var b = new Button { Text = text, Left = x, Top = y, Width = w, Height = 30 };
        b.Click += (s, e) => click();
        parent.Controls.Add(b);
        return b;
    }

    public static DataGridView Grid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window
        };
    }

    public static List<Item> LoadItems(string sql)
    {
        var list = new List<Item>();
        foreach (DataRow r in Db.Query(sql).Rows)
            list.Add(new Item(Convert.ToInt32(r[0]), Convert.ToString(r[1])));
        return list;
    }

    public static Item Pick(string title, List<Item> items)
    {
        using var f = new Form
        {
            Text = title, Width = 340, Height = 160,
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };
        var cb = new ComboBox { Left = 20, Top = 20, Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
        cb.Items.AddRange(items.ToArray());
        if (cb.Items.Count > 0) cb.SelectedIndex = 0;
        var ok = new Button { Text = "OK", Left = 120, Top = 60, Width = 80, DialogResult = DialogResult.OK };
        f.Controls.Add(cb);
        f.Controls.Add(ok);
        f.AcceptButton = ok;
        return f.ShowDialog() == DialogResult.OK ? cb.SelectedItem as Item : null;
    }

    public static void ShowTable(string title, DataTable dt)
    {
        using var f = new Form { Text = title, Width = 700, Height = 380, StartPosition = FormStartPosition.CenterScreen };
        var g = Grid();
        g.DataSource = dt;
        f.Controls.Add(g);
        f.ShowDialog();
    }
}
