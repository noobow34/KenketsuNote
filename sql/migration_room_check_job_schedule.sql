ALTER TABLE kenketsu.room_check_job_state
    ADD COLUMN IF NOT EXISTS scheduled_hour   INTEGER NOT NULL DEFAULT 6,
    ADD COLUMN IF NOT EXISTS scheduled_minute INTEGER NOT NULL DEFAULT 30;
