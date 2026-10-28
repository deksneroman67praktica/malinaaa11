SELECT u.user_id,
       u.login,
       u.full_name,
       r.name AS "role"
  FROM users u
 INNER JOIN roles r ON r.role_id = u.role_id;