namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Tray;
using AIQuotaBar.Core.Models;
using Xunit;

public class QuotaNotificationEvaluatorTests
{
    [Fact]
    public void Evaluate_FirstObservation_SetsBaselineAndDoesNotNotify_EvenIfLow()
    {
        var evaluator = new QuotaNotificationEvaluator();

        var initialObservation = new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 5.0)
        };

        var notification = evaluator.Evaluate(initialObservation);

        Assert.Null(notification);
    }

    [Fact]
    public void Evaluate_FirstObservation_SetsBaselineAndDoesNotNotify_EvenIfExhausted()
    {
        var evaluator = new QuotaNotificationEvaluator();

        var initialObservation = new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 0.0, QuotaWindowStatus.Exhausted)
        };

        var notification = evaluator.Evaluate(initialObservation);

        Assert.Null(notification);
    }

    [Fact]
    public void Evaluate_CrossingBelow10_ProducesLowQuotaNotification()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // Baseline: 12.4%
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 12.4)
        });

        // Next observation: 9.7% (crosses < 10%)
        var notification = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 9.7)
        });

        Assert.NotNull(notification);
        Assert.Equal("AIQuotaBar — Low quota", notification.Title);
        Assert.Equal(QuotaNotificationType.LowQuota, notification.Type);
        Assert.Contains("Codex Weekly", notification.Message);
        Assert.Contains("10%", notification.Message); // 9.7 rounds to 10%
    }

    [Fact]
    public void Evaluate_RepeatedLowObservations_DoNotProduceDuplicateAlerts()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // Baseline: 12%
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 12.0)
        });

        // 9%: fires low alert
        var n1 = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 9.0)
        });
        Assert.NotNull(n1);

        // 8%: must NOT re-notify
        var n2 = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 8.0)
        });
        Assert.Null(n2);

        // 7%: must NOT re-notify
        var n3 = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 7.0)
        });
        Assert.Null(n3);
    }

    [Fact]
    public void Evaluate_LowToExhausted_ProducesExhaustedNotification()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // Baseline: 15%
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 15.0)
        });

        // Drop to 8% -> low alert
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 8.0)
        });

        // Drop to 0% -> exhausted alert
        var notification = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 0.0, QuotaWindowStatus.Exhausted)
        });

        Assert.NotNull(notification);
        Assert.Equal("AIQuotaBar — Quota exhausted", notification.Title);
        Assert.Equal(QuotaNotificationType.QuotaExhausted, notification.Type);
        Assert.Contains("Codex Weekly has no quota remaining", notification.Message);
    }

    [Fact]
    public void Evaluate_DirectDropFromAbove10ToZero_ProducesOnlyExhaustedNotification()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // Baseline: 15%
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 15.0)
        });

        // Direct jump from 15% -> 0%
        var notification = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 0.0, QuotaWindowStatus.Exhausted)
        });

        Assert.NotNull(notification);
        Assert.Equal("AIQuotaBar — Quota exhausted", notification.Title);
        Assert.Equal(QuotaNotificationType.QuotaExhausted, notification.Type);
        Assert.Contains("Codex Weekly has no quota remaining", notification.Message);
    }

    [Fact]
    public void Evaluate_RecoveryAbove10_ReArmsLowAndExhaustedNotifications()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // Baseline: 15%
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 15.0)
        });

        // Drop to 8% -> Low alert
        var n1 = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 8.0)
        });
        Assert.NotNull(n1);

        // Reset occurs: Quota recovers to 95%
        var nReset = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 95.0)
        });
        Assert.Null(nReset);

        // Later drops to 9% -> Should fire low alert again!
        var n2 = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 9.0)
        });
        Assert.NotNull(n2);
        Assert.Equal("AIQuotaBar — Low quota", n2.Title);
    }

    [Fact]
    public void Evaluate_ExhaustedRecoveryTo100_ReArmsExhaustedNotification()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // Baseline: 15%
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 15.0)
        });

        // 0% -> Exhausted alert
        var n1 = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 0.0, QuotaWindowStatus.Exhausted)
        });
        Assert.NotNull(n1);

        // Reset to 100%
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 100.0)
        });

        // Drops again to 0% -> Exhausted alert should fire again!
        var n2 = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 0.0, QuotaWindowStatus.Exhausted)
        });
        Assert.NotNull(n2);
        Assert.Equal("AIQuotaBar — Quota exhausted", n2.Title);
    }

    [Fact]
    public void Evaluate_HiddenRow_DoesNotProduceNotification_AndReintroducingItBaselinesSilently()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // Baseline: Row 1 (Codex) and Row 2 (AGY)
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "5h", "5-Hour", 50.0),
            new QuotaObservation("antigravity", "Google Antigravity", "gemini_weekly", "Gemini · Weekly", 50.0)
        });

        // User hides AGY in Settings. Current visible set contains only Codex.
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "5h", "5-Hour", 50.0)
        });

        // Later AGY quota drops to 5% while hidden.
        // User unhides AGY, so it reappears in visible observations at 5%.
        var notification = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "5h", "5-Hour", 50.0),
            new QuotaObservation("antigravity", "Google Antigravity", "gemini_weekly", "Gemini · Weekly", 5.0)
        });

        // MUST NOT produce notification on reintroduction!
        Assert.Null(notification);

        // Subsequent drop to 4% -> already in <10 state, no alert
        var nSubsequent = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "5h", "5-Hour", 50.0),
            new QuotaObservation("antigravity", "Google Antigravity", "gemini_weekly", "Gemini · Weekly", 4.0)
        });
        Assert.Null(nSubsequent);
    }

    [Fact]
    public void Evaluate_MultipleSimultaneousLowCrossings_AggregatesIntoOneNotification()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // Baseline for 3 rows
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 40.0),
            new QuotaObservation("antigravity", "Google Antigravity", "gemini_5h", "Gemini · 5-Hour", 30.0),
            new QuotaObservation("antigravity", "Google Antigravity", "gemini_weekly", "Gemini · Weekly", 50.0)
        });

        // Next refresh: Codex drops to 7%, Gemini 5h drops to 8%, Gemini weekly drops to 9%
        var notification = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 7.0),
            new QuotaObservation("antigravity", "Google Antigravity", "gemini_5h", "Gemini · 5-Hour", 8.0),
            new QuotaObservation("antigravity", "Google Antigravity", "gemini_weekly", "Gemini · Weekly", 9.0)
        });

        Assert.NotNull(notification);
        Assert.Equal("AIQuotaBar — Low quota", notification.Title);
        // Primary is worst (7% -> Codex Weekly)
        Assert.Contains("Codex Weekly has 7% remaining", notification.Message);
        Assert.Contains("2 other quota windows are also low", notification.Message);
    }

    [Fact]
    public void Evaluate_MultipleSimultaneousExhaustionCrossings_AggregatesIntoOneNotification()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // Baseline for 2 rows
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 20.0),
            new QuotaObservation("antigravity", "Google Antigravity", "claude_weekly", "Claude & GPT · Weekly", 20.0)
        });

        // Next refresh: both exhaust simultaneously
        var notification = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 0.0, QuotaWindowStatus.Exhausted),
            new QuotaObservation("antigravity", "Google Antigravity", "claude_weekly", "Claude & GPT · Weekly", 0.0, QuotaWindowStatus.Exhausted)
        });

        Assert.NotNull(notification);
        Assert.Equal("AIQuotaBar — Quota exhausted", notification.Title);
        Assert.Contains("has no quota remaining", notification.Message);
        Assert.Contains("1 other quota window is also low or exhausted", notification.Message);
    }

    [Fact]
    public void Evaluate_NotificationsDisabled_ReturnsNull()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // Baseline
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 20.0)
        }, notificationsEnabled: false);

        // Drop to 5% with notifications disabled
        var notification = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 5.0)
        }, notificationsEnabled: false);

        Assert.Null(notification);
    }

    [Fact]
    public void Evaluate_DisabledThenDropThenEnabled_ProducesNoAlert_UntilRecoveryAndSubsequentDrop()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // 1. 12% baseline with notifications enabled
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 12.0)
        }, notificationsEnabled: true);

        // 2. User disables notifications
        // 3. Quota drops to 8% while disabled
        var nDisabled = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 8.0)
        }, notificationsEnabled: false);
        Assert.Null(nDisabled);

        // 4. User enables notifications -> Quota still at 8%
        var nEnabled = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 8.0)
        }, notificationsEnabled: true);
        Assert.Null(nEnabled); // Must NOT immediately alert!

        // 5. Quota recovers to 95%
        var nRecovered = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 95.0)
        }, notificationsEnabled: true);
        Assert.Null(nRecovered);

        // 6. Quota drops to 9% -> Now it fires low alert!
        var nAlert = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 9.0)
        }, notificationsEnabled: true);
        Assert.NotNull(nAlert);
        Assert.Equal("AIQuotaBar — Low quota", nAlert.Title);
    }

    [Fact]
    public void Evaluate_DisabledThenExhaustionThenEnabled_ProducesNoAlert_UntilRecoveryAndSubsequentDrop()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // 1. 20% baseline
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 20.0)
        }, notificationsEnabled: true);

        // 2. Disable notifications and drop to 0% (exhausted)
        var nDisabled = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 0.0, QuotaWindowStatus.Exhausted)
        }, notificationsEnabled: false);
        Assert.Null(nDisabled);

        // 3. Enable notifications -> Quota still 0%
        var nEnabled = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 0.0, QuotaWindowStatus.Exhausted)
        }, notificationsEnabled: true);
        Assert.Null(nEnabled); // Must NOT immediately alert!

        // 4. Quota recovers to 100%
        var nRecovered = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 100.0)
        }, notificationsEnabled: true);
        Assert.Null(nRecovered);

        // 5. Quota drops to 0% -> Fires exhausted alert!
        var nAlert = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 0.0, QuotaWindowStatus.Exhausted)
        }, notificationsEnabled: true);
        Assert.NotNull(nAlert);
        Assert.Equal("AIQuotaBar — Quota exhausted", nAlert.Title);
    }

    [Fact]
    public void Evaluate_EnabledAtLowThenDisabledThenReEnabledWithoutChange_ProducesNoDuplicateAlert()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // 1. Baseline at 15%
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 15.0)
        }, notificationsEnabled: true);

        // 2. Drop to 8% -> alert fired
        var n1 = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 8.0)
        }, notificationsEnabled: true);
        Assert.NotNull(n1);

        // 3. User disables notifications
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 8.0)
        }, notificationsEnabled: false);

        // 4. User enables notifications without quota changing
        var n2 = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "weekly", "Weekly", 8.0)
        }, notificationsEnabled: true);
        Assert.Null(n2); // Must NOT duplicate alert!
    }

    [Fact]
    public void Evaluate_DisabledWhileHiddenThenUnhiddenAt4PercentThenEnabled_ProducesNoAlert()
    {
        var evaluator = new QuotaNotificationEvaluator();

        // Baseline: Row A (15%) and Row B (20%)
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "5h", "5-Hour", 15.0),
            new QuotaObservation("antigravity", "Google Antigravity", "gemini_weekly", "Gemini · Weekly", 20.0)
        }, notificationsEnabled: true);

        // Disable notifications
        // User hides Row B
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "5h", "5-Hour", 15.0)
        }, notificationsEnabled: false);

        // Row B quota drops to 4% while hidden and is unhidden
        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "5h", "5-Hour", 15.0),
            new QuotaObservation("antigravity", "Google Antigravity", "gemini_weekly", "Gemini · Weekly", 4.0)
        }, notificationsEnabled: false);

        // Enable notifications -> still at 4%
        var n = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "5h", "5-Hour", 15.0),
            new QuotaObservation("antigravity", "Google Antigravity", "gemini_weekly", "Gemini · Weekly", 4.0)
        }, notificationsEnabled: true);

        Assert.Null(n);
    }

    [Fact]
    public void Evaluate_InvalidPercentages_AreIgnoredSafely()
    {
        var evaluator = new QuotaNotificationEvaluator();

        evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "nan_window", "NaN Window", double.NaN),
            new QuotaObservation("codex", "OpenAI Codex", "inf_window", "Inf Window", double.PositiveInfinity)
        });

        var notification = evaluator.Evaluate(new[]
        {
            new QuotaObservation("codex", "OpenAI Codex", "nan_window", "NaN Window", double.NaN),
            new QuotaObservation("codex", "OpenAI Codex", "inf_window", "Inf Window", double.PositiveInfinity)
        });

        Assert.Null(notification);
    }
}
