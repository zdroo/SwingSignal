using System.Globalization;
using System.Net;
using System.Text;

namespace RegimeDeck.Application.Alerts;

/// One digest per user per run listing the custom alerts that just triggered.
public static class CustomAlertEmailBuilder
{
    public static string Subject(int count) =>
        count == 1
            ? "RegimeDeck Alert — 1 of your alerts triggered"
            : $"RegimeDeck Alert — {count} of your alerts triggered";

    public static string BuildHtml(IReadOnlyList<(string Name, string Summary)> fired, string frontendUrl)
    {
        var html = new StringBuilder();
        html.Append(
            """
            <h2 style="margin:0 0 4px;font-size:22px;color:#18181b;">Your alert conditions were met</h2>
            <p style="margin:0 0 20px;color:#6b7280;font-size:13px;">Based on the latest daily readings. Descriptive, not a trade instruction.</p>
            """);

        foreach (var (name, summary) in fired)
        {
            html.Append(CultureInfo.InvariantCulture,
                $"""
                <div style="margin:0 0 12px;padding:14px 16px;border:1px solid #e4e4e7;border-radius:12px;">
                  <p style="margin:0;font-size:15px;font-weight:600;color:#18181b;">{WebUtility.HtmlEncode(name)}</p>
                  <p style="margin:4px 0 0;font-size:13px;color:#52525b;">{WebUtility.HtmlEncode(summary)}</p>
                </div>
                """);
        }

        html.Append(CultureInfo.InvariantCulture,
            $"""
            <a href="{frontendUrl}/alerts" style="display:inline-block;margin:8px 0 0;background:#059669;color:#fff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:600;font-size:14px;">Manage your alerts</a>
            <p style="margin:20px 0 0;color:#6b7280;font-size:12px;">Alerts describe conditions in historical/daily data — not predictions or financial advice.</p>
            """);

        return html.ToString();
    }
}
