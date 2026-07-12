using System.Globalization;
using System.Net;
using System.Text;
using RegimeDeck.Contracts.Regime;
using RegimeDeck.Contracts.Watchlist;

namespace RegimeDeck.Application.Reports;

/// Assembles the Pro weekly email from what the insight layer already
/// computes: market health, the narrative, the playbook, and the user's
/// watchlist. Pure string building — fully unit-testable.
public static class WeeklyReportBuilder
{
    public static string Subject(MacroRegimeDto regime) =>
        $"RegimeDeck Weekly — Market Health {regime.Health.Score} ({regime.Health.Label})";

    public static string BuildHtml(MacroRegimeDto regime, List<WatchlistRowDto> watchlist, string frontendUrl)
    {
        var html = new StringBuilder();

        html.Append(CultureInfo.InvariantCulture,
            $"""
            <h2 style="margin:0 0 4px;font-size:22px;color:#18181b;">Your weekly regime report</h2>
            <p style="margin:0 0 20px;color:#6b7280;font-size:13px;">Data as of {regime.AsOf:MMMM d, yyyy} — the same rules every week, no opinions.</p>
            <div style="margin:0 0 20px;padding:16px;border:1px solid #e4e4e7;border-radius:12px;">
              <div style="font-size:11px;font-weight:600;letter-spacing:1px;color:#6b7280;">MARKET HEALTH</div>
              <div style="font-size:32px;font-weight:700;color:#18181b;">{regime.Health.Score}<span style="font-size:16px;color:#6b7280;"> / 100 · {WebUtility.HtmlEncode(regime.Health.Label)}</span></div>
            </div>
            """);

        // The narrative
        foreach (var sentence in regime.Summary)
            html.Append(CultureInfo.InvariantCulture,
                $"""<p style="margin:0 0 8px;color:#52525b;font-size:14px;">{WebUtility.HtmlEncode(sentence)}</p>""");

        // The playbook
        html.Append(
            """<h3 style="margin:20px 0 8px;font-size:16px;color:#18181b;">Where conditions point for new money</h3>""");
        foreach (var asset in regime.Playbook.Assets)
        {
            var color = asset.Verdict switch
            {
                "Favored" => "#059669",
                "Headwinds" => "#dc2626",
                _ => "#6b7280",
            };
            html.Append(CultureInfo.InvariantCulture,
                $"""<p style="margin:0 0 4px;color:#52525b;font-size:14px;"><strong style="color:#18181b;">{WebUtility.HtmlEncode(asset.Name)}</strong> — <span style="color:{color};font-weight:600;">{asset.Verdict}</span> · fit {asset.Score}/100</p>""");
        }

        // The watchlist
        if (watchlist.Count > 0)
        {
            html.Append(
                """
                <h3 style="margin:20px 0 8px;font-size:16px;color:#18181b;">Your watchlist</h3>
                <table style="border-collapse:collapse;width:100%;font-size:14px;color:#52525b;">
                <tr><th align="left" style="padding:6px 12px 6px 0;border-bottom:1px solid #e4e4e7;">Asset</th><th align="left" style="padding:6px 12px 6px 0;border-bottom:1px solid #e4e4e7;">3M odds</th><th align="left" style="padding:6px 12px 6px 0;border-bottom:1px solid #e4e4e7;">Edge</th><th align="left" style="padding:6px 0;border-bottom:1px solid #e4e4e7;">Read</th></tr>
                """);
            foreach (var row in watchlist)
            {
                var odds = row.Odds3M is double o ? o.ToString("0", CultureInfo.InvariantCulture) + "%" : "—";
                var edge = row.Edge3M is double e
                    ? (e >= 0 ? "+" : "") + e.ToString("0.0", CultureInfo.InvariantCulture) + "pp"
                    : "—";
                var stance = row.TradeRead?.Stance ?? "pending data";
                html.Append(CultureInfo.InvariantCulture,
                    $"""<tr><td style="padding:6px 12px 6px 0;border-bottom:1px solid #f4f4f5;"><strong style="color:#18181b;">{WebUtility.HtmlEncode(row.Symbol)}</strong></td><td style="padding:6px 12px 6px 0;border-bottom:1px solid #f4f4f5;">{odds}</td><td style="padding:6px 12px 6px 0;border-bottom:1px solid #f4f4f5;">{edge}</td><td style="padding:6px 0;border-bottom:1px solid #f4f4f5;">{WebUtility.HtmlEncode(stance)}</td></tr>""");
            }
            html.Append("</table>");
        }

        html.Append(CultureInfo.InvariantCulture,
            $"""
            <a href="{frontendUrl}/dashboard" style="display:inline-block;margin:20px 0 0;background:#059669;color:#fff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:600;font-size:14px;">Open the dashboard</a>
            <p style="margin:20px 0 0;color:#6b7280;font-size:12px;">Descriptive statistics, not predictions or financial advice. Manage this email in your <a href="{frontendUrl}/account" style="color:#059669;">account settings</a>.</p>
            """);

        return html.ToString();
    }
}
