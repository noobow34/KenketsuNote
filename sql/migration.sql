-- KenketsuNote: 統合マイグレーション
-- 新規環境構築時にこのファイルを1度だけ実行してください。
-- 既存DB への列追加は ALTER TABLE で個別に実行してください（チャットで都度提示）。

BEGIN;

CREATE SCHEMA IF NOT EXISTS kenketsu;

-- ============================================================
-- マスター系
-- ============================================================

CREATE TABLE IF NOT EXISTS kenketsu.center_block (
    center_block_id SERIAL PRIMARY KEY,
    center_block_name VARCHAR(30),
    display_order INT
);

CREATE TABLE IF NOT EXISTS kenketsu.pref (
    pref_id SERIAL PRIMARY KEY,
    pref_name VARCHAR(30),
    center_block INT NOT NULL REFERENCES kenketsu.center_block(center_block_id),
    display_order INT
);

CREATE TABLE IF NOT EXISTS kenketsu.kenketsu_room (
    room_id        SERIAL PRIMARY KEY,
    pref           INT          NOT NULL REFERENCES kenketsu.pref(pref_id),
    room_name      VARCHAR(100) NOT NULL,
    display_order  INT,
    image_path     VARCHAR(100),
    is_closed      BOOLEAN      NOT NULL DEFAULT FALSE,
    remark         VARCHAR(200),
    city           VARCHAR(50),
    can_whole      BOOLEAN,
    can_plasma     BOOLEAN,
    can_platelet   BOOLEAN,
    closed_days    VARCHAR(200),
    room_url       VARCHAR(500)
);

CREATE TABLE IF NOT EXISTS kenketsu.room_business_hours (
    id                    SERIAL PRIMARY KEY,
    room_id               INT NOT NULL REFERENCES kenketsu.kenketsu_room(room_id) ON DELETE CASCADE,
    day_type              INT NOT NULL,  -- 0=平日, 1=土日祝
    whole_reception_start TIME,
    whole_reception_end   TIME,
    whole_lunch_start     TIME,
    whole_lunch_end       TIME,
    comp_reception_start  TIME,
    comp_reception_end    TIME,
    comp_lunch_start      TIME,
    comp_lunch_end        TIME
);

-- ============================================================
-- ユーザー系
-- ============================================================

