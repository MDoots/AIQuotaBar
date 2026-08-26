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
    }
}
