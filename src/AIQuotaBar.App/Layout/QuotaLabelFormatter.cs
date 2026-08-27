namespace AIQuotaBar.App.Layout;

public static class QuotaLabelFormatter
{
    public static string Format(string? displayName, WidgetLayoutMode mode, string? providerId = null, string? windowId = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return string.Empty;
        }

        if (mode == WidgetLayoutMode.Full)
        {
            return displayName;
        }

        if (displayName.Contains(" · "))
        {
            var parts = displayName.Split(new[] { " · " }, 2, StringSplitOptions.None);
            var prefix = parts[0].Trim();
            var suffix = parts[1].Trim();

            return mode switch
            {
                WidgetLayoutMode.Compact => FormatCompactCompound(prefix, suffix),
                WidgetLayoutMode.Minimal => FormatMinimalCompound(prefix, suffix),
                WidgetLayoutMode.Micro => FormatMicroCompound(prefix, suffix),
                _ => displayName
            };
        }

        return mode switch
        {
            WidgetLayoutMode.Compact => FormatCompactStandalone(displayName),
            WidgetLayoutMode.Minimal => FormatMinimalStandalone(displayName),
            WidgetLayoutMode.Micro => FormatMicroStandalone(displayName, providerId),
            _ => displayName
        };
    }

    private static string FormatCompactCompound(string prefix, string suffix)
    {
        var shortSuffix = AbbreviateSuffix(suffix, compact: true);
        return $"{prefix} · {shortSuffix}";
    }

    private static string FormatMinimalCompound(string prefix, string suffix)
    {
        var minPrefix = AbbreviatePrefix(prefix, minimal: true);
        var minSuffix = AbbreviateSuffix(suffix, minimal: true);
        return $"{minPrefix} · {minSuffix}";
    }

    private static string FormatMicroCompound(string prefix, string suffix)
    {
        var microPrefix = AbbreviatePrefix(prefix, micro: true);
        var microSuffix = AbbreviateSuffix(suffix, micro: true);
        return $"{microPrefix} · {microSuffix}";
    }

    private static string FormatCompactStandalone(string name)
    {
        if (name.EndsWith(" Window", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^7].Trim();
        }
        return name;
    }

    private static string FormatMinimalStandalone(string name)
    {
        var clean = FormatCompactStandalone(name);
        return AbbreviateSuffix(clean, minimal: true);
    }

    private static string FormatMicroStandalone(string name, string? providerId)
    {
        var clean = FormatCompactStandalone(name);
        var isCodex = !string.IsNullOrWhiteSpace(providerId) &&
                      providerId.Contains("codex", StringComparison.OrdinalIgnoreCase);
        var microSuffix = AbbreviateSuffix(clean, micro: true);
        return isCodex ? $"C · {microSuffix}" : microSuffix;
    }

    private static string AbbreviatePrefix(string prefix, bool minimal = false, bool micro = false)
    {
        if (micro)
        {
            if (prefix.Contains("Gemini", StringComparison.OrdinalIgnoreCase)) return "G";
            if (prefix.Contains("Claude", StringComparison.OrdinalIgnoreCase) && prefix.Contains("GPT", StringComparison.OrdinalIgnoreCase)) return "CG";
            if (prefix.Contains("Claude", StringComparison.OrdinalIgnoreCase)) return "C";
            if (prefix.Contains("GPT", StringComparison.OrdinalIgnoreCase)) return "GPT";
            if (prefix.Contains("Codex", StringComparison.OrdinalIgnoreCase)) return "C";
            return prefix.Length > 2 ? prefix[..2] : prefix;
        }

        if (minimal)
        {
            if (prefix.Contains("Claude", StringComparison.OrdinalIgnoreCase) && prefix.Contains("GPT", StringComparison.OrdinalIgnoreCase))
            {
                return "Claude";
            }
            return prefix;
        }

        return prefix;
    }

    private static string AbbreviateSuffix(string suffix, bool compact = false, bool minimal = false, bool micro = false)
    {
        var s = suffix;
        if (s.EndsWith(" Window", StringComparison.OrdinalIgnoreCase))
        {
            s = s[..^7].Trim();
        }

        if (micro)
        {
            if (s.Contains("5-Hour", StringComparison.OrdinalIgnoreCase) || s.Contains("5 Hour", StringComparison.OrdinalIgnoreCase) || s.Equals("5h", StringComparison.OrdinalIgnoreCase))
                return "5h";
            if (s.Contains("Weekly", StringComparison.OrdinalIgnoreCase) || s.Contains("Week", StringComparison.OrdinalIgnoreCase) || s.Equals("7d", StringComparison.OrdinalIgnoreCase))
                return "W";
            if (s.Contains("Primary", StringComparison.OrdinalIgnoreCase))
                return "Pri";
            if (s.Contains("Secondary", StringComparison.OrdinalIgnoreCase))
                return "Sec";
            return s.Length > 3 ? s[..3] : s;
        }

        if (compact)
        {
            if (s.Contains("5-Hour", StringComparison.OrdinalIgnoreCase) || s.Contains("5 Hour", StringComparison.OrdinalIgnoreCase))
                return "5h";
            if (s.Contains("Weekly", StringComparison.OrdinalIgnoreCase) || s.Contains("Week", StringComparison.OrdinalIgnoreCase))
                return "Week";
            return s;
        }

        if (minimal)
        {
            if (s.Contains("5-Hour", StringComparison.OrdinalIgnoreCase) || s.Contains("5 Hour", StringComparison.OrdinalIgnoreCase))
                return "5h";
            if (s.Contains("Weekly", StringComparison.OrdinalIgnoreCase) || s.Contains("Week", StringComparison.OrdinalIgnoreCase))
                return "Week";
            if (s.Contains("Primary", StringComparison.OrdinalIgnoreCase))
                return "Primary";
            if (s.Contains("Secondary", StringComparison.OrdinalIgnoreCase))
                return "Secondary";
            return s;
        }

        return s;
    }
}

public static class ProviderLabelFormatter
{
    public static string Format(string? providerName, WidgetLayoutMode mode)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return string.Empty;
        }

        if (mode is WidgetLayoutMode.Full or WidgetLayoutMode.Compact)
        {
            return providerName;
        }

        if (providerName.Contains("Codex", StringComparison.OrdinalIgnoreCase))
        {
            return "Codex";
        }

        if (providerName.Contains("Antigravity", StringComparison.OrdinalIgnoreCase))
        {
            return "Antigravity";
        }

        return providerName;
    }
}
