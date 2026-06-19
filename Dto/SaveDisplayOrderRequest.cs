namespace KenketsuNote.Dto;

public class SaveDisplayOrderRequest
{
    public string? UserId { get; set; }
    public List<CenterOrderDto>? Regions { get; set; }
}

public class CenterOrderDto
{
    public int CenterBlockId { get; set; }
    public int DisplayOrder { get; set; }
    public List<PrefOrderDto>? Prefectures { get; set; }
}

public class PrefOrderDto
{
    public int PrefId { get; set; }
    public int DisplayOrder { get; set; }
}
