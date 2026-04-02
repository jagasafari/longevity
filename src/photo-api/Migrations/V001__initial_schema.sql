CREATE TABLE IF NOT EXISTS photo_groups (
    group_id        TEXT PRIMARY KEY
        CONSTRAINT photo_groups_id_not_blank CHECK (group_id <> ''),
    parent_group_id TEXT NULL
        REFERENCES photo_groups (group_id)
        ON DELETE SET NULL
        CONSTRAINT photo_groups_no_self_parent CHECK (parent_group_id IS NULL OR parent_group_id <> group_id)
);

CREATE INDEX IF NOT EXISTS idx_photo_groups_parent
    ON photo_groups (parent_group_id);

CREATE TABLE IF NOT EXISTS photo_group_members (
    group_id   TEXT NOT NULL
        REFERENCES photo_groups (group_id)
        ON DELETE RESTRICT,
    photo_name TEXT NOT NULL UNIQUE
        CONSTRAINT photo_group_members_name_not_blank CHECK (photo_name <> ''),
    PRIMARY KEY (group_id, photo_name)
);

CREATE INDEX IF NOT EXISTS idx_photo_group_members_photo
    ON photo_group_members (photo_name);

CREATE TABLE IF NOT EXISTS categories (
    id   SERIAL PRIMARY KEY,
    name TEXT   NOT NULL UNIQUE
        CONSTRAINT categories_name_not_blank CHECK (name <> '')
);

CREATE TABLE IF NOT EXISTS photo_group_categories (
    group_id    TEXT    NOT NULL
        REFERENCES photo_groups (group_id)
        ON DELETE RESTRICT,
    category_id INTEGER NOT NULL
        REFERENCES categories (id)
        ON DELETE CASCADE,
    PRIMARY KEY (group_id, category_id)
);

CREATE INDEX IF NOT EXISTS idx_photo_group_categories_category
    ON photo_group_categories (category_id);
