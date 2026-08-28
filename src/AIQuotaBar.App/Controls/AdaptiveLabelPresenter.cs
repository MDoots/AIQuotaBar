namespace AIQuotaBar.App.Controls;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

public sealed class AdaptiveLabelPresenter : FrameworkElement
{
    public static readonly DependencyProperty CandidateLabelsProperty =
        DependencyProperty.Register(
            nameof(CandidateLabels),
            typeof(IReadOnlyList<string>),
            typeof(AdaptiveLabelPresenter),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty =
        DependencyProperty.Register(
            nameof(FontFamily),
            typeof(FontFamily),
            typeof(AdaptiveLabelPresenter),
            new FrameworkPropertyMetadata(new FontFamily("Segoe UI Variable, Segoe UI, sans-serif"), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(
            nameof(FontSize),
            typeof(double),
            typeof(AdaptiveLabelPresenter),
            new FrameworkPropertyMetadata(11.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontWeightProperty =
        DependencyProperty.Register(
            nameof(FontWeight),
            typeof(FontWeight),
            typeof(AdaptiveLabelPresenter),
            new FrameworkPropertyMetadata(FontWeights.Medium, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Brush),
            typeof(AdaptiveLabelPresenter),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<string>? CandidateLabels
    {
        get => (IReadOnlyList<string>?)GetValue(CandidateLabelsProperty);
        set => SetValue(CandidateLabelsProperty, value);
    }

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    private string _selectedText = string.Empty;
    private FormattedText? _formattedText;

    protected override Size MeasureOverride(Size availableSize)
    {
        var candidates = CandidateLabels;
        if (candidates == null || candidates.Count == 0)
        {
            _selectedText = string.Empty;
            _formattedText = null;
            return new Size(0, Math.Max(0, FontSize * 1.3));
        }

        var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var maxW = availableSize.Width;

        FormattedText? chosen = null;
        string chosenText = candidates[^1];

        for (int i = 0; i < candidates.Count; i++)
        {
            var text = candidates[i];
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var ft = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                FontSize,
                Foreground ?? Brushes.White,
                dpi);

            if (double.IsPositiveInfinity(maxW) || ft.Width <= maxW || i == candidates.Count - 1)
            {
                chosen = ft;
                chosenText = text;
                break;
            }
        }

        if (chosen == null)
        {
            chosenText = candidates[^1];
            chosen = new FormattedText(
                chosenText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                FontSize,
                Foreground ?? Brushes.White,
                dpi);
        }

        _selectedText = chosenText;
        _formattedText = chosen;

        var width = double.IsPositiveInfinity(maxW) ? chosen.Width : Math.Min(chosen.Width, maxW);
        return new Size(Math.Max(0, width), chosen.Height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (_formattedText != null && !string.IsNullOrEmpty(_selectedText))
        {
            var yOffset = Math.Max(0, (ActualHeight - _formattedText.Height) / 2.0);
            dc.DrawText(_formattedText, new Point(0, yOffset));
        }
    }
}
