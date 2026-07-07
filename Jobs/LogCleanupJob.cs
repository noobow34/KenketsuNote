using KenketsuNote.Data;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace KenketsuNote.Jobs;

public class LogCleanupJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var services = context.Scheduler.Context["services"] as IServiceProvider
            ?? throw new InvalidOperationException("IServiceProvider がスケジューラコンテキストに登録されていません");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KenketsuNoteContext>();

        var state = await db.RoomCheckJobStates.FindAsync(1);
        var retentionDays = state?.LogRetentionDays ?? 90;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);

        var deletedAccess = await db.AccessLogs
            .Where(l => l.AccessedAt < cutoff)
            .ExecuteDeleteAsync();

        var deletedSearch = await db.RoomSearchLogs
            .Where(l => l.SearchedAt < cutoff)
            .ExecuteDeleteAsync();

        Console.WriteLine($"[LogCleanupJob] 保持期間:{retentionDays}日 アクセスログ:{deletedAccess}件 検索ログ:{deletedSearch}件 削除完了");
    }
}
