CREATE DOMAIN d_login         AS VARCHAR(50)  CHECK (char_length(trim(VALUE)) >= 3);
CREATE DOMAIN d_email         AS VARCHAR(100) CHECK (VALUE ~ '^[^@\s]+@[^@\s]+\.[^@\s]+$');
CREATE DOMAIN d_password_hash AS VARCHAR(255) CHECK (char_length(VALUE) >= 8);
CREATE DOMAIN d_full_name     AS VARCHAR(100) CHECK (char_length(trim(VALUE)) > 0);
CREATE DOMAIN d_title         AS VARCHAR(100) CHECK (char_length(trim(VALUE)) > 0);

CREATE TABLE roles (
    role_id SERIAL PRIMARY KEY,
    name    VARCHAR(30) NOT NULL UNIQUE
);

CREATE TABLE categories (
    category_id SERIAL PRIMARY KEY,
    name        VARCHAR(50) NOT NULL UNIQUE
);

CREATE TABLE priorities (
    priority_id SERIAL PRIMARY KEY,
    name        VARCHAR(30) NOT NULL UNIQUE
);

CREATE TABLE statuses (
    status_id SERIAL PRIMARY KEY,
    name      VARCHAR(30) NOT NULL UNIQUE
);

CREATE TABLE users (
    user_id       SERIAL PRIMARY KEY,
    login         d_login         NOT NULL UNIQUE,
    password_hash d_password_hash NOT NULL,
    full_name     d_full_name     NOT NULL,
    email         d_email         NOT NULL UNIQUE,
    role_id       INT NOT NULL REFERENCES roles(role_id)
);

CREATE TABLE tasks (
    task_id     SERIAL PRIMARY KEY,
    title       d_title NOT NULL,
    description VARCHAR(500),
    created_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    due_date    DATE,
    author_id   INT NOT NULL REFERENCES users(user_id),
    manager_id  INT NULL     REFERENCES users(user_id),
    category_id INT NOT NULL REFERENCES categories(category_id),
    priority_id INT NOT NULL REFERENCES priorities(priority_id),
    status_id   INT NOT NULL REFERENCES statuses(status_id),
    CONSTRAINT chk_tasks_due CHECK (due_date IS NULL OR due_date >= created_at::date)
);

CREATE TABLE task_history (
    history_id    SERIAL PRIMARY KEY,
    user_id       INT NOT NULL REFERENCES users(user_id),
    task_id       INT NOT NULL REFERENCES tasks(task_id) ON DELETE CASCADE,
    old_status_id INT NULL     REFERENCES statuses(status_id),
    new_status_id INT NOT NULL REFERENCES statuses(status_id),
    changed_at    TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_history_status CHECK (old_status_id IS DISTINCT FROM new_status_id)
);

CREATE OR REPLACE FUNCTION prevent_id_change() RETURNS trigger AS $$
BEGIN
    IF to_jsonb(NEW)->>TG_ARGV[0] IS DISTINCT FROM to_jsonb(OLD)->>TG_ARGV[0] THEN
        RAISE EXCEPTION 'Изменение идентификатора (%) запрещено', TG_ARGV[0];
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_roles_id        BEFORE UPDATE ON roles        FOR EACH ROW EXECUTE FUNCTION prevent_id_change('role_id');
CREATE TRIGGER trg_categories_id   BEFORE UPDATE ON categories   FOR EACH ROW EXECUTE FUNCTION prevent_id_change('category_id');
CREATE TRIGGER trg_priorities_id   BEFORE UPDATE ON priorities   FOR EACH ROW EXECUTE FUNCTION prevent_id_change('priority_id');
CREATE TRIGGER trg_statuses_id     BEFORE UPDATE ON statuses     FOR EACH ROW EXECUTE FUNCTION prevent_id_change('status_id');
CREATE TRIGGER trg_users_id        BEFORE UPDATE ON users        FOR EACH ROW EXECUTE FUNCTION prevent_id_change('user_id');
CREATE TRIGGER trg_tasks_id        BEFORE UPDATE ON tasks        FOR EACH ROW EXECUTE FUNCTION prevent_id_change('task_id');
CREATE TRIGGER trg_task_history_id BEFORE UPDATE ON task_history FOR EACH ROW EXECUTE FUNCTION prevent_id_change('history_id');

INSERT INTO roles(name)      VALUES ('Администратор'), ('Менеджер'), ('Пользователь');
INSERT INTO categories(name) VALUES ('Работа'), ('Учёба'), ('Дом'), ('Личное'), ('Здоровье'), ('Финансы');
INSERT INTO priorities(name) VALUES ('Низкий'), ('Средний'), ('Высокий');
INSERT INTO statuses(name)   VALUES ('Новая'), ('Принята'), ('В работе'), ('Выполнена'), ('Отменена');

INSERT INTO users(login, password_hash, full_name, email, role_id)
SELECT 'user' || i,
       md5('pass' || i),
       (ARRAY['Иванов','Петров','Сидоров','Смирнов','Кузнецов','Попов','Лебедев','Козлов'])[1 + i % 8]
         || ' ' || (ARRAY['Иван','Пётр','Алексей','Дмитрий','Сергей','Андрей'])[1 + i % 6],
       'user' || i || '@mail.ru',
       CASE WHEN i = 1 THEN 1 WHEN i <= 10 THEN 2 ELSE 3 END
FROM generate_series(1, 120) AS i;

INSERT INTO tasks(title, description, created_at, due_date, author_id, manager_id, category_id, priority_id, status_id)
SELECT 'Задача №' || i,
       'Описание задачи №' || i,
       ts,
       ts::date + (1 + floor(random() * 30))::int,
       author,
       CASE WHEN s = 1 THEN NULL ELSE 2 + floor(random() * 9)::int END,
       cat, pr, s
FROM (
    SELECT i,
           localtimestamp - random() * 30 * interval '1 day' AS ts,
           11 + floor(random() * 110)::int AS author,
           1 + floor(random() * 6)::int AS cat,
           1 + floor(random() * 3)::int AS pr,
           1 + floor(random() * 5)::int AS s
    FROM generate_series(1, 150) AS i
    OFFSET 0
) t;

INSERT INTO task_history(user_id, task_id, old_status_id, new_status_id, changed_at)
SELECT author_id, task_id, NULL, 1, created_at FROM tasks;

INSERT INTO task_history(user_id, task_id, old_status_id, new_status_id, changed_at)
SELECT manager_id, task_id, 1, status_id, created_at + interval '1 hour'
FROM tasks WHERE status_id > 1;

SELECT 'users' AS таблица, count(*) AS записей FROM users
UNION ALL SELECT 'tasks', count(*) FROM tasks
UNION ALL SELECT 'task_history', count(*) FROM task_history;
