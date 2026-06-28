using KenketsuNote.Data;

namespace KenketsuNote.Services;

/// <summary>
/// 献血の制限チェック・次回可能日計算・再スケジュール計算サービス
/// </summary>
public class KenketsuLimitService
{
    // ── 定数 ─────────────────────────────────────────────────────
    private const int RollingWeeks      = 52;
    private const int ComponentMaxCount = 24;

    // 200ml全血後：男女共通でいずれの献血も4週後
    private const int AnyAfter200Days             = 4  * 7;  // 28日

    // 400ml全血後
    private const int WholeAfterWhole400MaleDays   = 12 * 7;  // 84日（男性）
    private const int WholeAfterWhole400FemaleDays = 16 * 7;  // 112日（女性 → 400ml）
    private const int ComponentAfterWhole400Days   = 8  * 7;  // 56日（男女共通）

    // 成分後
    private const int AnyAfterComponentDays = 2 * 7;  // 14日

    private readonly int     _wholeMaxMl;
    private readonly string? _gender;

    public KenketsuLimitService(int wholeMaxMl = 1200, string? gender = null)
    {
        _wholeMaxMl = wholeMaxMl;
        _gender     = gender;
    }

    public static int WholeMaxMlForGender(string? gender) => gender == "female" ? 800 : 1200;

    // ── 公開メソッド ──────────────────────────────────────────────

    public ValidationResult Validate(
        DateOnly targetDate,
        string donationType,
        IReadOnlyList<KenketsuRecord> records,
        IReadOnlyList<KenketsuRestriction>? restrictions = null,
        int? excludeId = null)
    {
        var others = records.Where(r => excludeId == null || r.Id != excludeId).ToList();

        if (restrictions != null)
        {
            // 実効開始日の判定は excludeId を除かない全記録で行う
            // （編集対象の実績が制限開始日と同日の場合でも正しく翌日開始と判断するため）
            var allForRestriction = records.ToList();
            var hit = restrictions.FirstOrDefault(r => EffectiveContains(r, targetDate, allForRestriction));
            if (hit != null)
                return ValidationResult.Fail(
                    $"手動制限期間中です（{hit.StartDate:yyyy/MM/dd}〜{hit.EndDate:yyyy/MM/dd}）" +
                    (hit.Reason != null ? $"：{hit.Reason}" : ""));
        }

        var intervalError = CheckInterval(targetDate, donationType, others);
        if (intervalError != null) return ValidationResult.Fail(intervalError);

        var limitError = CheckRollingLimit(targetDate, donationType, others);
        if (limitError != null) return ValidationResult.Fail(limitError);

        return ValidationResult.Ok();
    }

    public DateOnly EarliestPossibleDate(
        DateOnly from,
        string donationType,
        IReadOnlyList<KenketsuRecord> records,
        IReadOnlyList<KenketsuRestriction>? restrictions = null,
        int? excludeId = null)
        => EarliestPossibleDateWithReason(from, donationType, records, restrictions, excludeId).Date;

    public (DateOnly Date, bool LimitConstrained, bool RestrictionConstrained)
        EarliestPossibleDateWithReason(
            DateOnly from,
            string donationType,
            IReadOnlyList<KenketsuRecord> records,
            IReadOnlyList<KenketsuRestriction>? restrictions = null,
            int? excludeId = null)
    {
        var others = records.Where(r => excludeId == null || r.Id != excludeId).ToList();

        DateOnly intervalEarliest = from;
        for (int i = 0; i < 365 * 3; i++)
        {
            if (CheckInterval(intervalEarliest, donationType, others) == null)
                break;
            intervalEarliest = intervalEarliest.AddDays(1);
        }

        DateOnly afterLimit = intervalEarliest;
        for (int i = 0; i < 365 * 3; i++)
        {
            if (CheckRollingLimit(afterLimit, donationType, others) == null)
                break;
            afterLimit = afterLimit.AddDays(1);
        }

        DateOnly actual = afterLimit;
        if (restrictions != null && restrictions.Count > 0)
        {
            for (int i = 0; i < 365 * 3; i++)
            {
                var hit = restrictions.FirstOrDefault(r => EffectiveContains(r, actual, others));
                if (hit == null) break;
                actual = hit.EndDate.AddDays(1);
            }
        }

        bool limitConstrained       = afterLimit > intervalEarliest;
        bool restrictionConstrained = actual > afterLimit;
        return (actual, limitConstrained, restrictionConstrained);
    }

