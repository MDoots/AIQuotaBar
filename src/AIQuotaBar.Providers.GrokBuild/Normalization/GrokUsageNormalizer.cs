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
        var rawUsedPercent = CalculateUsedPercent(config);

        var isUnified = config.IsUnifiedBillingUser ?? false;
        var periodType = config.CurrentPeriod?.Type;

        string windowId;
        string windowDisplayName;
        TimeSpan? duration;

        if (isUnified)
        {
            if (periodType?.Contains("MONTHLY", StringComparison.OrdinalIgnoreCase) == true)
            {
                windowId = "shared-monthly";
                windowDisplayName = "Grok · Monthly";
                duration = TimeSpan.FromDays(30);
            }
            else
            {
                windowId = "shared-weekly";
                windowDisplayName = "Grok · Weekly";
                duration = TimeSpan.FromDays(7);
            }
        }
        else
        {
            if (periodType?.Contains("WEEKLY", StringComparison.OrdinalIgnoreCase) == true)
            {
                windowId = "build-weekly";
                windowDisplayName = "Build · Weekly";
                duration = TimeSpan.FromDays(7);
            }
            else
            {
                windowId = "build-monthly";
                windowDisplayName = "Build · Monthly";
                duration = TimeSpan.FromDays(30);
            }
        }

        DateTimeOffset? resetsAt = ParseDateTimeOffset(config.CurrentPeriod?.End)
            ?? ParseDateTimeOffset(config.BillingPeriodEnd);

        var status = rawUsedPercent >= 100
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

    private static double CalculateUsedPercent(GrokBillingConfig config)
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

        return 0.0;
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
