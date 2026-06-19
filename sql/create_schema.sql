-- KenketsuNote: 初期スキーマ作成
-- 新規DBに対して1度だけ実行してください
-- 既存の献血のあしあとDBがある場合は migrate_add_user_id.sql を使用してください

BEGIN;

CREATE SCHEMA IF NOT EXISTS kenketsu;

-- ============================================================
-- マスター系（全ユーザー共通）
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
    room_id SERIAL PRIMARY KEY,
    pref INT NOT NULL REFERENCES kenketsu.pref(pref_id),
    room_name VARCHAR(100) NOT NULL,
    display_order INT,
    image_path VARCHAR(100),
    is_closed BOOLEAN NOT NULL DEFAULT FALSE,
    remark VARCHAR(200)
);

-- ============================================================
-- ユーザー系
-- ============================================================

CREATE TABLE IF NOT EXISTS kenketsu.users (
    user_id VARCHAR(10) PRIMARY KEY,
    registered_at TIMESTAMP,
    last_access_at TIMESTAMP,
    user_name VARCHAR(50),
    show_closed_default BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS kenketsu.visit_stamp (
    stamp_id SERIAL PRIMARY KEY,
    user_id VARCHAR(10) NOT NULL REFERENCES kenketsu.users(user_id),
    room_id INT NOT NULL REFERENCES kenketsu.kenketsu_room(room_id),
    visit_date DATE,
    angle NUMERIC(5,1) NOT NULL DEFAULT 0,
    created_at TIMESTAMP(6),
    updated_at TIMESTAMP(6)
);

CREATE TABLE IF NOT EXISTS kenketsu.share_mapping (
    share_id VARCHAR(10) PRIMARY KEY,
    original_id VARCHAR(10) NOT NULL REFERENCES kenketsu.users(user_id)
);

CREATE TABLE IF NOT EXISTS kenketsu.center_block_order (
    order_id SERIAL PRIMARY KEY,
    center_block_id INT NOT NULL REFERENCES kenketsu.center_block(center_block_id),
    user_id VARCHAR(10) NOT NULL REFERENCES kenketsu.users(user_id),
    display_order INT NOT NULL
);

CREATE TABLE IF NOT EXISTS kenketsu.pref_order (
    order_id SERIAL PRIMARY KEY,
    pref_id INT NOT NULL REFERENCES kenketsu.pref(pref_id),
    user_id VARCHAR(10) NOT NULL REFERENCES kenketsu.users(user_id),
    display_order INT NOT NULL
);

-- ============================================================
-- トラッカー系
-- ============================================================

CREATE TABLE IF NOT EXISTS kenketsu.kenketsu_record (
    id SERIAL PRIMARY KEY,
    user_id VARCHAR(10) NOT NULL,
    donation_date DATE NOT NULL,
    donation_type VARCHAR(20) NOT NULL,  -- whole_200 / whole_400 / plasma / platelet
    record_type VARCHAR(10) NOT NULL,    -- plan / actual
    volume_ml INT,
    component_count INT,
    notes VARCHAR(500),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_kenketsu_record_user_id
    ON kenketsu.kenketsu_record (user_id);
CREATE INDEX IF NOT EXISTS idx_kenketsu_record_user_date
    ON kenketsu.kenketsu_record (user_id, donation_date);

CREATE TABLE IF NOT EXISTS kenketsu.kenketsu_restriction (
    id SERIAL PRIMARY KEY,
    user_id VARCHAR(10) NOT NULL,
    start_date DATE NOT NULL,
    duration_days INT NOT NULL,
    reason VARCHAR(500),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_kenketsu_restriction_user_id
    ON kenketsu.kenketsu_restriction (user_id);

COMMIT;
