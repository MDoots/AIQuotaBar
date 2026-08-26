namespace AIQuotaBar.App.ViewModels;

using AIQuotaBar.Core.Models;
using AIQuotaBar.Core.Utils;

public sealed class QuotaWindowViewModel : ViewModelBase
{
    private readonly QuotaWindow _model;

    public string Id => _model.Id;
    public string DisplayName => _model.DisplayName;
    public int RemainingPercent => _model.RemainingPercent;
    public int UsedPercent => _model.ClampedUsedPercent;
    public string RemainingText => $"{_model.RemainingPercent}% remaining";
    public string UsedText => $"{_model.ClampedUsedPercent}% used";
    public string? ResetCountdown => CountdownFormatter.FormatCountdown(_model.ResetsAt);
    public bool HasCountdown => !string.IsNullOrWhiteSpace(ResetCountdown);
    public string? ExactResetTime => _model.ResetsAt?.ToLocalTime().ToString("dddd, d MMMM, HH:mm");
    
    public string TooltipText
    {
        get
        {
            var lines = new List<string>
            {
                $"{DisplayName}: {RemainingPercent}% remaining ({UsedPercent}% used)"
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
