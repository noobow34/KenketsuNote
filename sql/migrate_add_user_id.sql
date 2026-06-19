-- KenketsuNote migration: add user_id to tracker tables
-- Run once against the existing database (ashiato schema)
-- Prerequisite: ashiato.kenketsu_record and ashiato.kenketsu_restriction already exist
--               (created by MultiWeb / Noobow.Commons)

BEGIN;

-- ============================================================
-- 1. kenketsu_record: add user_id column
-- ============================================================
ALTER TABLE ashiato.kenketsu_record
    ADD COLUMN IF NOT EXISTS user_id VARCHAR(10) NOT NULL DEFAULT '';

-- Remove the default once the column exists
-- (rows already in the table will have '' which can be cleaned up manually if needed)
ALTER TABLE ashiato.kenketsu_record
    ALTER COLUMN user_id DROP DEFAULT;

-- Index for fast per-user queries
CREATE INDEX IF NOT EXISTS idx_kenketsu_record_user_id
    ON ashiato.kenketsu_record (user_id);

-- ============================================================
-- 2. kenketsu_restriction: add user_id column
-- ============================================================
ALTER TABLE ashiato.kenketsu_restriction
    ADD COLUMN IF NOT EXISTS user_id VARCHAR(10) NOT NULL DEFAULT '';

ALTER TABLE ashiato.kenketsu_restriction
    ALTER COLUMN user_id DROP DEFAULT;

CREATE INDEX IF NOT EXISTS idx_kenketsu_restriction_user_id
    ON ashiato.kenketsu_restriction (user_id);

-- ============================================================
-- 3. kenketsu_record: add room_id column
-- ============================================================
ALTER TABLE ashiato.kenketsu_record
    ADD COLUMN IF NOT EXISTS room_id INT REFERENCES ashiato.kenketsu_room(room_id) ON DELETE SET NULL;

-- ============================================================
-- 4. users: add gender column
-- ============================================================
ALTER TABLE ashiato.users
    ADD COLUMN IF NOT EXISTS gender VARCHAR(6);  -- 'male' / 'female' / NULL

-- ============================================================
-- 4. (Optional) If migrating existing single-user data from MultiWeb,
--    replace 'YOUR_USER_ID_HERE' with the target user's ID and run:
--
--   UPDATE ashiato.kenketsu_record     SET user_id = 'YOUR_USER_ID_HERE' WHERE user_id = '';
--   UPDATE ashiato.kenketsu_restriction SET user_id = 'YOUR_USER_ID_HERE' WHERE user_id = '';
-- ============================================================

COMMIT;