    public IReadOnlyList<RescheduleProposal> CalculateReschedule(
        KenketsuRecord triggerRecord,
        IReadOnlyList<KenketsuRecord> allRecords,
        IReadOnlyList<KenketsuRestriction>? restrictions = null)
    {
        var futurePlans = allRecords
            .Where(r => r.IsPlan && r.DonationDate > triggerRecord.DonationDate)
            .OrderBy(r => r.DonationDate)
            .ToList();

        if (futurePlans.Count == 0) return [];

        var proposals = new List<RescheduleProposal>();

        var simRecords = allRecords
            .Where(r => r.IsActual || r.Id == triggerRecord.Id)
            .Select(Clone)
            .ToList();

        var gapBase = triggerRecord.DonationDate;
        var originalGaps = futurePlans.Select(p =>
        {
            int gap = p.DonationDate.DayNumber - gapBase.DayNumber;
            gapBase = p.DonationDate;
            return gap;
        }).ToList();

        var prevDate = triggerRecord.DonationDate;
        for (int i = 0; i < futurePlans.Count; i++)
        {
            var plan    = futurePlans[i];
            var ideal   = prevDate.AddDays(originalGaps[i]);
            var newDate = EarliestPossibleDate(ideal, plan.DonationType, simRecords, restrictions);

            if (newDate != plan.DonationDate)
            {
                proposals.Add(new RescheduleProposal
                {
                    RecordId     = plan.Id,
                    DonationType = plan.DonationType,
                    OriginalDate = plan.DonationDate,
                    NewDate      = newDate,
                });
            }

            var sim = Clone(plan);
            sim.DonationDate = newDate;
            simRecords.Add(sim);
            prevDate = newDate;
        }

        return proposals;
    }

    public KenketsuSummary CalculateSummary(
        DateOnly baseDate,
        IReadOnlyList<KenketsuRecord> records,
        IReadOnlyList<KenketsuRestriction>? restrictions = null)
    {
        var windowStart = baseDate.AddDays(-(RollingWeeks * 7 - 1));
        var inWindow = records
            .Where(r => r.DonationDate >= windowStart && r.DonationDate <= baseDate)
            .ToList();

        int usedMl    = inWindow.Where(r => r.IsWhole).Sum(r => r.VolumeMl ?? 0);
        int usedCount = inWindow.Where(r => r.IsComponent).Sum(r => r.ComponentCount ?? 0);

        var (wholeDate, wholeLC, wholeRC) = TryEarliestDateWithReason("whole_400", baseDate, records, restrictions);
        var (compDate,  compLC,  compRC)  = TryEarliestDateWithReason("plasma",    baseDate, records, restrictions);

        var activeRestrictions = restrictions?
            .Where(r => r.EndDate >= baseDate)
            .OrderBy(r => r.StartDate)
            .ToList() ?? [];

        return new KenketsuSummary
        {
            UsedVolumeMl                        = usedMl,
            MaxVolumeMl                         = _wholeMaxMl,
            UsedComponentCount                  = usedCount,
            MaxComponentCount                   = ComponentMaxCount,
            NextWholePossible                   = wholeDate,
            NextComponentPossible               = compDate,
            NextWholeLimitConstrained           = wholeLC,
            NextComponentLimitConstrained       = compLC,
            NextWholeRestrictionConstrained     = wholeRC,
            NextComponentRestrictionConstrained = compRC,
            ActiveRestrictions                  = activeRestrictions,
        };
    }

