namespace AIQuotaBar.App.ViewModels;

using System.Windows;
using AIQuotaBar.App.Layout;
using AIQuotaBar.Core.Models;
using AIQuotaBar.Core.Utils;

public sealed class QuotaWindowViewModel : ViewModelBase
{
    private readonly QuotaWindow _model;
    private readonly string? _providerId;
    private WidgetLayoutMode _layoutMode = WidgetLayoutMode.Full;

    public string Id => _model.Id;
    public string? ProviderId => _providerId;
    public string RawDisplayName => _model.DisplayName;
    public string DisplayName => QuotaLabelFormatter.Format(_model.DisplayName, _layoutMode, _providerId, _model.Id);
    public double RemainingPercent => _model.RemainingPercent;
    public double UsedPercent => _model.ClampedUsedPercent;
    public int DisplayRemainingPercent => (int)Math.Round(_model.RemainingPercent, MidpointRounding.AwayFromZero);
    public int DisplayUsedPercent => (int)Math.Round(_model.ClampedUsedPercent, MidpointRounding.AwayFromZero);
    public string RemainingText => $"{DisplayRemainingPercent}%";
    public string PercentText => $"{DisplayRemainingPercent}%";
    public string UsedText => $"{DisplayUsedPercent}% used";
    public string? ResetCountdown => CountdownFormatter.FormatCountdown(_model.ResetsAt);
    public bool HasCountdown => !string.IsNullOrWhiteSpace(ResetCountdown);
    public bool ShowCountdown => _layoutMode is WidgetLayoutMode.Full or WidgetLayoutMode.Compact && HasCountdown;
    public Thickness RemainingMargin => ShowCountdown ? new Thickness(0, 0, 6, 0) : new Thickness(0);
    public string? ExactResetTime => _model.ResetsAt?.ToLocalTime().ToString("dddd, d MMMM, HH:mm");

    public WidgetLayoutMode LayoutMode
    {
        get => _layoutMode;
        set
        {
            if (_layoutMode != value)
            {
                _layoutMode = value;
                OnPropertyChanged(nameof(LayoutMode));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(ShowCountdown));
                OnPropertyChanged(nameof(RemainingMargin));
                OnPropertyChanged(nameof(TooltipText));
            }
        }
    }

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

    public QuotaWindowViewModel(QuotaWindow model, string? providerId = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _providerId = providerId;
    }

    public void RefreshCountdown()
    {
        OnPropertyChanged(nameof(ResetCountdown));
        OnPropertyChanged(nameof(HasCountdown));
        OnPropertyChanged(nameof(ExactResetTime));
        OnPropertyChanged(nameof(TooltipText));
    }
}
