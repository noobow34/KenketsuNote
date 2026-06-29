using KenketsuNote.Data;

namespace KenketsuNote.Models;

public class StampModel
{
    public required User User { get; set; }
    public KenketsuRoom[] Rooms { get; set; } = [];
    public CenterBlock[] CenterBlocks { get; set; } = [];
    public Pref[] Prefectures { get; set; } = [];
    public bool IsShare { get; set; }
    public string ShareId { get; set; } = string.Empty;
    public bool ShowClosedDefault { get; set; } = false;
    public bool ShareShowHistory { get; set; } = false;
}