    public IReadOnlyList<DateRangeInfo> GetIntervalConstrainedRanges(
        DateOnly from, DateOnly to,
        IReadOnlyList<KenketsuRecord> records)
    {
        var result = new List<DateRangeInfo>();
        var sorted = records.OrderBy(r => r.DonationDate).ToList();

        string? prevKind  = null;
        DateOnly? rangeStart = null;

        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var lastWhole = sorted.LastOrDefault(r => r.DonationDate < d && r.IsWhole);
            var lastComp  = sorted.LastOrDefault(r => r.DonationDate < d && !r.IsWhole);
            var lastAny   = sorted.LastOrDefault(r => r.DonationDate < d);
            string? kind = null;

            if (lastAny != null)
            {
                bool wholeBlocked = false;
                bool compBlocked  = false;

                // 全血インターバル：最後の全血 + 最後の成分 を独立評価し、どちらかが引っかかればブロック
                if (lastWhole != null)
                {
                    int dw = d.DayNumber - lastWhole.DonationDate.DayNumber;
                    if (lastWhole.DonationType == "whole_200")
                        wholeBlocked |= dw < AnyAfter200Days;
                    else
                        wholeBlocked |= dw < (_gender == "female" ? WholeAfterWhole400FemaleDays : WholeAfterWhole400MaleDays);
                }
                if (lastComp != null)
                {
                    int dc = d.DayNumber - lastComp.DonationDate.DayNumber;
                    wholeBlocked |= dc < AnyAfterComponentDays;
                }

                // 成分インターバルは直前の献血（全血・成分問わず）を起点にする
                {
                    int daysFromLast = d.DayNumber - lastAny.DonationDate.DayNumber;
                    if (lastAny.DonationType == "whole_200")
                        compBlocked = daysFromLast < AnyAfter200Days;
                    else if (lastAny.IsWhole)
                        compBlocked = daysFromLast < ComponentAfterWhole400Days;
                    else
                        compBlocked = daysFromLast < AnyAfterComponentDays;
                }

                if (wholeBlocked && compBlocked)
                    kind = "interval_both";
                else if (wholeBlocked)
                    kind = "interval_whole";
            }

            if (kind != prevKind)
            {
                if (prevKind != null && rangeStart != null)
                    result.Add(new DateRangeInfo(rangeStart.Value, d.AddDays(-1), prevKind));
                rangeStart = kind != null ? d : null;
                prevKind   = kind;
            }
        }
        if (prevKind != null && rangeStart != null)
            result.Add(new DateRangeInfo(rangeStart.Value, to, prevKind));

