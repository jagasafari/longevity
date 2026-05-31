-- Create vocabulary schema with its own membership table,
-- decoupled from the gallery categories system.
-- Also restores categories + photo_group_categories which were dropped
-- by the V002 migration on feature/filter-improved.

CREATE SCHEMA IF NOT EXISTS vocabulary;

CREATE TABLE IF NOT EXISTS vocabulary.group_members (
    group_id TEXT        NOT NULL
        REFERENCES photo_groups (group_id)
        ON DELETE RESTRICT,
    added_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (group_id)
);

-- Migrate vocabulary-tagged groups from photo_group_names
INSERT INTO vocabulary.group_members (group_id)
SELECT group_id FROM photo_group_names WHERE name = 'vocabulary'
ON CONFLICT DO NOTHING;

-- Restore categories table (was dropped by V002)
CREATE TABLE IF NOT EXISTS categories (
    id   SERIAL PRIMARY KEY,
    name TEXT   NOT NULL UNIQUE
        CONSTRAINT categories_name_not_blank CHECK (name <> '')
);

-- Restore photo_group_categories table (was dropped by V002)
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

-- Migrate non-vocabulary names back into the categories system
INSERT INTO categories (name)
SELECT DISTINCT name FROM photo_group_names WHERE name <> 'vocabulary'
ON CONFLICT DO NOTHING;

INSERT INTO photo_group_categories (group_id, category_id)
SELECT pgn.group_id, c.id
FROM photo_group_names pgn
JOIN categories c ON c.name = pgn.name
WHERE pgn.name <> 'vocabulary'
ON CONFLICT DO NOTHING;

-- Drop the now-superseded photo_group_names table
DROP TABLE photo_group_names;
