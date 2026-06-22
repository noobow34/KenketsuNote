# 🩸 けんけつノート

献血の記録・計画管理サービスです。

**🌐 本番サイト: https://kenketsu.noobow.me/**

## ✨ 機能

- 🗾 **全国スタンプ** — 全国の献血ルームをスタンプ帳形式で記録
- 📅 **計画管理** — 献血の予定・実績を管理し、インターバルや年間上限を自動チェック
- 🔗 **シェア機能** — 閲覧専用URLを発行してスタンプ帳をSNSでシェア
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
  ManualController.cs     # ヘルプページ (/u/{id}/manual)
  AdminController.cs      # 管理用API
Data/                     # EF Core エンティティ
Services/
  KenketsuLimitService.cs # 献血インターバル・年間上限計算ロジック
sql/                      # DDL・マイグレーションSQL
```

## 🔗 URL構造

| URL | 内容 |
|-----|------|
| `/` | トップページ |
| `/u/{userId}` | マイページHub |
| `/u/{userId}/stamp` | 🗾 全国スタンプ |
| `/u/{userId}/tracker` | 📅 計画管理 |
| `/u/{userId}/manual` | 📖 ヘルプ |
| `/s/{shareId}` | 👁️ スタンプ閲覧共有ページ |

## 🚀 セットアップ

### 必要環境

- .NET 10 SDK
- PostgreSQL

### 🗄️ データベース

```sql
-- スキーマ・テーブル作成
\i sql/create_schema.sql
\i sql/create_tracker_tables.sql
```

## 🔄 関連リポジトリ

- [KenketsuNoAshiato](https://github.com/noobow34/KenketsuNoAshiato) — 旧サービス「献血のあしあと」からのリダイレクトサイト
