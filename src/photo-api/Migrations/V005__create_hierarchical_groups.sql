CREATE TABLE IF NOT EXISTS photo_groups (
    group_id        TEXT PRIMARY KEY,
    parent_group_id TEXT NULL
        REFERENCES photo_groups (group_id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_photo_groups_parent
    ON photo_groups (parent_group_id);

INSERT INTO photo_groups (group_id, parent_group_id)
SELECT DISTINCT g.group_id, NULL
FROM (
    SELECT group_id FROM group_photos
    UNION
    SELECT group_id FROM group_categories
) g
ON CONFLICT (group_id) DO NOTHING;

ALTER TABLE IF EXISTS group_photos
    DROP CONSTRAINT IF EXISTS group_photos_group_id_fkey;

ALTER TABLE IF EXISTS group_photos
    ADD CONSTRAINT group_photos_group_id_fkey
        FOREIGN KEY (group_id)
        REFERENCES photo_groups (group_id)
        ON DELETE CASCADE;

ALTER TABLE IF EXISTS group_categories
    DROP CONSTRAINT IF EXISTS group_categories_group_id_fkey;

ALTER TABLE IF EXISTS group_categories
    ADD CONSTRAINT group_categories_group_id_fkey
        FOREIGN KEY (group_id)
        REFERENCES photo_groups (group_id)
        ON DELETE CASCADE;
