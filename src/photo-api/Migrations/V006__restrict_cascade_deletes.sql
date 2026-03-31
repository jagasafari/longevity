-- ============================================================
-- V006: Make group operations safe — no silent data loss.
-- ============================================================

-- 1) group_photos: RESTRICT delete of group that still has photos.
--    Code must explicitly ungroup photos before deleting a group.
ALTER TABLE group_photos
    DROP CONSTRAINT IF EXISTS group_photos_group_id_fkey;

ALTER TABLE group_photos
    ADD CONSTRAINT group_photos_group_id_fkey
        FOREIGN KEY (group_id)
        REFERENCES photo_groups (group_id)
        ON DELETE RESTRICT;

-- 2) group_categories: RESTRICT delete of group that still has categories.
--    Code must explicitly move/remove categories before deleting a group.
ALTER TABLE group_categories
    DROP CONSTRAINT IF EXISTS group_categories_group_id_fkey;

ALTER TABLE group_categories
    ADD CONSTRAINT group_categories_group_id_fkey
        FOREIGN KEY (group_id)
        REFERENCES photo_groups (group_id)
        ON DELETE RESTRICT;

-- 3) photo_groups self-ref: SET NULL when parent is deleted.
--    Children become root groups instead of being destroyed.
ALTER TABLE photo_groups
    DROP CONSTRAINT IF EXISTS photo_groups_parent_group_id_fkey;

ALTER TABLE photo_groups
    ADD CONSTRAINT photo_groups_parent_group_id_fkey
        FOREIGN KEY (parent_group_id)
        REFERENCES photo_groups (group_id)
        ON DELETE SET NULL;

-- 4) Ensure photo_name is never blank.
ALTER TABLE group_photos
    DROP CONSTRAINT IF EXISTS group_photos_name_not_blank;

ALTER TABLE group_photos
    ADD CONSTRAINT group_photos_name_not_blank
        CHECK (photo_name <> '');

-- 5) Ensure group_id is never blank.
ALTER TABLE photo_groups
    DROP CONSTRAINT IF EXISTS photo_groups_id_not_blank;

ALTER TABLE photo_groups
    ADD CONSTRAINT photo_groups_id_not_blank
        CHECK (group_id <> '');

-- 6) Prevent a group from being its own parent.
ALTER TABLE photo_groups
    DROP CONSTRAINT IF EXISTS photo_groups_no_self_parent;

ALTER TABLE photo_groups
    ADD CONSTRAINT photo_groups_no_self_parent
        CHECK (parent_group_id IS NULL OR parent_group_id <> group_id);

-- 7) Ensure category names are never blank.
ALTER TABLE categories
    DROP CONSTRAINT IF EXISTS categories_name_not_blank;

ALTER TABLE categories
    ADD CONSTRAINT categories_name_not_blank
        CHECK (name <> '');
