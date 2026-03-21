DELETE FROM group_photos a
USING group_photos b
WHERE a.photo_name = b.photo_name
  AND a.group_id > b.group_id;

ALTER TABLE group_photos
    DROP CONSTRAINT IF EXISTS group_photos_photo_name_key;

ALTER TABLE group_photos
    ADD CONSTRAINT group_photos_photo_name_key UNIQUE (photo_name);
