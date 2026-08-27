namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Layout;
using Xunit;

public class QuotaLabelFormatterTests
{
    [Theory]
    [InlineData("Gemini · 5-Hour", WidgetLayoutMode.Full, "Gemini · 5-Hour")]
    [InlineData("Gemini · 5-Hour", WidgetLayoutMode.Compact, "Gemini · 5h")]
    [InlineData("Gemini · 5-Hour", WidgetLayoutMode.Minimal, "Gemini · 5h")]
    [InlineData("Gemini · 5-Hour", WidgetLayoutMode.Micro, "G · 5h")]
    [InlineData("Gemini · Weekly", WidgetLayoutMode.Full, "Gemini · Weekly")]
    [InlineData("Gemini · Weekly", WidgetLayoutMode.Compact, "Gemini · Week")]
    [InlineData("Gemini · Weekly", WidgetLayoutMode.Minimal, "Gemini · Week")]
    [InlineData("Gemini · Weekly", WidgetLayoutMode.Micro, "G · W")]
    [InlineData("Claude & GPT · 5-Hour", WidgetLayoutMode.Full, "Claude & GPT · 5-Hour")]
    [InlineData("Claude & GPT · 5-Hour", WidgetLayoutMode.Compact, "Claude & GPT · 5h")]
    [InlineData("Claude & GPT · 5-Hour", WidgetLayoutMode.Minimal, "Claude · 5h")]
    [InlineData("Claude & GPT · 5-Hour", WidgetLayoutMode.Micro, "CG · 5h")]
    [InlineData("Claude & GPT · Weekly", WidgetLayoutMode.Full, "Claude & GPT · Weekly")]
    [InlineData("Claude & GPT · Weekly", WidgetLayoutMode.Compact, "Claude & GPT · Week")]
    [InlineData("Claude & GPT · Weekly", WidgetLayoutMode.Minimal, "Claude · Week")]
    [InlineData("Claude & GPT · Weekly", WidgetLayoutMode.Micro, "CG · W")]
    public void Format_CompoundNames_FormatsAcrossModes(string input, WidgetLayoutMode mode, string expected)
    {
        var result = QuotaLabelFormatter.Format(input, mode);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("5-Hour", WidgetLayoutMode.Full, "codex_5h", "5-Hour")]
    [InlineData("5-Hour", WidgetLayoutMode.Compact, "codex_5h", "5-Hour")]
    [InlineData("5-Hour", WidgetLayoutMode.Minimal, "codex_5h", "5h")]
    [InlineData("5-Hour", WidgetLayoutMode.Micro, "codex_5h", "C · 5h")]
    [InlineData("Weekly", WidgetLayoutMode.Full, "codex_weekly", "Weekly")]
    [InlineData("Weekly", WidgetLayoutMode.Compact, "codex_weekly", "Weekly")]
    [InlineData("Weekly", WidgetLayoutMode.Minimal, "codex_weekly", "Week")]
    [InlineData("Weekly", WidgetLayoutMode.Micro, "codex_weekly", "C · W")]
    [InlineData("Primary Window", WidgetLayoutMode.Full, "codex_primary", "Primary Window")]
    [InlineData("Primary Window", WidgetLayoutMode.Compact, "codex_primary", "Primary")]
    [InlineData("Primary Window", WidgetLayoutMode.Minimal, "codex_primary", "Primary")]
    [InlineData("Primary Window", WidgetLayoutMode.Micro, "codex_primary", "C · Pri")]
    [InlineData("Secondary Window", WidgetLayoutMode.Full, "codex_secondary", "Secondary Window")]
    [InlineData("Secondary Window", WidgetLayoutMode.Compact, "codex_secondary", "Secondary")]
    [InlineData("Secondary Window", WidgetLayoutMode.Minimal, "codex_secondary", "Secondary")]
    [InlineData("Secondary Window", WidgetLayoutMode.Micro, "codex_secondary", "C · Sec")]
    public void Format_CodexStandaloneNames_FormatsAcrossModes(string input, WidgetLayoutMode mode, string id, string expected)
    {
        var result = QuotaLabelFormatter.Format(input, mode, id);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_ReturnsEmptyString_ForNullOrEmpty()
    {
        Assert.Equal(string.Empty, QuotaLabelFormatter.Format(null, WidgetLayoutMode.Full));
        Assert.Equal(string.Empty, QuotaLabelFormatter.Format("", WidgetLayoutMode.Compact));
        Assert.Equal(string.Empty, QuotaLabelFormatter.Format("   ", WidgetLayoutMode.Micro));
    }

    [Theory]
    [InlineData("OpenAI Codex", WidgetLayoutMode.Full, "OpenAI Codex")]
    [InlineData("OpenAI Codex", WidgetLayoutMode.Compact, "OpenAI Codex")]
    [InlineData("OpenAI Codex", WidgetLayoutMode.Minimal, "Codex")]
    [InlineData("OpenAI Codex", WidgetLayoutMode.Micro, "Codex")]
    [InlineData("Google Antigravity", WidgetLayoutMode.Full, "Google Antigravity")]
    [InlineData("Google Antigravity", WidgetLayoutMode.Compact, "Google Antigravity")]
    [InlineData("Google Antigravity", WidgetLayoutMode.Minimal, "Antigravity")]
    [InlineData("Google Antigravity", WidgetLayoutMode.Micro, "Antigravity")]
    public void ProviderLabelFormatter_FormatsAcrossModes(string input, WidgetLayoutMode mode, string expected)
    {
        var result = ProviderLabelFormatter.Format(input, mode);
        Assert.Equal(expected, result);
    }
}
