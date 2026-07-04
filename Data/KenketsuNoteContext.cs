using Microsoft.EntityFrameworkCore;

namespace KenketsuNote.Data;

public partial class KenketsuNoteContext : DbContext
{
    public KenketsuNoteContext() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    public KenketsuNoteContext(DbContextOptions<KenketsuNoteContext> options) : base(options)
        => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    // ── スタンプ関連 ──────────────────────────────────────
    public virtual DbSet<CenterBlock> CenterBlocks { get; set; }
    public virtual DbSet<Pref> Prefs { get; set; }
    public virtual DbSet<KenketsuRoom> KenketsuRooms { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<VisitStamp> VisitStamps { get; set; }
    public virtual DbSet<ShareMapping> ShareMappings { get; set; }
    public virtual DbSet<CenterBlockOrder> CenterBlockOrders { get; set; }
    public virtual DbSet<PrefOrder> PrefOrders { get; set; }

    // ── 検索ログ ──────────────────────────────────────────
    public virtual DbSet<RoomSearchLog> RoomSearchLogs { get; set; }

    // ── トラッカー関連 ────────────────────────────────────
    public virtual DbSet<RoomBusinessHours> RoomBusinessHours { get; set; }
    public virtual DbSet<KenketsuRecord> KenketsuRecords { get; set; }
    public virtual DbSet<KenketsuRestriction> KenketsuRestrictions { get; set; }
    public virtual DbSet<KenketsuRestrictionPreset> KenketsuRestrictionPresets { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        string connectionString = Environment.GetEnvironmentVariable("KENKETSUNOTE_CONNECTION_STRING") ?? "";
        optionsBuilder.UseNpgsql(connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("kenketsu");

        modelBuilder.Entity<CenterBlock>(entity =>
        {
            entity.HasKey(e => e.CenterBlockId).HasName("center_block_pkey");
        });

        modelBuilder.Entity<KenketsuRoom>(entity =>
        {
            entity.HasKey(e => e.RoomId).HasName("kenketsu_room_pkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("users_pkey");
        });

        modelBuilder.Entity<VisitStamp>(entity =>
        {
            entity.HasKey(e => e.StampId).HasName("visit_stamp_pkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