CREATE TABLE IF NOT EXISTS kenketsu.users (
    user_id                VARCHAR(10) PRIMARY KEY,
    registered_at          TIMESTAMP,
    last_access_at         TIMESTAMP,
    user_name              VARCHAR(50),
    show_closed_default    BOOLEAN NOT NULL DEFAULT FALSE,
    gender                 VARCHAR(6),
    migrated_from_ashiato  BOOLEAN NOT NULL DEFAULT FALSE,
    share_show_history     BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS kenketsu.visit_stamp (
    stamp_id   SERIAL PRIMARY KEY,
    user_id    VARCHAR(10) NOT NULL REFERENCES kenketsu.users(user_id),
    room_id    INT         NOT NULL REFERENCES kenketsu.kenketsu_room(room_id),
    visit_date DATE,
    angle      NUMERIC(5,1) NOT NULL DEFAULT 0,
    created_at TIMESTAMP(6),
    updated_at TIMESTAMP(6)
);

CREATE TABLE IF NOT EXISTS kenketsu.share_mapping (
    share_id    VARCHAR(10) PRIMARY KEY,
    original_id VARCHAR(10) NOT NULL REFERENCES kenketsu.users(user_id)
);

CREATE TABLE IF NOT EXISTS kenketsu.center_block_order (
    order_id        SERIAL PRIMARY KEY,
    center_block_id INT         NOT NULL REFERENCES kenketsu.center_block(center_block_id),
    user_id         VARCHAR(10) NOT NULL REFERENCES kenketsu.users(user_id),
    display_order   INT         NOT NULL
);

CREATE TABLE IF NOT EXISTS kenketsu.pref_order (
    order_id      SERIAL PRIMARY KEY,
    pref_id       INT         NOT NULL REFERENCES kenketsu.pref(pref_id),
    user_id       VARCHAR(10) NOT NULL REFERENCES kenketsu.users(user_id),
    display_order INT         NOT NULL
);

-- ============================================================
-- トラッカー系
-- ============================================================

CREATE TABLE IF NOT EXISTS kenketsu.kenketsu_record (
    id             SERIAL PRIMARY KEY,
    user_id        VARCHAR(10) NOT NULL,
    donation_date  DATE        NOT NULL,
    donation_type  VARCHAR(20) NOT NULL,
    record_type    VARCHAR(10) NOT NULL,
    volume_ml      INT,
    component_count INT,
    room_id        INT REFERENCES kenketsu.kenketsu_room(room_id) ON DELETE SET NULL,
    notes          VARCHAR(500),
    created_at     TIMESTAMP   NOT NULL DEFAULT NOW(),
    updated_at     TIMESTAMP   NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_kenketsu_record_user_id
    ON kenketsu.kenketsu_record (user_id);
CREATE INDEX IF NOT EXISTS idx_kenketsu_record_user_date
    ON kenketsu.kenketsu_record (user_id, donation_date);

CREATE TABLE IF NOT EXISTS kenketsu.kenketsu_restriction (
    id            SERIAL PRIMARY KEY,
    user_id       VARCHAR(10) NOT NULL,
    start_date    DATE        NOT NULL,
    duration_days INT         NOT NULL,
    reason        VARCHAR(500),
    created_at    TIMESTAMP   NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMP   NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_kenketsu_restriction_user_id
    ON kenketsu.kenketsu_restriction (user_id);

CREATE TABLE IF NOT EXISTS kenketsu.kenketsu_restriction_preset (
    id            SERIAL PRIMARY KEY,
    label         VARCHAR(100) NOT NULL,
    duration_days INT          NOT NULL,
    display_order INT          NOT NULL DEFAULT 0
);

-- ============================================================
-- 検索ログ
-- ============================================================

CREATE TABLE IF NOT EXISTS kenketsu.room_search_log (
    id                  BIGSERIAL PRIMARY KEY,
    searched_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    center_block_id     INT,
    pref_id             INT,
    room_name           VARCHAR(100),
    city                VARCHAR(50),
    can_whole           BOOLEAN,
    can_plasma          BOOLEAN,
    can_platelet        BOOLEAN,
    whole_only          BOOLEAN     NOT NULL DEFAULT FALSE,
    plasma_only         BOOLEAN     NOT NULL DEFAULT FALSE,
    open_dows           VARCHAR(50),
    no_lunch_break_dows VARCHAR(20),
    time_day_type       INT,
    whole_open_by       TIME,
    whole_close_after   TIME,
    comp_open_by        TIME,
    comp_close_after    TIME,
    include_closed      BOOLEAN     NOT NULL DEFAULT FALSE,
    result_count        INT,
    ip_address          VARCHAR(45),
    user_agent          VARCHAR(500),
    is_admin            BOOLEAN     NOT NULL DEFAULT FALSE
);

CREATE INDEX IF NOT EXISTS idx_room_search_log_searched_at
    ON kenketsu.room_search_log (searched_at DESC);

-- ============================================================
-- アクセスログ
-- ============================================================

CREATE TABLE IF NOT EXISTS kenketsu.access_log (
    id          BIGSERIAL PRIMARY KEY,
    accessed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    page        VARCHAR(200) NOT NULL,
    is_admin    BOOLEAN      NOT NULL DEFAULT FALSE,
    ip_address  VARCHAR(45)
);

CREATE INDEX IF NOT EXISTS idx_access_log_accessed_at ON kenketsu.access_log (accessed_at);
CREATE INDEX IF NOT EXISTS idx_access_log_page        ON kenketsu.access_log (page);

-- ============================================================
-- Gemini差分チェック
-- ============================================================

CREATE TABLE IF NOT EXISTS kenketsu.room_check_job_state (
    id                 INT PRIMARY KEY DEFAULT 1,
    next_offset        INT         NOT NULL DEFAULT 0,
    last_run_at        TIMESTAMPTZ,
    scheduled_hour     INT         NOT NULL DEFAULT 6,
    scheduled_minute   INT         NOT NULL DEFAULT 30,
    log_retention_days INT         NOT NULL DEFAULT 90,
    gemini_model       VARCHAR(100) NOT NULL DEFAULT 'gemini-3.5-flash-lite'
);

INSERT INTO kenketsu.room_check_job_state (id, next_offset)
    VALUES (1, 0)
    ON CONFLICT (id) DO NOTHING;

CREATE TABLE IF NOT EXISTS kenketsu.room_check_result (
    id            BIGSERIAL PRIMARY KEY,
    room_id       INT         NOT NULL REFERENCES kenketsu.kenketsu_room(room_id),
    checked_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    gemini_result TEXT,
    changes       VARCHAR(1000),
    has_changes   BOOLEAN     NOT NULL DEFAULT FALSE,
    resolved      BOOLEAN     NOT NULL DEFAULT FALSE,
    review_token  UUID        NOT NULL DEFAULT gen_random_uuid()
);

CREATE INDEX IF NOT EXISTS idx_room_check_result_room_id
    ON kenketsu.room_check_result (room_id);
CREATE INDEX IF NOT EXISTS idx_room_check_result_checked_at
    ON kenketsu.room_check_result (checked_at DESC);
CREATE INDEX IF NOT EXISTS idx_room_check_result_has_changes
    ON kenketsu.room_check_result (has_changes) WHERE has_changes = TRUE;

CREATE TABLE IF NOT EXISTS kenketsu.room_dismissed_diff (
    id           BIGSERIAL PRIMARY KEY,
    room_id      INT         NOT NULL,
    field        VARCHAR(50) NOT NULL,
    gemini_value TEXT        NOT NULL,
    dismissed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (room_id, field)
);

COMMIT;
