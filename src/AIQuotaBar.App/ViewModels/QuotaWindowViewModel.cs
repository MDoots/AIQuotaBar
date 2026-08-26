namespace AIQuotaBar.App.ViewModels;

using AIQuotaBar.Core.Models;
using AIQuotaBar.Core.Utils;

public sealed class QuotaWindowViewModel : ViewModelBase
{
    private readonly QuotaWindow _model;

    public string Id => _model.Id;
    public string DisplayName => _model.DisplayName;
    public double RemainingPercent => _model.RemainingPercent;
    public double UsedPercent => _model.ClampedUsedPercent;
    public int DisplayRemainingPercent => (int)Math.Round(_model.RemainingPercent, MidpointRounding.AwayFromZero);
    public int DisplayUsedPercent => (int)Math.Round(_model.ClampedUsedPercent, MidpointRounding.AwayFromZero);
    public string RemainingText => $"{DisplayRemainingPercent}%";
    public string PercentText => $"{DisplayRemainingPercent}%";
    public string UsedText => $"{DisplayUsedPercent}% used";
    public string? ResetCountdown => CountdownFormatter.FormatCountdown(_model.ResetsAt);
    public bool HasCountdown => !string.IsNullOrWhiteSpace(ResetCountdown);
    public string? ExactResetTime => _model.ResetsAt?.ToLocalTime().ToString("dddd, d MMMM, HH:mm");

    public string FormattedRemainingPercent
    {
        get
        {
            var rounded1 = Math.Round(_model.RemainingPercent, 1, MidpointRounding.AwayFromZero);
            var rounded0 = Math.Round(_model.RemainingPercent, 0, MidpointRounding.AwayFromZero);
            return Math.Abs(rounded1 - rounded0) > 0.001
                ? rounded1.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                : rounded0.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
    
    public string TooltipText
    {
        get
        {
            var lines = new List<string>
            {
                $"{DisplayName}: {FormattedRemainingPercent}% quota remaining"
            };

            if (!string.IsNullOrWhiteSpace(ResetCountdown))
            {
                lines.Add(char.ToUpperInvariant(ResetCountdown[0]) + ResetCountdown[1..]);
            }

            if (!string.IsNullOrWhiteSpace(ExactResetTime))
            {
                lines.Add($"Reset: {ExactResetTime}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    public string AccessibilityText
    {
        get
        {
            var text = $"{DisplayName}, {DisplayRemainingPercent} percent remaining";
            if (!string.IsNullOrWhiteSpace(ResetCountdown))
            {
                text += $", {ResetCountdown}";
            }
            return text;
        }
    }

    public QuotaWindowStatus Status => _model.Status;
    public bool IsExhausted => _model.Status == QuotaWindowStatus.Exhausted;

    public QuotaWindowViewModel(QuotaWindow model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public void RefreshCountdown()
    {
        OnPropertyChanged(nameof(ResetCountdown));
        OnPropertyChanged(nameof(HasCountdown));
        OnPropertyChanged(nameof(ExactResetTime));
        OnPropertyChanged(nameof(TooltipText));
    }
}
