-- KenketsuNote: トラッカーテーブル作成 + users.gender 列追加
-- 既存の ashiato スキーマ（献血のあしあとのDB）に対して実行してください

BEGIN;

-- users テーブルに gender 列を追加（なければ）
ALTER TABLE ashiato.users
    ADD COLUMN IF NOT EXISTS gender VARCHAR(6);  -- 'male' / 'female' / NULL

CREATE TABLE IF NOT EXISTS ashiato.kenketsu_record (
    id SERIAL PRIMARY KEY,
    user_id VARCHAR(10) NOT NULL,
    donation_date DATE NOT NULL,
    donation_type VARCHAR(20) NOT NULL,  -- whole_200 / whole_400 / plasma / platelet
    record_type VARCHAR(10) NOT NULL,    -- plan / actual
    volume_ml INT,
    component_count INT,
    room_id INT REFERENCES ashiato.kenketsu_room(room_id) ON DELETE SET NULL,
    notes VARCHAR(500),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_kenketsu_record_user_id
    ON ashiato.kenketsu_record (user_id);
CREATE INDEX IF NOT EXISTS idx_kenketsu_record_user_date
    ON ashiato.kenketsu_record (user_id, donation_date);

CREATE TABLE IF NOT EXISTS ashiato.kenketsu_restriction (
    id SERIAL PRIMARY KEY,
    user_id VARCHAR(10) NOT NULL,
    start_date DATE NOT NULL,
    duration_days INT NOT NULL,
    reason VARCHAR(500),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_kenketsu_restriction_user_id
    ON ashiato.kenketsu_restriction (user_id);

COMMIT;
