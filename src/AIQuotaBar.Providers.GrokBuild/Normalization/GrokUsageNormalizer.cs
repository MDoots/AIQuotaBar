namespace AIQuotaBar.Providers.GrokBuild.Normalization;

using System.Globalization;
using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.GrokBuild.Protocol;

public static class GrokUsageNormalizer
{
    public const string ProviderIdentifier = "grok-build";
    public const string ProviderName = "Grok Build";

    public static ProviderSnapshot Normalize(GrokBillingResult? billingResult)
    {
        var plan = FormatPlanType(billingResult?.EffectiveTier);

        if (billingResult?.Config == null)
        {
            return new ProviderSnapshot(
                providerId: ProviderIdentifier,
                providerDisplayName: ProviderName,
                status: ProviderStatus.Unavailable,
                statusMessage: "No billing data returned by Grok",
                accountPlan: plan);
        }

        var config = billingResult.Config;
        var usedPercent = CalculateUsedPercent(config);
        if (!usedPercent.HasValue)
        {
            // Missing creditUsagePercent and missing legacy used/monthlyLimit: do NOT fabricate a 0% used / 100% remaining quota!
            return new ProviderSnapshot(
                providerId: ProviderIdentifier,
                providerDisplayName: ProviderName,
                status: ProviderStatus.Unavailable,
                statusMessage: "No finite quota returned by Grok",
                accountPlan: plan);
        }

        var periodDetails = ResolvePeriodDetails(config);
        if (periodDetails == null)
        {
            // Unknown or missing period type: do not guess weekly/monthly
            return new ProviderSnapshot(
                providerId: ProviderIdentifier,
                providerDisplayName: ProviderName,
                status: ProviderStatus.Unavailable,
                statusMessage: "Unrecognized billing period from Grok",
                accountPlan: plan);
        }

        var (windowId, windowDisplayName, duration) = periodDetails.Value;

        DateTimeOffset? resetsAt = ParseDateTimeOffset(config.CurrentPeriod?.End)
            ?? ParseDateTimeOffset(config.BillingPeriodEnd);

        var rawUsedPercent = usedPercent.Value;
        var status = rawUsedPercent >= 100.0
            ? QuotaWindowStatus.Exhausted
            : QuotaWindowStatus.Active;

        var window = new QuotaWindow(
            id: windowId,
            displayName: windowDisplayName,
            rawUsedPercent: rawUsedPercent,
            duration: duration,
            resetsAt: resetsAt,
            status: status);

        return new ProviderSnapshot(
            providerId: ProviderIdentifier,
            providerDisplayName: ProviderName,
            status: ProviderStatus.Available,
            statusMessage: null,
            accountPlan: plan,
            windows: new[] { window });
    }

    public static double? CalculateUsedPercent(GrokBillingConfig config)
    {
        if (config.CreditUsagePercent.HasValue)
        {
            return Math.Clamp(config.CreditUsagePercent.Value, 0.0, 100.0);
        }

        if (config.Used?.Val.HasValue == true && config.MonthlyLimit?.Val.HasValue == true && config.MonthlyLimit.Val.Value > 0)
        {
            var pct = (config.Used.Val.Value / config.MonthlyLimit.Val.Value) * 100.0;
            return Math.Clamp(pct, 0.0, 100.0);
        }

        return null;
    }

    public static (string WindowId, string DisplayName, TimeSpan? Duration)? ResolvePeriodDetails(GrokBillingConfig config)
    {
        var isUnified = config.IsUnifiedBillingUser ?? false;
        var rawType = config.CurrentPeriod?.Type;

        if (!string.IsNullOrWhiteSpace(rawType))
        {
            if (rawType.Contains("WEEKLY", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawType, "weekly", StringComparison.OrdinalIgnoreCase))
            {
                var id = isUnified ? "shared-weekly" : "build-weekly";
                var name = isUnified ? "Grok · Weekly" : "Build · Weekly";
                return (id, name, TimeSpan.FromDays(7));
            }

            if (rawType.Contains("MONTHLY", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawType, "monthly", StringComparison.OrdinalIgnoreCase))
            {
                var id = isUnified ? "shared-monthly" : "build-monthly";
                var name = isUnified ? "Grok · Monthly" : "Build · Monthly";
                return (id, name, TimeSpan.FromDays(30));
            }

            // Unknown rawType -> Do not guess!
            return null;
        }

        // Legacy Build billing fallback only if monthly limit and billing period are clearly present
        if (config.MonthlyLimit?.Val.HasValue == true && config.MonthlyLimit.Val.Value > 0 && !string.IsNullOrWhiteSpace(config.BillingPeriodStart))
        {
            return ("build-monthly", "Build · Monthly", TimeSpan.FromDays(30));
        }

        return null;
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            return dto;
        }

        if (DateTimeOffset.TryParse(text, out var fallbackDto))
        {
            return fallbackDto;
        }

        return null;
    }

    private static string? FormatPlanType(string? planType)
    {
        if (string.IsNullOrWhiteSpace(planType))
        {
            return null;
        }

        return planType.Trim();
    }
}
