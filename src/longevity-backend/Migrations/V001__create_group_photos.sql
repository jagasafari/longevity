ALTER TABLE IF EXISTS group_photos
    DROP CONSTRAINT IF EXISTS group_photos_group_id_fkey;

DROP TABLE IF EXISTS photo_groups;

CREATE TABLE IF NOT EXISTS group_photos (
    group_id   TEXT NOT NULL,
    photo_name TEXT NOT NULL,
    PRIMARY KEY (group_id, photo_name)
);

CREATE INDEX IF NOT EXISTS idx_group_photos_photo
    ON group_photos (photo_name);
