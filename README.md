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
- **管理者認証**: Cloudflare Access（JWTを `Microsoft.AspNetCore.Authentication.JwtBearer` で検証）

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

## 🔐 管理者認証（Cloudflare Access）

全ページがログイン不要で見える作りのまま、**管理者だけを見分けて**管理機能や管理者向け表示を出しています。
Cloudflare Access はプロキシとして認証しますが、通過したリクエストに JWT を渡してくるので、
これを検証すれば公開ページでも管理者を識別できます。

### 仕組み

1. Access アプリケーションは **`/Account/Login` だけ**を保護する。他のページは Access の外に置く。
2. 管理者が `/Account/Login` を踏むと Cloudflare のログイン画面が出て、通過すると
   ホストに `CF_Authorization` Cookie（中身は JWT）が発行される。
3. この Cookie はパス `/` で発行されるためブラウザが全ページへ送る。
   アプリは `Auth/CloudflareAccess.cs` でこれを検証し、成功すれば `User.Identity.IsAuthenticated` が立つ。
   判定は従来どおり `AdminAuth.IsAdmin` で行う。
4. 公開鍵は `https://{CF_ACCESS_TEAM_DOMAIN}/cdn-cgi/access/certs` から取得して 1 時間キャッシュする。
   Access には OIDC のディスカバリ文書が無いため、`Authority` ではなく `IssuerSigningKeyResolver` で直接引いている。

`ConditionalAuthRedirectMiddleware` は、`ADMIN_KEY`/`ADMIN_VALUE` の Cookie を持つ端末が未認証のときだけ
`/Account/Login` へリダイレクトします。一般の利用者はこの Cookie を持たないのでログイン画面を見ることはありません。
Cookie は `/SetCookie?key=...&value=...` で仕込みます。

`/Account/Logout` は目印の Cookie を消したうえで `/cdn-cgi/access/logout` へ送ります
（消さないとログアウト直後にまたログインへ飛ばされます）。

### Access アプリケーションの設定

| 項目 | 値 |
|-----|-----|
| Application type | Self-hosted |
| Path | `kenketsu.noobow.me/Account/Login` |
| Policy | Allow / Emails → 管理者のメールアドレス |
| IdP | One-time PIN で足りる |

発行された **Application Audience (AUD) タグ**を `CF_ACCESS_AUD` に設定します。

管理機能のパス（`/Admin` など）は **Access の保護対象に入れていません**。
アプリ側が `AdminAuth.IsAdmin` で `NotFound` を返しており、パスの存在自体を隠せるためです。
また、同一ホストに複数の Access アプリケーションを作ると `CF_Authorization` の `aud` が混ざるため、
アプリケーションは 1 つに保ってください。

> **キャッシュ注意**: `/u/{userId}` には管理者向けの表示が含まれます。この HTML がエッジにキャッシュされると
> 一般利用者に出てしまいます。Cloudflare は既定で HTML をキャッシュしませんが、Cache Everything 系の
> ルールは入れないでください。入れる場合は `CF_Authorization` Cookie の有無で Bypass が必須です。

### 🌐 環境変数

| 変数 | 用途 |
|-----|------|
| `KENKETSUNOTE_CONNECTION_STRING` | PostgreSQL の接続文字列 |
| `KENKETSUNOTE_BASE_URL` | 通知などで使う絶対 URL の基点 |
| `CF_ACCESS_TEAM_DOMAIN` | Cloudflare Zero Trust のチームドメイン（例: `noobow.cloudflareaccess.com`）。JWT の `iss` と公開鍵の取得先 |
| `CF_ACCESS_AUD` | Access アプリケーションの Application Audience (AUD) タグ |
| `ADMIN_KEY` / `ADMIN_VALUE` | 管理者の端末を見分けるための Cookie の名前と値。一致した場合のみ未認証時に `/Account/Login` へ自動リダイレクトする |
| `GEMINI_API_KEY` | `RoomInfoCheckJob` のルーム情報差分チェック |
| `SLACK_BOT_TOKEN` / `SLACK_ROOM_CHECK_CHANNEL` | Slack 通知 |
| `CF_ACCESS_DEV_ADMIN` | 開発専用。`ASPNETCORE_ENVIRONMENT=Development` かつ `1` のときだけ、認証済みの管理者になりすます |

### 🚇 公開経路（Cloudflare Tunnel）

`cloudflared` が Kestrel へ直接つなぎます。Caddy は廃止しました（TLS はエッジが終端）。

```
Cloudflare Edge → cloudflared → Kestrel(localhost:6002)
```

`~/.cloudflared/config.yml`:

```yaml
tunnel: <TUNNEL-ID>
credentials-file: /home/noobow/.cloudflared/<TUNNEL-ID>.json

ingress:
  - hostname: kenketsu.noobow.me
    service: http://localhost:6002
  - service: http_status:404
```

`Program.cs` の `UseForwardedHeaders` は `ForwardLimit = null` にしてあります。
信頼できる中継（既定でループバックのみ）が続く限り `X-Forwarded-For` を遡るので、
アクセスログ・検索ログには実クライアント IP が残ります。中継元がループバックでなくなった時点で
止まるため、Cloudflare より手前で偽装して差し込まれた値は採用されません。

gzip / Brotli の圧縮と HSTS ヘッダは Caddy ではなく Cloudflare 側で設定します。

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
