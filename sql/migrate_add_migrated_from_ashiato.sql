ALTER TABLE kenketsu.users
    ADD COLUMN IF NOT EXISTS migrated_from_ashiato boolean NOT NULL DEFAULT false;