        return result;
    }

    public IReadOnlyList<DateRangeInfo> GetLimitConstrainedRanges(
        DateOnly from, DateOnly to,
        IReadOnlyList<KenketsuRecord> records)
    {
        var result    = new List<DateRangeInfo>();
        string? prevKind  = null;
        DateOnly? rangeStart = null;

        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var windowStart = d.AddDays(-(RollingWeeks * 7 - 1));
            var inWindow = records
                .Where(r => r.DonationDate >= windowStart && r.DonationDate <= d)
                .ToList();

            int usedMl    = inWindow.Where(r => r.IsWhole).Sum(r => r.VolumeMl ?? 0);
            int usedCount = inWindow.Where(r => r.IsComponent).Sum(r => r.ComponentCount ?? 0);

            bool wholeLimit = usedMl    >= _wholeMaxMl;
            bool compLimit  = usedCount >= ComponentMaxCount;

            string? kind = (wholeLimit, compLimit) switch
            {
                (true,  true)  => "limit_both",
                (true,  false) => "limit_whole",
                (false, true)  => "limit_comp",
                _              => null,
            };

            if (kind != prevKind)
            {
                if (prevKind != null && rangeStart != null)
                    result.Add(new DateRangeInfo(rangeStart.Value, d.AddDays(-1), prevKind));
                rangeStart = kind != null ? d : null;
                prevKind   = kind;
            }
        }
        if (prevKind != null && rangeStart != null)
            result.Add(new DateRangeInfo(rangeStart.Value, to, prevKind));

        return result;
    }

    // ── プライベート ──────────────────────────────────────────────

    private static DateOnly EffectiveStartDate(
        KenketsuRestriction restriction,
        IList<KenketsuRecord> records)
    {
        bool hasDonationOnStart = records.Any(r => r.DonationDate == restriction.StartDate);
        return hasDonationOnStart
            ? restriction.StartDate.AddDays(1)
            : restriction.StartDate;
    }

    private static bool EffectiveContains(
        KenketsuRestriction restriction,
        DateOnly date,
        IList<KenketsuRecord> records)
    {
        var effectiveStart = EffectiveStartDate(restriction, records);
        return date >= effectiveStart && date <= restriction.EndDate;
    }

    private string? CheckInterval(
        DateOnly targetDate,
        string donationType,
        IList<KenketsuRecord> others)
    {
        var isWholeTarget = donationType is "whole_200" or "whole_400";

        if (isWholeTarget)
        {
            // 全血を狙う場合：全血インターバルと成分インターバルを独立に評価し、厳しい方を返す
            var lastWhole = others.Where(r => r.DonationDate < targetDate && r.IsWhole).MaxBy(r => r.DonationDate);
            var lastComp  = others.Where(r => r.DonationDate < targetDate && !r.IsWhole).MaxBy(r => r.DonationDate);

            string? wholeErr = null;
            if (lastWhole != null)
            {
                int days = targetDate.DayNumber - lastWhole.DonationDate.DayNumber;
                if (lastWhole.DonationType == "whole_200")
                {
                    if (days < AnyAfter200Days)
                        wholeErr = $"直前の200ml全血献血（{lastWhole.DonationDate:yyyy/MM/dd}）から4週間後の {lastWhole.DonationDate.AddDays(AnyAfter200Days):yyyy/MM/dd} 以降に可能です。";
                }
                else
                {
                    int required = (donationType == "whole_400" && _gender == "female") ? WholeAfterWhole400FemaleDays : WholeAfterWhole400MaleDays;
                    if (days < required)
                        wholeErr = $"直前の400ml全血献血（{lastWhole.DonationDate:yyyy/MM/dd}）から{required / 7}週間後の {lastWhole.DonationDate.AddDays(required):yyyy/MM/dd} 以降に可能です。";
                }
            }
            string? compErr = null;
            if (lastComp != null)
            {
                int days = targetDate.DayNumber - lastComp.DonationDate.DayNumber;
                if (days < AnyAfterComponentDays)
                    compErr = $"直前の成分献血（{lastComp.DonationDate:yyyy/MM/dd}）から2週間後の {lastComp.DonationDate.AddDays(AnyAfterComponentDays):yyyy/MM/dd} 以降に可能です。";
            }
            // 両方ある場合は可能日が遅い方を返す
            if (wholeErr != null && compErr != null)
            {
                var wholeOk = lastWhole!.DonationDate.AddDays(lastWhole.DonationType == "whole_200" ? AnyAfter200Days : ((donationType == "whole_400" && _gender == "female") ? WholeAfterWhole400FemaleDays : WholeAfterWhole400MaleDays));
                var compOk  = lastComp!.DonationDate.AddDays(AnyAfterComponentDays);
                return wholeOk >= compOk ? wholeErr : compErr;
            }
            return wholeErr ?? compErr;
        }

        var last = others
            .Where(r => r.DonationDate < targetDate)
            .MaxBy(r => r.DonationDate);
        if (last == null) return null;

        int daysSince = targetDate.DayNumber - last.DonationDate.DayNumber;

        if (last.DonationType == "whole_200")
        {
            // 200ml後：男女共通でいずれも4週後
            if (daysSince < AnyAfter200Days)
            {
                var earliest = last.DonationDate.AddDays(AnyAfter200Days);
                return $"直前の200ml全血献血（{last.DonationDate:yyyy/MM/dd}）から" +
                       $"4週間後の {earliest:yyyy/MM/dd} 以降に可能です。";
            }
        }
        else if (last.DonationType == "whole_400")
        {
            int required;
            if (donationType is "whole_200" or "whole_400")
            {
                // 女性が400mlを狙う場合のみ16週、それ以外（男性 or 200ml）は12週
                required = (donationType == "whole_400" && _gender == "female")
                    ? WholeAfterWhole400FemaleDays
                    : WholeAfterWhole400MaleDays;
            }
            else
            {
                required = ComponentAfterWhole400Days;
            }

            if (daysSince < required)
            {
                var earliest = last.DonationDate.AddDays(required);
                return $"直前の400ml全血献血（{last.DonationDate:yyyy/MM/dd}）から" +
                       $"{required / 7}週間後の {earliest:yyyy/MM/dd} 以降に可能です。";
            }
        }
        else
        {
            if (daysSince < AnyAfterComponentDays)
            {
                var earliest = last.DonationDate.AddDays(AnyAfterComponentDays);
                return $"直前の成分献血（{last.DonationDate:yyyy/MM/dd}）から" +
                       $"2週間後の {earliest:yyyy/MM/dd} 以降に可能です。";
            }
        }
        return null;
    }

    private string? CheckRollingLimit(
        DateOnly targetDate,
        string donationType,
        IList<KenketsuRecord> others)
    {
        var windowStart = targetDate.AddDays(-(RollingWeeks * 7 - 1));
        var inWindow = others
            .Where(r => r.DonationDate >= windowStart && r.DonationDate <= targetDate)
            .ToList();

        if (donationType is "whole_200" or "whole_400")
        {
            int add  = donationType == "whole_200" ? 200 : 400;
            int used = inWindow.Where(r => r.IsWhole).Sum(r => r.VolumeMl ?? 0);
            if (used + add > _wholeMaxMl)
                return $"直近52週の全血献血量が上限（{_wholeMaxMl}ml）を超えます。" +
                       $"（使用済み {used}ml ＋ 追加 {add}ml）";
        }
        else
        {
            int add  = donationType == "platelet" ? 2 : 1;
            int used = inWindow.Where(r => r.IsComponent).Sum(r => r.ComponentCount ?? 0);
            if (used + add > ComponentMaxCount)
                return $"直近52週の成分献血回数が上限（{ComponentMaxCount}回）を超えます。" +
                       $"（使用済み {used}回 ＋ 追加 {add}回）";
        }
        return null;
    }

    private (DateOnly? Date, bool LimitConstrained, bool RestrictionConstrained)
        TryEarliestDateWithReason(
            string donationType, DateOnly from,
            IReadOnlyList<KenketsuRecord> records,
            IReadOnlyList<KenketsuRestriction>? restrictions)
    {
        try
        {
            var (date, lc, rc) = EarliestPossibleDateWithReason(from, donationType, records, restrictions);
            return (date, lc, rc);
        }
        catch { return (null, false, false); }
    }

    private static KenketsuRecord Clone(KenketsuRecord r) => new()
    {
        Id             = r.Id,
        UserId         = r.UserId,
        DonationDate   = r.DonationDate,
        DonationType   = r.DonationType,
        RecordType     = r.RecordType,
        VolumeMl       = r.VolumeMl,
        ComponentCount = r.ComponentCount,
    };
}

