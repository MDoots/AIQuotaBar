namespace AIQuotaBar.App.Layout;

public static class QuotaLabelFormatter
{
    public static IReadOnlyList<string> GetCandidateLabels(string? displayName, string? providerId = null, string? windowId = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Array.Empty<string>();
        }

        var isCodex = !string.IsNullOrWhiteSpace(providerId) &&
                      providerId.Contains("codex", StringComparison.OrdinalIgnoreCase);

        if (isCodex)
        {
            return GetCodexCandidateLabels(displayName, windowId);
        }

        if (displayName.Contains(" · "))
        {
            return GetCompoundCandidateLabels(displayName);
        }

        return GetStandaloneCandidateLabels(displayName);
    }

    public static string SelectFittingLabel(IReadOnlyList<string> candidates, double availableWidth, Func<string, double> measureWidth)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return string.Empty;
        }

        if (measureWidth == null)
        {
            return candidates[0];
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var width = measureWidth(candidate);
            if (width <= availableWidth || i == candidates.Count - 1)
            {
                return candidate;
            }
        }

        return candidates[^1];
    }

    private static IReadOnlyList<string> GetCodexCandidateLabels(string displayName, string? windowId)
    {
        var raw = displayName.Trim();
        var list = new List<string>();

        if (raw.Contains("5-Hour", StringComparison.OrdinalIgnoreCase) || raw.Contains("5 Hour", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("5-Hour");
            list.Add("5h");
        }
        else if (raw.Contains("Weekly", StringComparison.OrdinalIgnoreCase) || raw.Contains("Week", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("Weekly");
            list.Add("Week");
            list.Add("W");
        }
        else if (raw.Contains("Primary", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("Primary");
            list.Add("Pri");
        }
        else if (raw.Contains("Secondary", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("Secondary");
            list.Add("Sec");
        }
        else
        {
            var clean = raw.EndsWith(" Window", StringComparison.OrdinalIgnoreCase)
                ? raw[..^7].Trim()
                : raw;
            list.Add(clean);
            list.Add(AbbreviateSuffix(clean, micro: true));
        }

        return list.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
    }

    private static IReadOnlyList<string> GetCompoundCandidateLabels(string displayName)
    {
        var parts = displayName.Split(new[] { " · " }, 2, StringSplitOptions.None);
        var prefix = parts[0].Trim();
        var suffix = parts[1].Trim();
        var list = new List<string>();

        if (prefix.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            if (suffix.Contains("5-Hour", StringComparison.OrdinalIgnoreCase) || suffix.Contains("5 Hour", StringComparison.OrdinalIgnoreCase))
            {
                list.Add("Gemini · 5-Hour");
                list.Add("Gemini · 5h");
                list.Add("Gemini 5h");
                list.Add("G · 5h");
                list.Add("G 5h");
            }
            else if (suffix.Contains("Weekly", StringComparison.OrdinalIgnoreCase) || suffix.Contains("Week", StringComparison.OrdinalIgnoreCase))
            {
                list.Add("Gemini · Weekly");
                list.Add("Gemini · Week");
                list.Add("Gemini · W");
                list.Add("Gemini W");
                list.Add("G · W");
                list.Add("G W");
            }
            else
            {
                list.Add($"{prefix} · {suffix}");
                list.Add($"{prefix} · {AbbreviateSuffix(suffix, compact: true)}");
                list.Add($"G · {AbbreviateSuffix(suffix, micro: true)}");
            }
        }
        else if (prefix.Contains("Claude", StringComparison.OrdinalIgnoreCase) && prefix.Contains("GPT", StringComparison.OrdinalIgnoreCase))
        {
            if (suffix.Contains("5-Hour", StringComparison.OrdinalIgnoreCase) || suffix.Contains("5 Hour", StringComparison.OrdinalIgnoreCase))
            {
                list.Add("Claude & GPT · 5-Hour");
                list.Add("Claude & GPT · 5h");
                list.Add("Claude/GPT · 5h");
                list.Add("Claude/GPT 5h");
                list.Add("Claude · 5h");
                list.Add("C/G · 5h");
                list.Add("CG · 5h");
                list.Add("CG 5h");
            }
            else if (suffix.Contains("Weekly", StringComparison.OrdinalIgnoreCase) || suffix.Contains("Week", StringComparison.OrdinalIgnoreCase))
            {
                list.Add("Claude & GPT · Weekly");
                list.Add("Claude & GPT · Week");
                list.Add("Claude/GPT · Week");
                list.Add("Claude/GPT Week");
                list.Add("Claude/GPT - W");
                list.Add("Claude · Week");
                list.Add("C/G · W");
                list.Add("CG · W");
                list.Add("CG W");
            }
            else
            {
                list.Add($"{prefix} · {suffix}");
                list.Add($"{prefix} · {AbbreviateSuffix(suffix, compact: true)}");
                list.Add($"CG · {AbbreviateSuffix(suffix, micro: true)}");
            }
        }
        else
        {
            list.Add($"{prefix} · {suffix}");
            list.Add($"{prefix} · {AbbreviateSuffix(suffix, compact: true)}");
            list.Add($"{AbbreviatePrefix(prefix, minimal: true)} · {AbbreviateSuffix(suffix, minimal: true)}");
            list.Add($"{AbbreviatePrefix(prefix, micro: true)} · {AbbreviateSuffix(suffix, micro: true)}");
        }

        return list.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
    }

    private static IReadOnlyList<string> GetStandaloneCandidateLabels(string name)
    {
        var raw = name.Trim();
        var list = new List<string> { raw };

        if (raw.EndsWith(" Window", StringComparison.OrdinalIgnoreCase))
        {
            var stripped = raw[..^7].Trim();
            list.Add(stripped);
            list.Add(AbbreviateSuffix(stripped, minimal: true));
            list.Add(AbbreviateSuffix(stripped, micro: true));
        }
        else
        {
            list.Add(AbbreviateSuffix(raw, minimal: true));
            list.Add(AbbreviateSuffix(raw, micro: true));
        }

        return list.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
    }

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
                WidgetLayoutMode.Compact => $"{prefix} · {AbbreviateSuffix(suffix, compact: true)}",
                WidgetLayoutMode.Minimal => $"{AbbreviatePrefix(prefix, minimal: true)} · {AbbreviateSuffix(suffix, minimal: true)}",
                WidgetLayoutMode.Micro => $"{AbbreviatePrefix(prefix, micro: true)} · {AbbreviateSuffix(suffix, micro: true)}",
                _ => displayName
            };
        }

        var isCodex = !string.IsNullOrWhiteSpace(providerId) &&
                      providerId.Contains("codex", StringComparison.OrdinalIgnoreCase);

        var clean = displayName.EndsWith(" Window", StringComparison.OrdinalIgnoreCase)
            ? displayName[..^7].Trim()
            : displayName;

        return mode switch
        {
            WidgetLayoutMode.Compact => clean,
            WidgetLayoutMode.Minimal => AbbreviateSuffix(clean, minimal: true),
            WidgetLayoutMode.Micro => isCodex ? $"C · {AbbreviateSuffix(clean, micro: true)}" : AbbreviateSuffix(clean, micro: true),
            _ => displayName
        };
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
