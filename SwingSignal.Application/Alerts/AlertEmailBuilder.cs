using System.Globalization;
using System.Net;
using System.Text;

namespace SwingSignal.Application.Alerts;

/// One digest email per user per evaluation run — never one email per event.
public static class AlertEmailBuilder
{
    public static string Subject(IReadOnlyList<AlertRules.Change> changes) =>
        changes.Count == 1
            ? $"SwingSignal Alert — {changes[0].Title}"
            : $"SwingSignal Alert — {changes.Count} changes in what you track";

    public static string BuildHtml(IReadOnlyList<AlertRules.Change> changes, string frontendUrl)
    {
        var html = new StringBuilder();
        html.Append(
            """
            <h2 style="margin:0 0 4px;font-size:22px;color:#18181b;">Something you track changed</h2>
            <p style="margin:0 0 20px;color:#6b7280;font-size:13px;">The same fixed rules that power the site noticed a shift. Descriptive, not a trade instruction.</p>
            """);

        foreach (var change in changes)
        {
            html.Append(CultureInfo.InvariantCulture,
                $"""
                <div style="margin:0 0 12px;padding:14px 16px;border:1px solid #e4e4e7;border-radius:12px;">
                  <p style="margin:0;font-size:15px;font-weight:600;color:#18181b;">{WebUtility.HtmlEncode(change.Title)}</p>
                  <p style="margin:4px 0 0;font-size:13px;color:#52525b;">{WebUtility.HtmlEncode(change.Detail)}</p>
                </div>
                """);
        }

        html.Append(CultureInfo.InvariantCulture,
            $"""
            <a href="{frontendUrl}/watchlist" style="display:inline-block;margin:8px 0 0;background:#059669;color:#fff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:600;font-size:14px;">Open your watchlist</a>
            <p style="margin:20px 0 0;color:#6b7280;font-size:12px;">Alerts describe changes in historical statistics — not predictions or financial advice. Manage alerts in your <a href="{frontendUrl}/account" style="color:#059669;">account settings</a>.</p>
            """);

        return html.ToString();
    }
}
