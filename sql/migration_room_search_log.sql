-- 献血ルーム検索ログテーブル
CREATE TABLE kenketsu.room_search_log (
    id                  BIGSERIAL PRIMARY KEY,
    searched_at         TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    center_block_id     INT,
    pref_id             INT,
    room_name           VARCHAR(100),
    city                VARCHAR(50),
    can_whole           BOOLEAN,
    can_plasma          BOOLEAN,
    can_platelet        BOOLEAN,
    whole_only          BOOLEAN       NOT NULL DEFAULT FALSE,
    plasma_only         BOOLEAN       NOT NULL DEFAULT FALSE,
    open_dows           VARCHAR(50),
    no_lunch_break_dows VARCHAR(20),
    time_day_type       INT,
    whole_open_by       TIME,
    whole_close_after   TIME,
    comp_open_by        TIME,
    comp_close_after    TIME,
    include_closed      BOOLEAN       NOT NULL DEFAULT FALSE,
    result_count        INT,
    ip_address          VARCHAR(45),
    user_agent          VARCHAR(500)
);

CREATE INDEX ix_room_search_log_searched_at ON kenketsu.room_search_log (searched_at DESC);
