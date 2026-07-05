-- room_check_result テストデータ
-- ※ room_idはDBに実在するルームから自動取得
-- ※ review_token は固定値（ブラウザ確認用）

-- テスト用変数
DO $$
DECLARE
    v_room_id_1 INT;
    v_room_id_2 INT;
BEGIN
    -- 差分あり用ルーム（1件目）
    SELECT room_id INTO v_room_id_1
    FROM kenketsu.kenketsu_room
    WHERE NOT is_closed AND room_url IS NOT NULL
    ORDER BY room_id
    LIMIT 1;

    -- 差分なし用ルーム（2件目）
    SELECT room_id INTO v_room_id_2
    FROM kenketsu.kenketsu_room
    WHERE NOT is_closed AND room_url IS NOT NULL
    ORDER BY room_id
    OFFSET 1 LIMIT 1;

    -- ① 差分あり（定休日・血小板・受付時間が変わった想定）
    INSERT INTO kenketsu.room_check_result
        (room_id, gemini_result, changes, has_changes, resolved, review_token)
    VALUES (
        v_room_id_1,
        jsonb_build_object(
            'city',        (SELECT city FROM kenketsu.kenketsu_room WHERE room_id = v_room_id_1),
            'can_whole',   (SELECT can_whole FROM kenketsu.kenketsu_room WHERE room_id = v_room_id_1),
            'can_plasma',  (SELECT can_plasma FROM kenketsu.kenketsu_room WHERE room_id = v_room_id_1),
            'can_platelet', false,      -- DBと異なる値（テスト用）
            'closed_days', '月曜・祝日', -- DBと異なる値（テスト用）
            'business_hours', jsonb_build_array(
                jsonb_build_object(
                    'day_type', 0,
                    'whole_reception_start', '09:30',  -- DBと異なる値（テスト用）
                    'whole_reception_end',   '17:00',
                    'whole_lunch_start',     '12:00',
                    'whole_lunch_end',       '13:00',
                    'comp_reception_start',  '09:30',
                    'comp_reception_end',    '16:30',
                    'comp_lunch_start',      '12:00',
                    'comp_lunch_end',        '13:00'
                ),
                jsonb_build_object(
                    'day_type', 1,
                    'whole_reception_start', '10:00',
                    'whole_reception_end',   '17:00',
                    'whole_lunch_start',     null,
                    'whole_lunch_end',       null,
                    'comp_reception_start',  '10:00',
                    'comp_reception_end',    '16:30',
                    'comp_lunch_start',      null,
                    'comp_lunch_end',        null
                )
            ),
            'has_changes', true,
            'changes', jsonb_build_array(
                '血小板成分の受付が終了しました（可→不可）',
                '定休日が変更されています',
                '平日の全血・成分受付開始時刻が変更されています（09:00→09:30）'
            )
        )::text,
        '血小板成分の受付が終了しました（可→不可）, 定休日が変更されています, 平日の受付開始時刻が変更されています',
        true,
        false,
        'aaaaaaaa-0000-0000-0000-000000000001'::uuid
    );

    -- ② 差分なし（参考用）
    INSERT INTO kenketsu.room_check_result
        (room_id, gemini_result, changes, has_changes, resolved, review_token)
    VALUES (
        v_room_id_2,
        jsonb_build_object(
            'city',         (SELECT city FROM kenketsu.kenketsu_room WHERE room_id = v_room_id_2),
            'can_whole',    (SELECT can_whole FROM kenketsu.kenketsu_room WHERE room_id = v_room_id_2),
            'can_plasma',   (SELECT can_plasma FROM kenketsu.kenketsu_room WHERE room_id = v_room_id_2),
            'can_platelet', (SELECT can_platelet FROM kenketsu.kenketsu_room WHERE room_id = v_room_id_2),
            'closed_days',  (SELECT closed_days FROM kenketsu.kenketsu_room WHERE room_id = v_room_id_2),
            'business_hours', '[]'::jsonb,
            'has_changes', false,
            'changes', '[]'::jsonb
        )::text,
        null,
        false,
        false,
        'bbbbbbbb-0000-0000-0000-000000000002'::uuid
    );

    RAISE NOTICE '挿入完了: room_id_1=%, room_id_2=%', v_room_id_1, v_room_id_2;
    RAISE NOTICE '差分ありレビューURL: /admin/room-check/<id>?token=aaaaaaaa-0000-0000-0000-000000000001';
END $$;

-- 挿入されたIDとURLを確認
SELECT
    id,
    room_id,
    has_changes,
    resolved,
    '/admin/room-check/' || id || '?token=' || review_token AS review_url
FROM kenketsu.room_check_result
ORDER BY id DESC
LIMIT 2;
