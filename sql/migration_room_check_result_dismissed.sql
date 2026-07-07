CREATE TABLE IF NOT EXISTS kenketsu.room_dismissed_diff (
    id           BIGSERIAL PRIMARY KEY,
    room_id      INTEGER NOT NULL,
    field        VARCHAR(50) NOT NULL,
    gemini_value TEXT NOT NULL,
    dismissed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (room_id, field)
);
