CREATE TABLE IF NOT EXISTS kenketsu.access_log (
    id          BIGSERIAL PRIMARY KEY,
    accessed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    page        VARCHAR(200) NOT NULL,
    is_admin    BOOLEAN NOT NULL DEFAULT FALSE,
    ip_address  VARCHAR(45)
);

CREATE INDEX IF NOT EXISTS idx_access_log_accessed_at ON kenketsu.access_log (accessed_at);
CREATE INDEX IF NOT EXISTS idx_access_log_page        ON kenketsu.access_log (page);
