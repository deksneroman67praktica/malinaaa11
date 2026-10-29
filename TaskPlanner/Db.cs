using System;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

public class UserMessageException : Exception
{
    public UserMessageException(string message) : base(message) { }
}

public static class Session
{
    public static int UserId;
    public static string FullName;
    public static string Role;
}

public static class Db
{
    public static string Host = "localhost";
    public static string Port = "5432";
    public static string Database = "task_planner";
    public static string Username = "user1";
    public static string Password = "";

    static readonly string SettingsFile = Path.Combine(AppContext.BaseDirectory, "connection.txt");

    static Db()
    {
        LoadSettings();
    }

    public static bool HasSettings
    {
        get { return File.Exists(SettingsFile); }
    }

    public static string ConnectionString
    {
        get
        {
            var b = new NpgsqlConnectionStringBuilder
            {
                Host = Host,
                Database = Database,
                Username = Username,
                Password = Password,
                Timeout = 5
            };
            if (int.TryParse(Port, out int p)) b.Port = p;
            return b.ConnectionString;
        }
    }

    public static void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return;
            string[] lines = File.ReadAllLines(SettingsFile);
            if (lines.Length < 5) return;
            Host = lines[0]; Port = lines[1]; Database = lines[2]; Username = lines[3]; Password = lines[4];
        }
        catch { }
    }

    public static void SaveSettings()
    {
        File.WriteAllLines(SettingsFile, new[] { Host, Port, Database, Username, Password });
    }

    public static NpgsqlConnection Open()
    {
        var c = new NpgsqlConnection(ConnectionString);
        c.Open();
        return c;
    }

    static NpgsqlCommand Make(NpgsqlConnection c, NpgsqlTransaction tx, string sql, (string, object)[] ps)
    {
        var cmd = new NpgsqlCommand(sql, c, tx);
        foreach (var (name, value) in ps)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    public static DataTable Query(NpgsqlConnection c, NpgsqlTransaction tx, string sql, params (string, object)[] ps)
    {
        using var cmd = Make(c, tx, sql, ps);
        using var r = cmd.ExecuteReader();
        var t = new DataTable();
        t.Load(r);
        return t;
    }

    public static int Exec(NpgsqlConnection c, NpgsqlTransaction tx, string sql, params (string, object)[] ps)
    {
        using var cmd = Make(c, tx, sql, ps);
        return cmd.ExecuteNonQuery();
    }

    public static object Scalar(NpgsqlConnection c, NpgsqlTransaction tx, string sql, params (string, object)[] ps)
    {
        using var cmd = Make(c, tx, sql, ps);
        return cmd.ExecuteScalar();
    }

    public static object Scalar(string sql, params (string, object)[] ps)
    {
        using var c = Open();
        return Scalar(c, null, sql, ps);
    }

    public static DataTable Query(string sql, params (string, object)[] ps)
    {
        using var c = Open();
        return Query(c, null, sql, ps);
    }

    public static int Exec(string sql, params (string, object)[] ps)
    {
        using var c = Open();
        return Exec(c, null, sql, ps);
    }

    public static string Md5(string s)
    {
        using var md5 = MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
    }

    public static string Friendly(Exception ex)
    {
        if (ex is UserMessageException) return ex.Message;

        if (ex is PostgresException pe)
        {
            string m = pe.MessageText ?? "";
            switch (pe.SqlState)
            {
                case "23505":
                    string key = pe.ConstraintName ?? m;
                    if (key.Contains("login")) return "Пользователь с таким логином уже существует.";
                    if (key.Contains("email")) return "Этот email уже используется.";
                    return "Такая запись уже существует.";
                case "23514":
                    if (m.Contains("d_email")) return "Некорректный email. Пример: name@mail.ru";
                    if (m.Contains("d_login")) return "Логин должен быть не короче 3 символов.";
                    if (m.Contains("d_password_hash")) return "Пароль слишком короткий.";
                    if (m.Contains("d_full_name")) return "Укажите ФИО.";
                    if (m.Contains("d_title")) return "Название задачи не может быть пустым.";
                    if (m.Contains("chk_tasks_due")) return "Срок выполнения не может быть раньше даты создания задачи.";
                    return "Недопустимое значение в одном из полей.";
                case "23502":
                    return "Не заполнено обязательное поле" + (pe.ColumnName != null ? " (" + pe.ColumnName + ")" : "") + ".";
                case "23503":
                    return "Операцию выполнить нельзя: запись связана с другими данными.";
                case "22001":
                    return "Слишком длинное значение в одном из полей.";
                case "42P01":
                    return "В выбранной базе нет нужных таблиц. Откройте эту базу в pgAdmin и выполните скрипт database.sql.";
                case "42703":
                    return "Структура таблиц в базе отличается от ожидаемой. Выполните database.sql заново в пустой базе.";
                case "28P01":
                case "28000":
                    return "Неверный пользователь или пароль базы данных. Нажмите «Настройки» и проверьте данные.";
                case "3D000":
                    return "Базы данных с таким именем нет. Нажмите «Настройки» и проверьте имя базы.";
                case "P0001":
                    return m;
                default:
                    return "Ошибка базы данных: " + m;
            }
        }

        if (ex is NpgsqlException || ex is System.Net.Sockets.SocketException || ex is TimeoutException)
            return "Не удалось подключиться к базе данных. Нажмите «Настройки» и проверьте адрес сервера, порт, имя базы, пользователя и пароль.";

        return "Ошибка: " + ex.Message;
    }
}
