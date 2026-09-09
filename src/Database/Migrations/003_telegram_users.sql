-- Появилась настоящая авторизация: у пользователя теперь есть личность из Telegram.
--
-- Таблицы пересоздаются, а не правятся через alter: SQLite не умеет добавлять
-- unique-ограничение существующей колонке, а данные всё равно уходят — отметки
-- фиктивного пользователя ни к кому не относятся.

drop table check_ins;
drop table users;

create table users (
    id           integer primary key autoincrement,
    telegram_id  integer not null unique,
    display_name text    not null,
    username     text    null,
    photo_url    text    null
);

create table check_ins (
    id         integer primary key autoincrement,
    place_id   integer not null references places (id),
    user_id    integer not null references users (id),
    created_at text    not null
);

-- Под проверку кулдауна: «когда этот человек последний раз отмечался тут».
create index ix_check_ins_user_place on check_ins (user_id, place_id, created_at desc);

-- Под список отметок: where user_id = ? order by created_at desc.
create index ix_check_ins_user_created_at on check_ins (user_id, created_at desc);
