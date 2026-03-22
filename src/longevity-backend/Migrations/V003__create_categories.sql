CREATE TABLE categories (
    id   SERIAL      PRIMARY KEY,
    name TEXT        NOT NULL UNIQUE
);

CREATE TABLE group_categories (
    group_id    TEXT    NOT NULL,
    category_id INTEGER NOT NULL REFERENCES categories(id) ON DELETE CASCADE,
    PRIMARY KEY (group_id, category_id)
);

CREATE INDEX idx_group_categories_category ON group_categories (category_id);
