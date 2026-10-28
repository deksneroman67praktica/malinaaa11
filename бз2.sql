WITH new_task AS (
    INSERT INTO tasks (title, description, due_date, author_id, category_id, priority_id, status_id)
    VALUES ('Тестовая задача', 'Описание тестовой задачи', CURRENT_DATE + 7,
            (SELECT min(user_id) FROM users),
            (SELECT min(category_id) FROM categories),
            (SELECT min(priority_id) FROM priorities), 1)
    RETURNING task_id, author_id
)
INSERT INTO task_history (user_id, task_id, old_status_id, new_status_id)
SELECT author_id, task_id, NULL, 1
FROM new_task;

UPDATE tasks
SET title = 'Тестовая задача (изменена)', description = 'Новое описание'
WHERE title = 'Тестовая задача'
RETURNING task_id, title, description;