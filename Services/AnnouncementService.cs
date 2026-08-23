using KenketsuNote.Data;
using Microsoft.EntityFrameworkCore;

namespace KenketsuNote.Services;

/// <summary>
/// 各ユーザー画面のアクセス時に出すお知らせモーダルの表示判定
/// </summary>
public static class AnnouncementService
{
    /// <summary>
    /// このユーザーに表示すべきお知らせを返す。表示不要なら null。
    /// 公開中のうち最も新しい1件が対象で、そのIDを「次回から表示しない」済みなら表示しない
    /// （より新しいお知らせが公開されればIDが変わるため再び表示される）。
    /// </summary>
    public static AnnouncementView? GetForUser(KenketsuNoteContext db, User user)
    {
        var latest = db.Announcements
            .AsNoTracking()
            .Where(a => a.IsPublished)
            .OrderByDescending(a => a.Id)
            .FirstOrDefault();

        if (latest == null) return null;
        if (user.DismissedAnnouncementId == latest.Id) return null;

        return new AnnouncementView
        {
            Id         = latest.Id,
            UserId     = user.UserId,
            Title      = latest.Title,
            Body       = latest.Body,
            ChangeLogs = LoadChangeLogs(db, latest.Id),
        };
    }

    /// <summary>お知らせに紐づけられた更新履歴を、トップページと同じ並び（公開日の降順）で返す</summary>
    public static List<ChangeLog> LoadChangeLogs(KenketsuNoteContext db, int announcementId)
        => (from link in db.AnnouncementChangeLogs.AsNoTracking()
            join log in db.ChangeLogs.AsNoTracking() on link.ChangeLogId equals log.Id
            where link.AnnouncementId == announcementId
            orderby log.ReleasedOn descending, log.Id descending
            select log)
           .ToList();
}

/// <summary>お知らせモーダルの表示内容</summary>
public class AnnouncementView
{
    public int              Id         { get; init; }
    public string           UserId     { get; init; } = "";
    public string           Title      { get; init; } = "";
    public string?          Body       { get; init; }
    public List<ChangeLog>  ChangeLogs { get; init; } = [];
}
