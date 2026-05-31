ALTER TABLE vocabulary.photos ADD COLUMN IF NOT EXISTS subgroup_id TEXT;

CREATE INDEX IF NOT EXISTS idx_vocabulary_photos_subgroup
    ON vocabulary.photos (subgroup_id)
    WHERE subgroup_id IS NOT NULL;
