create table users (
    id           integer primary key autoincrement,
    display_name text not null
);

insert into users (id, display_name) values (1, 'Аноним');

create table places_categories (
    id    text primary key,
    title text not null
);

insert into places_categories (id, title)
values ('cafe', 'Кофейни'),
       ('restaurant', 'Рестораны');

create table places (
    id          integer primary key autoincrement,
    external_id text    not null unique,
    name        text    not null,
    category_id text    not null references places_categories (id),
    lat         real    not null,
    lon         real    not null,
    address     text    null,
    updated_at  text    not null
);

create table check_ins (
    id         integer primary key autoincrement,
    place_id   integer not null references places (id),
    user_id    integer not null references users (id),
    created_at text    not null
);

-- Под проверку кулдауна: «когда этот человек последний раз отмечался тут».
create index ix_check_ins_user_place on check_ins (user_id, place_id, created_at desc);

