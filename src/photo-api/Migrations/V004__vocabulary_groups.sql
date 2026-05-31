-- V004: Decouple vocabulary from gallery
-- Vocabulary groups get their own independent tables with a name field.
-- Photos are moved out of gallery into vocabulary on demand.

CREATE TABLE IF NOT EXISTS vocabulary.groups (
    id         TEXT        PRIMARY KEY
        CONSTRAINT vocabulary_groups_id_not_blank CHECK (id <> ''),
    name       TEXT        NOT NULL
        CONSTRAINT vocabulary_groups_name_not_blank CHECK (name <> ''),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS vocabulary.photos (
    photo_name TEXT        NOT NULL PRIMARY KEY
        CONSTRAINT vocabulary_photos_name_not_blank CHECK (photo_name <> ''),
    group_id   TEXT        NOT NULL
        REFERENCES vocabulary.groups (id) ON DELETE RESTRICT,
    added_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_vocabulary_photos_group
    ON vocabulary.photos (group_id);

-- ── Migrate existing vocabulary.group_members ─────────────────────────────────

-- Create vocabulary.groups entries (reuse existing gallery group ID, derive name from category)
INSERT INTO vocabulary.groups (id, name, created_at)
SELECT
    vm.group_id,
    COALESCE(
        (SELECT c.name
         FROM   public.photo_group_categories gc
         JOIN   public.categories c ON c.id = gc.category_id
         WHERE  gc.group_id = vm.group_id
         LIMIT  1),
        'Untitled'
    ),
    vm.added_at
FROM vocabulary.group_members vm;

-- Move all photos (from the vocab group and all its gallery children) into vocabulary.photos
INSERT INTO vocabulary.photos (photo_name, group_id, added_at)
SELECT pgm.photo_name, vg.id, vm.added_at
FROM   vocabulary.groups vg
JOIN   vocabulary.group_members vm ON vm.group_id = vg.id
JOIN   public.photo_group_members pgm
       ON  pgm.group_id = vg.id
       OR  pgm.group_id IN (
               SELECT group_id FROM public.photo_groups
               WHERE  parent_group_id = vg.id)
ON CONFLICT DO NOTHING;

-- ── Clean up gallery ──────────────────────────────────────────────────────────

-- Release FK from vocabulary.group_members before touching photo_groups
DELETE FROM vocabulary.group_members;

-- Remove moved photos from gallery
DELETE FROM public.photo_group_members
WHERE  photo_name IN (SELECT photo_name FROM vocabulary.photos);

-- Remove category links for child groups of moved parent groups
DELETE FROM public.photo_group_categories
WHERE  group_id IN (
    SELECT group_id FROM public.photo_groups
    WHERE  parent_group_id IN (SELECT id FROM vocabulary.groups));

-- Remove child groups of moved parent groups
DELETE FROM public.photo_groups
WHERE  parent_group_id IN (SELECT id FROM vocabulary.groups);

-- Remove category links for the parent groups themselves
DELETE FROM public.photo_group_categories
WHERE  group_id IN (SELECT id FROM vocabulary.groups);

-- Remove the parent groups themselves
DELETE FROM public.photo_groups
WHERE  group_id IN (SELECT id FROM vocabulary.groups);

-- ── Drop the old membership table ─────────────────────────────────────────────
DROP TABLE vocabulary.group_members;
