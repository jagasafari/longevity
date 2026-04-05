CREATE TABLE IF NOT EXISTS photo_group_names (
    group_id TEXT NOT NULL
        REFERENCES photo_groups (group_id)
        ON DELETE RESTRICT,
    name     TEXT NOT NULL
        CONSTRAINT photo_group_names_name_not_blank CHECK (name <> ''),
    PRIMARY KEY (group_id, name)
);

CREATE INDEX IF NOT EXISTS idx_photo_group_names_name
    ON photo_group_names (name);

INSERT INTO photo_group_names (group_id, name)
SELECT pgc.group_id, c.name
FROM photo_group_categories pgc
JOIN categories c ON c.id = pgc.category_id
ON CONFLICT DO NOTHING;

DROP TABLE photo_group_categories;
DROP TABLE categories;
