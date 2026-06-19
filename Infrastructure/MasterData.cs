using KenketsuNote.Data;

namespace KenketsuNote.Infrastructure;

public static class MasterData
{
    public static CenterBlock[] CenterBlocks { get; private set; } = [];
    public static Pref[]        Prefectures  { get; private set; } = [];
    public static KenketsuRoom[] Rooms       { get; private set; } = [];

    public static void Load()
    {
        using var db = new KenketsuNoteContext();
        CenterBlocks = [.. db.CenterBlocks.OrderBy(cb => cb.DisplayOrder)];
        Prefectures  = [.. db.Prefs.OrderBy(p => p.DisplayOrder)];
        Rooms        = [.. db.KenketsuRooms.OrderBy(r => r.DisplayOrder)];
    }
}
