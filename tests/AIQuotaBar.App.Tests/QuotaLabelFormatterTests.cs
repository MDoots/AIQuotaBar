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

    [Fact]
    public void GetCandidateLabels_Codex5Hour_GeneratesExpectedOrder()
    {
        var candidates = QuotaLabelFormatter.GetCandidateLabels("5-Hour Window", providerId: "codex", windowId: "primary");
        Assert.Equal(new[] { "5-Hour", "5h" }, candidates);
    }

    [Fact]
    public void GetCandidateLabels_CodexWeekly_GeneratesExpectedOrder()
    {
        var candidates = QuotaLabelFormatter.GetCandidateLabels("Weekly Window", providerId: "codex", windowId: "secondary");
        Assert.Equal(new[] { "Weekly", "Week", "W" }, candidates);
    }

    [Fact]
    public void GetCandidateLabels_CodexPrimaryFallback_GeneratesExpectedOrder()
    {
        var candidates = QuotaLabelFormatter.GetCandidateLabels("Primary Window", providerId: "codex", windowId: "primary");
        Assert.Equal(new[] { "Primary", "Pri" }, candidates);
    }

    [Fact]
    public void GetCandidateLabels_CodexSecondaryFallback_GeneratesExpectedOrder()
    {
        var candidates = QuotaLabelFormatter.GetCandidateLabels("Secondary Window", providerId: "codex", windowId: "secondary");
        Assert.Equal(new[] { "Secondary", "Sec" }, candidates);
    }

    [Fact]
    public void GetCandidateLabels_AntigravityGemini5Hour_GeneratesExpectedOrder()
    {
        var candidates = QuotaLabelFormatter.GetCandidateLabels("Gemini · 5-Hour", providerId: "antigravity", windowId: "gemini_5h");
        Assert.Equal(new[] { "Gemini · 5-Hour", "Gemini · 5h", "Gemini 5h", "G · 5h", "G 5h" }, candidates);
    }

    [Fact]
    public void GetCandidateLabels_AntigravityGeminiWeekly_GeneratesExpectedOrder()
    {
        var candidates = QuotaLabelFormatter.GetCandidateLabels("Gemini · Weekly", providerId: "antigravity", windowId: "gemini_weekly");
        Assert.Equal(new[] { "Gemini · Weekly", "Gemini · Week", "Gemini · W", "Gemini W", "G · W", "G W" }, candidates);
    }

    [Fact]
    public void GetCandidateLabels_AntigravityClaudeGpt5Hour_GeneratesExpectedOrder()
    {
        var candidates = QuotaLabelFormatter.GetCandidateLabels("Claude & GPT · 5-Hour", providerId: "antigravity", windowId: "claudegpt_5h");
        Assert.Equal(new[] { "Claude & GPT · 5-Hour", "Claude & GPT · 5h", "Claude/GPT · 5h", "Claude/GPT 5h", "Claude · 5h", "C/G · 5h", "CG · 5h", "CG 5h" }, candidates);
    }

    [Fact]
    public void GetCandidateLabels_AntigravityClaudeGptWeekly_GeneratesExpectedOrder()
    {
        var candidates = QuotaLabelFormatter.GetCandidateLabels("Claude & GPT · Weekly", providerId: "antigravity", windowId: "claudegpt_weekly");
        Assert.Equal(new[] { "Claude & GPT · Weekly", "Claude & GPT · Week", "Claude/GPT · Week", "Claude/GPT Week", "Claude/GPT - W", "Claude · Week", "C/G · W", "CG · W", "CG W" }, candidates);
    }

    [Fact]
    public void GetCandidateLabels_NeverReturnsEmptyCandidate()
    {
        var testInputs = new[]
        {
            ("5-Hour Window", "codex"),
            ("Weekly Window", "codex"),
            ("Primary Window", "codex"),
            ("Gemini · 5-Hour", "antigravity"),
            ("Claude & GPT · Weekly", "antigravity"),
            ("Custom · 5-Hour", "other"),
            ("Standalone", "other")
        };

        foreach (var (input, provider) in testInputs)
        {
            var candidates = QuotaLabelFormatter.GetCandidateLabels(input, provider);
            Assert.NotEmpty(candidates);
            Assert.All(candidates, c => Assert.False(string.IsNullOrWhiteSpace(c)));
            Assert.All(candidates, c => Assert.DoesNotContain("Window", c, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void SelectFittingLabel_SelectsRichestCandidateThatFits()
    {
        var candidates = new[] { "5-Hour", "5h" };

        double MockMeasure(string text) => text switch
        {
            "5-Hour" => 45.0,
            "5h" => 20.0,
            _ => 100.0
        };

        var chosenAmple = QuotaLabelFormatter.SelectFittingLabel(candidates, availableWidth: 100.0, MockMeasure);
        Assert.Equal("5-Hour", chosenAmple);

        var chosenTight = QuotaLabelFormatter.SelectFittingLabel(candidates, availableWidth: 25.0, MockMeasure);
        Assert.Equal("5h", chosenTight);

        var chosenTiny = QuotaLabelFormatter.SelectFittingLabel(candidates, availableWidth: 10.0, MockMeasure);
        Assert.Equal("5h", chosenTiny);
    }

    [Fact]
    public void SelectFittingLabel_PrioritizesClaudeGptIdentityOverClaudeOnly()
    {
        var candidates = QuotaLabelFormatter.GetCandidateLabels("Claude & GPT · 5-Hour", providerId: "antigravity", windowId: "claudegpt_5h");

        // Simulate widths:
        // "Claude & GPT · 5-Hour" = 120
        // "Claude & GPT · 5h" = 95
        // "Claude/GPT · 5h" = 80
        // "Claude/GPT 5h" = 72
        // "Claude · 5h" = 65
        // "C/G · 5h" = 50
        // "CG · 5h" = 43
        // "CG 5h" = 38
        double MockMeasure(string text) => text switch
        {
            "Claude & GPT · 5-Hour" => 120.0,
            "Claude & GPT · 5h" => 95.0,
            "Claude/GPT · 5h" => 80.0,
            "Claude/GPT 5h" => 72.0,
            "Claude · 5h" => 65.0,
            "C/G · 5h" => 50.0,
            "CG · 5h" => 43.0,
            "CG 5h" => 38.0,
            _ => 200.0
        };

        // If available width is 75, "Claude & GPT · 5h" (95) and "Claude/GPT · 5h" (80) don't fit.
        // But "Claude/GPT 5h" (72) DOES fit.
        // It must be chosen over "Claude · 5h" (65).
        var chosen = QuotaLabelFormatter.SelectFittingLabel(candidates, availableWidth: 75.0, MockMeasure);
        Assert.Equal("Claude/GPT 5h", chosen);
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
