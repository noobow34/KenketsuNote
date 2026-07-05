-- ルーム情報チェックジョブ状態管理テーブル
CREATE TABLE kenketsu.room_check_job_state (
    id           INT PRIMARY KEY DEFAULT 1,
    next_offset  INT          NOT NULL DEFAULT 0,
    last_run_at  TIMESTAMPTZ
);

-- 初期レコード挿入
INSERT INTO kenketsu.room_check_job_state (id, next_offset) VALUES (1, 0);

-- ルーム情報チェック結果テーブル
CREATE TABLE kenketsu.room_check_result (
    id             BIGSERIAL PRIMARY KEY,
    room_id        INT          NOT NULL REFERENCES kenketsu.kenketsu_room(room_id),
    checked_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    gemini_result  TEXT,
    changes        VARCHAR(1000),
    has_changes    BOOLEAN      NOT NULL DEFAULT FALSE,
    resolved       BOOLEAN      NOT NULL DEFAULT FALSE,
    review_token   UUID         NOT NULL DEFAULT gen_random_uuid()
);

CREATE INDEX ix_room_check_result_room_id     ON kenketsu.room_check_result (room_id);
CREATE INDEX ix_room_check_result_checked_at  ON kenketsu.room_check_result (checked_at DESC);
CREATE INDEX ix_room_check_result_has_changes ON kenketsu.room_check_result (has_changes) WHERE has_changes = TRUE;
