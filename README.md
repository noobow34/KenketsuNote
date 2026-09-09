# 🩸 けんけつノート

献血の記録・計画管理サービスです。

**🌐 本番サイト: https://kenketsu.noobow.me/**

## ✨ 機能

- 🗾 **全国スタンプ** — 全国の献血ルームをスタンプ帳形式で記録
- 📅 **計画管理** — 献血の予定・実績を管理し、インターバルや年間上限を自動チェック（実績には採血した腕・使用機器・採血単位数も記録可能）
- ⭐ **お気に入りルーム** — よく行く献血ルームをユーザーごとに登録し、ルーム選択時にすぐ呼び出し
- 🔗 **シェア機能** — 閲覧専用URLを発行してスタンプ帳をSNSでシェア（献血履歴の公開可否も設定可能）
- 🩸 **ルーム別履歴** — 全国スタンプページから各ルームの献血履歴（計画管理の実績）を確認
- 🔒 **ログイン不要** — ユーザー専用URLで管理（会員登録なし）

## 🛠️ 技術スタック

- **フレームワーク**: ASP.NET Core MVC (.NET 10)
- **データベース**: PostgreSQL（スキーマ: `kenketsu`）
- **ORM**: Entity Framework Core + Npgsql

## 📁 プロジェクト構成

```
Controllers/
  HomeController.cs       # トップページ・ユーザー作成・CheckUser API
  UserController.cs       # マイページHub (/u/{id})
  StampController.cs      # 全国スタンプ (/u/{id}/stamp)
  TrackerController.cs    # 計画管理 (/u/{id}/tracker)
  ManualController.cs     # ヘルプページ (/manual)
  AvailabilityController.cs # 献血空き横断検索の案内ページ (/availability)
  AdminController.cs      # 管理用API
Data/                     # EF Core エンティティ
Services/
  KenketsuLimitService.cs # 献血インターバル・年間上限計算ロジック
  JobScheduleService.cs   # ジョブ設定（DB）をQuartzスケジューラへ反映
Jobs/                     # Quartzジョブ
  RoomInfoCheckJob.cs     # 献血ルーム公式ページとDBの差分をGeminiでチェック
  LogCleanupJob.cs        # アクセスログ・検索ログを保持期間で自動削除
  JobRegistry.cs          # 管理画面で管理するジョブの定義一覧
sql/                      # DDL・マイグレーションSQL
```

## ⏰ 自動実行ジョブ（Quartz）

ジョブの有効/無効と実行スケジュール（cron式・JST解釈）は管理画面「ジョブ管理」から変更でき、
設定は `kenketsu.job_schedule` テーブルに保存されます（起動時にこの内容でトリガーを登録）。
ジョブを追加する場合は `Jobs/JobRegistry.cs` に定義を1行足せば、Quartzへの登録と管理画面の表示に反映されます。

## 🔗 URL構造

| URL | 内容 |
|-----|------|
| `/` | トップページ |
| `/u/{userId}` | マイページHub |
| `/u/{userId}/stamp` | 🗾 全国スタンプ |
| `/u/{userId}/tracker` | 📅 計画管理 |
| `/manual` | 📖 ヘルプ |
| `/availability` | 💻 献血空き横断検索（PC用アプリ案内） |
| `/s/{shareId}` | 👁️ スタンプ閲覧共有ページ |
| `/healthz` | 🩺 ヘルスチェック（デプロイスクリプト用・DB到達確認を含む） |

## 🚀 セットアップ

### 必要環境

- .NET 10 SDK
- PostgreSQL

### 🗄️ データベース

スキーマ・テーブル定義は `sql/migration.sql` に統合されています。新規環境ではこれを1度だけ実行してください。

```bash
psql "$KENKETSUNOTE_CONNECTION_STRING" -f sql/migration.sql
```

既存DBへの列追加・変更は `migration.sql` には反映されない（＝新規構築用のファイル）ため、
`ALTER TABLE` を個別に実行してください。

動作確認用のテストデータは `sql/testdata_*.sql` にあります（任意・実行前に既存データを確認してください）。

## 🔄 関連リポジトリ

- [KenketsuNoAshiato](https://github.com/noobow34/KenketsuNoAshiato) — 旧サービス「献血のあしあと」からのリダイレクトサイト
