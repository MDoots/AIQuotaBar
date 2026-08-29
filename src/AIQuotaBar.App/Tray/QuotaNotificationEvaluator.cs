namespace AIQuotaBar.App.Tray;

using System.Windows.Forms;
using AIQuotaBar.Core.Models;

public sealed class QuotaNotificationEvaluator
{
    private sealed class RowTrackingState
    {
        public bool HasBaseline { get; set; }
        public bool NotifiedLow { get; set; }
        public bool NotifiedExhausted { get; set; }
        public double LastObservedRemainingPercent { get; set; }
    }

    private readonly Dictionary<string, RowTrackingState> _trackedStates = new(StringComparer.OrdinalIgnoreCase);

    public void Reset()
    {
        _trackedStates.Clear();
    }

    public QuotaNotification? Evaluate(
        IEnumerable<QuotaObservation>? currentVisibleObservations,
        bool notificationsEnabled = true)
    {
        if (currentVisibleObservations == null)
        {
            _trackedStates.Clear();
            return null;
        }

        var validObservations = currentVisibleObservations
            .Where(obs => IsValidPercentage(obs.RemainingPercent))
            .ToList();

        var currentObservationKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var triggeredExhaustedRows = new List<QuotaObservation>();
        var triggeredLowRows = new List<QuotaObservation>();

        foreach (var obs in validObservations)
        {
            var key = GetRowKey(obs.ProviderId, obs.WindowId);
            currentObservationKeys.Add(key);

            if (obs.IsStale)
            {
                // Transient stale poll: preserve tracking baseline without evaluating crossings or altering states
                continue;
            }

            var remaining = obs.RemainingPercent;
            var isExhausted = remaining <= 0.0 || obs.Status == QuotaWindowStatus.Exhausted;
            var isLow = remaining < 10.0;

            if (!_trackedStates.TryGetValue(key, out var state))
            {
                // First observation for this row in the session -> Record baseline only, do not notify
                state = new RowTrackingState
                {
                    HasBaseline = true,
                    LastObservedRemainingPercent = remaining,
                    NotifiedLow = isLow,
                    NotifiedExhausted = isExhausted
                };
                _trackedStates[key] = state;
                continue;
            }

            // Row was previously observed -> Check re-arming first
            if (remaining >= 10.0 && obs.Status != QuotaWindowStatus.Exhausted)
            {
                state.NotifiedLow = false;
                state.NotifiedExhausted = false;
            }
            else if (remaining > 0.0 && obs.Status != QuotaWindowStatus.Exhausted)
            {
                state.NotifiedExhausted = false;
            }

            // Check threshold crossings
            if (isExhausted)
            {
                if (!state.NotifiedExhausted)
                {
                    state.NotifiedExhausted = true;
                    state.NotifiedLow = true; // Suppress separate low notification
                    if (notificationsEnabled)
                    {
                        triggeredExhaustedRows.Add(obs);
                    }
                }
            }
            else if (isLow)
            {
                if (!state.NotifiedLow)
                {
                    state.NotifiedLow = true;
                    if (notificationsEnabled)
                    {
                        triggeredLowRows.Add(obs);
                    }
                }
            }

            state.LastObservedRemainingPercent = remaining;
        }

        // Remove tracking for rows that are no longer in visible observations (e.g. hidden).
        // This ensures if they are made visible again later, they enter silently as a new baseline.
        var keysToRemove = _trackedStates.Keys
            .Where(k => !currentObservationKeys.Contains(k))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _trackedStates.Remove(key);
        }

        if (!notificationsEnabled)
        {
            return null;
        }

        if (triggeredExhaustedRows.Count == 0 && triggeredLowRows.Count == 0)
        {
            return null;
        }

        // Aggregate triggered rows into a single notification event
        if (triggeredExhaustedRows.Count > 0)
        {
            var primary = triggeredExhaustedRows
                .OrderBy(r => r.RemainingPercent)
                .ThenBy(r => r.ProviderDisplayName)
                .ThenBy(r => r.WindowDisplayName)
                .First();

            var primaryLabel = TrayHealthCalculator.FormatRowLabel(primary.ProviderDisplayName, primary.WindowDisplayName);
            var totalTriggered = triggeredExhaustedRows.Count + triggeredLowRows.Count;

            string message;
            if (totalTriggered == 1)
            {
                message = $"{primaryLabel} has no quota remaining.";
            }
            else
            {
                var others = totalTriggered - 1;
                var otherText = others == 1
                    ? "1 other quota window is also low or exhausted."
                    : $"{others} other quota windows are also low or exhausted.";
                message = $"{primaryLabel} has no quota remaining.\n{otherText}";
            }

            return new QuotaNotification(
                Title: "AIQuotaBar — Quota exhausted",
                Message: message,
                Type: QuotaNotificationType.QuotaExhausted,
                Icon: ToolTipIcon.Warning);
        }
        else
        {
            var primary = triggeredLowRows
                .OrderBy(r => r.RemainingPercent)
                .ThenBy(r => r.ProviderDisplayName)
                .ThenBy(r => r.WindowDisplayName)
                .First();

            var primaryLabel = TrayHealthCalculator.FormatRowLabel(primary.ProviderDisplayName, primary.WindowDisplayName);
            var primaryPercent = (int)Math.Round(primary.RemainingPercent, MidpointRounding.AwayFromZero);
            var totalTriggered = triggeredLowRows.Count;

            string message;
            if (totalTriggered == 1)
            {
                message = $"{primaryLabel} has {primaryPercent}% remaining.";
            }
            else
            {
                var others = totalTriggered - 1;
                var otherWord = others == 1 ? "window is" : "windows are";
                message = $"{primaryLabel} has {primaryPercent}% remaining.\n{others} other quota {otherWord} also low.";
            }

            return new QuotaNotification(
                Title: "AIQuotaBar — Low quota",
                Message: message,
                Type: QuotaNotificationType.LowQuota,
                Icon: ToolTipIcon.Warning);
        }
    }

    private static string GetRowKey(string? providerId, string? windowId)
    {
        return $"{(providerId ?? string.Empty).Trim().ToLowerInvariant()}:{(windowId ?? string.Empty).Trim().ToLowerInvariant()}";
    }

    private static bool IsValidPercentage(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
