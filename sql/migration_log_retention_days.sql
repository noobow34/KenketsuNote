ALTER TABLE kenketsu.room_check_job_state
    ADD COLUMN IF NOT EXISTS log_retention_days INTEGER NOT NULL DEFAULT 90;
