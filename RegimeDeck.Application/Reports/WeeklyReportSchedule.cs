namespace RegimeDeck.Application.Reports;

/// When a weekly report is due: Mondays (UTC), at most once per user per
/// day — restarts on a Monday can't double-send.
public static class WeeklyReportSchedule
{
    public static bool IsDue(DateTime utcNow, DateTime? lastSentAt) =>
        utcNow.DayOfWeek == DayOfWeek.Monday
        && (lastSentAt is null || lastSentAt.Value < utcNow.Date);
}
