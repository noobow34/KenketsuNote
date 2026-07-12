using System.Reflection;

namespace KenketsuNote.Infrastructure;

public static class AppVersion
{
    private const string RepoUrl = "https://github.com/noobow34/KenketsuNote";

    public static string CommitHash { get; } =
        Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "CommitHash")?.Value
        ?? "unknown";

    public static string? CommitUrl { get; } =
        CommitHash == "unknown" ? null : $"{RepoUrl}/commit/{CommitHash}";
}
