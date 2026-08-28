namespace AIQuotaBar.App.Tray;

using System.Windows.Forms;

public enum QuotaNotificationType
{
    LowQuota,
    QuotaExhausted
}

public sealed record QuotaNotification(
    string Title,
    string Message,
    QuotaNotificationType Type,
    ToolTipIcon Icon = ToolTipIcon.Warning);