// ── DTO ───────────────────────────────────────────────────────────

public class ValidationResult
{
    public bool    IsValid      { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static ValidationResult Ok()             => new() { IsValid = true };
    public static ValidationResult Fail(string msg) => new() { IsValid = false, ErrorMessage = msg };
}

public class RescheduleProposal
{
    public int      RecordId     { get; init; }
    public string   DonationType { get; init; } = "";
    public DateOnly OriginalDate { get; init; }
    public DateOnly NewDate      { get; init; }
}

public record DateRangeInfo(DateOnly Start, DateOnly End, string Kind);

public class KenketsuSummary
{
    public int       UsedVolumeMl                        { get; init; }
    public int       MaxVolumeMl                         { get; init; }
    public int       UsedComponentCount                  { get; init; }
    public int       MaxComponentCount                   { get; init; }
    public DateOnly? NextWholePossible                   { get; init; }
    public DateOnly? NextComponentPossible               { get; init; }
    public bool      NextWholeLimitConstrained           { get; init; }
    public bool      NextComponentLimitConstrained       { get; init; }
    public bool      NextWholeRestrictionConstrained     { get; init; }
    public bool      NextComponentRestrictionConstrained { get; init; }
    public IReadOnlyList<KenketsuRestriction> ActiveRestrictions { get; init; } = [];

    public int RemainingMl    => MaxVolumeMl       - UsedVolumeMl;
    public int RemainingCount => MaxComponentCount - UsedComponentCount;
}
