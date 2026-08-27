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
    public void Format_AntigravityCompoundNames_FormatsAcrossModes(string input, WidgetLayoutMode mode, string expected)
    {
        var result = QuotaLabelFormatter.Format(input, mode, providerId: "antigravity", windowId: "gemini_5h");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("5-Hour Window", WidgetLayoutMode.Full, "codex", "primary", "5-Hour Window")]
    [InlineData("5-Hour Window", WidgetLayoutMode.Compact, "codex", "primary", "5-Hour")]
    [InlineData("5-Hour Window", WidgetLayoutMode.Minimal, "codex", "primary", "5h")]
    [InlineData("5-Hour Window", WidgetLayoutMode.Micro, "codex", "primary", "C · 5h")]
    [InlineData("Weekly Window", WidgetLayoutMode.Full, "codex", "secondary", "Weekly Window")]
    [InlineData("Weekly Window", WidgetLayoutMode.Compact, "codex", "secondary", "Weekly")]
    [InlineData("Weekly Window", WidgetLayoutMode.Minimal, "codex", "secondary", "Week")]
    [InlineData("Weekly Window", WidgetLayoutMode.Micro, "codex", "secondary", "C · W")]
    [InlineData("Primary Window", WidgetLayoutMode.Full, "codex", "primary", "Primary Window")]
    [InlineData("Primary Window", WidgetLayoutMode.Compact, "codex", "primary", "Primary")]
    [InlineData("Primary Window", WidgetLayoutMode.Minimal, "codex", "primary", "Primary")]
    [InlineData("Primary Window", WidgetLayoutMode.Micro, "codex", "primary", "C · Pri")]
    [InlineData("Secondary Window", WidgetLayoutMode.Full, "codex", "secondary", "Secondary Window")]
    [InlineData("Secondary Window", WidgetLayoutMode.Compact, "codex", "secondary", "Secondary")]
    [InlineData("Secondary Window", WidgetLayoutMode.Minimal, "codex", "secondary", "Secondary")]
    [InlineData("Secondary Window", WidgetLayoutMode.Micro, "codex", "secondary", "C · Sec")]
    [InlineData("5-Hour Window", WidgetLayoutMode.Micro, "codex", "gpt4_primary", "C · 5h")]
    [InlineData("Weekly Window", WidgetLayoutMode.Micro, "codex", "gpt4_secondary", "C · W")]
    public void Format_CodexProductionNames_FormatsWithExplicitProviderId(string input, WidgetLayoutMode mode, string providerId, string windowId, string expected)
    {
        var result = QuotaLabelFormatter.Format(input, mode, providerId: providerId, windowId: windowId);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("5-Hour Window", WidgetLayoutMode.Micro, "other_provider", "primary", "5h")]
    [InlineData("Weekly Window", WidgetLayoutMode.Micro, "other_provider", "secondary", "W")]
    public void Format_NonCodexStandaloneNames_DoesNotAddCodexPrefix(string input, WidgetLayoutMode mode, string providerId, string windowId, string expected)
    {
        var result = QuotaLabelFormatter.Format(input, mode, providerId: providerId, windowId: windowId);
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
