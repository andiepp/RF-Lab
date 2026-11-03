#nullable enable

using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RfLab.Controls;

/// <summary>
/// Draws one measurement series as a polyline against a bounded value axis
/// and a bounded abscissa.
/// </summary>
/// <remarks>
/// This replaces the third-party chart of the original RF-Lab application. It
/// carries no licence and no external dependency, and renders the same
/// picture the instrument's operator expects: one green trace on the dark
/// measurement surface, with the axis bounds the view model publishes.
/// </remarks>
public partial class RfLabChart : UserControl
{
    private const int AbscissaTickCount = 5;
    private const int ValueTickCount = 7;

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(RfLabChart),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty MinimumXProperty =
        DependencyProperty.Register(
            nameof(MinimumX),
            typeof(double),
            typeof(RfLabChart),
            new PropertyMetadata(0.0, OnRenderInputChanged));

    public static readonly DependencyProperty MaximumXProperty =
        DependencyProperty.Register(
            nameof(MaximumX),
            typeof(double),
            typeof(RfLabChart),
            new PropertyMetadata(500.0, OnRenderInputChanged));

    public static readonly DependencyProperty MinimumYProperty =
        DependencyProperty.Register(
            nameof(MinimumY),
            typeof(double),
            typeof(RfLabChart),
            new PropertyMetadata(-70.0, OnRenderInputChanged));

    public static readonly DependencyProperty MaximumYProperty =
        DependencyProperty.Register(
            nameof(MaximumY),
            typeof(double),
            typeof(RfLabChart),
            new PropertyMetadata(10.0, OnRenderInputChanged));

    public static readonly DependencyProperty AxisLabelProperty =
        DependencyProperty.Register(
            nameof(AxisLabel),
            typeof(string),
            typeof(RfLabChart),
            new PropertyMetadata(string.Empty, OnRenderInputChanged));

    public static readonly DependencyProperty ValueLabelProperty =
        DependencyProperty.Register(
            nameof(ValueLabel),
            typeof(string),
            typeof(RfLabChart),
            new PropertyMetadata(string.Empty, OnRenderInputChanged));

    public static readonly DependencyProperty SeriesBrushProperty =
        DependencyProperty.Register(
            nameof(SeriesBrush),
            typeof(Brush),
            typeof(RfLabChart),
            new PropertyMetadata(Brushes.LightGreen, OnRenderInputChanged));

    public static readonly DependencyProperty AxisBrushProperty =
        DependencyProperty.Register(
            nameof(AxisBrush),
            typeof(Brush),
            typeof(RfLabChart),
            new PropertyMetadata(Brushes.Gray, OnRenderInputChanged));

    public static readonly DependencyProperty LabelBrushProperty =
        DependencyProperty.Register(
            nameof(LabelBrush),
            typeof(Brush),
            typeof(RfLabChart),
            new PropertyMetadata(Brushes.White, OnRenderInputChanged));

    public RfLabChart()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public double MinimumX
    {
        get => (double)GetValue(MinimumXProperty);
        set => SetValue(MinimumXProperty, value);
    }

    public double MaximumX
    {
        get => (double)GetValue(MaximumXProperty);
        set => SetValue(MaximumXProperty, value);
    }

    public double MinimumY
    {
        get => (double)GetValue(MinimumYProperty);
        set => SetValue(MinimumYProperty, value);
    }

    public double MaximumY
    {
        get => (double)GetValue(MaximumYProperty);
        set => SetValue(MaximumYProperty, value);
    }

    public string AxisLabel
    {
        get => (string)GetValue(AxisLabelProperty);
        set => SetValue(AxisLabelProperty, value);
    }

    public string ValueLabel
    {
        get => (string)GetValue(ValueLabelProperty);
        set => SetValue(ValueLabelProperty, value);
    }

    public Brush SeriesBrush
    {
        get => (Brush)GetValue(SeriesBrushProperty);
        set => SetValue(SeriesBrushProperty, value);
    }

    public Brush AxisBrush
    {
        get => (Brush)GetValue(AxisBrushProperty);
        set => SetValue(AxisBrushProperty, value);
    }

    public Brush LabelBrush
    {
        get => (Brush)GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    private static void OnItemsSourceChanged(
        DependencyObject source,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (source is not RfLabChart chart)
        {
            return;
        }

        if (eventArgs.OldValue is INotifyCollectionChanged previous)
        {
            previous.CollectionChanged -= chart.SeriesCollectionChanged;
        }

        if (eventArgs.NewValue is INotifyCollectionChanged current)
        {
            current.CollectionChanged += chart.SeriesCollectionChanged;
        }

        chart.Render();
    }

    private static void OnRenderInputChanged(
        DependencyObject source,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        (source as RfLabChart)?.Render();
    }

    private void SeriesCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs eventArgs) =>
        Dispatcher.Invoke(Render);

    private void PlotCanvasSizeChanged(object sender, SizeChangedEventArgs eventArgs) =>
        Render();

    private void Render()
    {
        if (!IsLoaded)
        {
            return;
        }

        SeriesLine.Stroke = SeriesBrush;
        SeriesLabel.Foreground = LabelBrush;
        AbscissaAxisLabel.Foreground = LabelBrush;
        ValueAxisLabel.Foreground = LabelBrush;
        PlotBorder.BorderBrush = AxisBrush;
        AbscissaAxisLabel.Text = AxisLabel;
        ValueAxisLabel.Text = ValueLabel;

        RenderTicks();
        RenderSeries();
    }

    private void RenderTicks()
    {
        AbscissaTickCanvas.Children.Clear();
        ValueTickCanvas.Children.Clear();

        double width = PlotCanvas.ActualWidth;
        double height = PlotCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        for (int index = 0; index < AbscissaTickCount; index++)
        {
            double fraction = index / (double)(AbscissaTickCount - 1);
            var label = new TextBlock
            {
                Text = FormatAbscissa(MinimumX + ((MaximumX - MinimumX) * fraction)),
                Foreground = LabelBrush,
                FontSize = 10
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(
                label,
                Math.Clamp(
                    (width * fraction) - (label.DesiredSize.Width / 2),
                    0,
                    Math.Max(0, width - label.DesiredSize.Width)));
            AbscissaTickCanvas.Children.Add(label);
        }

        // round values: a step of 1, 2, 2.5 or 5 times a power of ten
        double spanY = MaximumY - MinimumY;
        if (spanY <= 0)
        {
            return;
        }

        double rawStep = spanY / (ValueTickCount - 1);
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        double step = new[] { 1, 2, 2.5, 5, 10 }.Select(factor => factor * magnitude).First(candidate => candidate >= rawStep);
        for (double tick = Math.Ceiling(MinimumY / step) * step; tick <= MaximumY + (step / 1000); tick += step)
        {
            double fraction = (MaximumY - tick) / spanY;
            var label = new TextBlock
            {
                Text = tick.ToString("0.#", CultureInfo.CurrentCulture),
                Foreground = LabelBrush,
                FontSize = 10
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetTop(
                label,
                Math.Clamp(
                    (height * fraction) - (label.DesiredSize.Height / 2),
                    0,
                    Math.Max(0, height - label.DesiredSize.Height)));
            Canvas.SetRight(label, 4);
            ValueTickCanvas.Children.Add(label);
        }
    }

    private void RenderSeries()
    {
        SeriesLine.Points.Clear();

        double width = PlotCanvas.ActualWidth;
        double height = PlotCanvas.ActualHeight;
        double spanX = MaximumX - MinimumX;
        double spanY = MaximumY - MinimumY;

        if (ItemsSource is null
            || width <= 0
            || height <= 0
            || spanX <= 0
            || spanY <= 0)
        {
            return;
        }

        foreach (object? item in ItemsSource)
        {
            if (item is not RfLabMeasurementPoint point)
            {
                continue;
            }

            double x = (point.X - MinimumX) / spanX * width;
            double y = height - ((point.Y - MinimumY) / spanY * height);
            SeriesLine.Points.Add(
                new Point(
                    Math.Clamp(x, 0, width),
                    Math.Clamp(y, 0, height)));
        }
    }

    private string FormatAbscissa(double value) =>
        Math.Abs(value) >= 1_000_000
            ? (value / 1_000_000).ToString("0.###", CultureInfo.CurrentCulture) + "M"
            : value.ToString("0.#", CultureInfo.CurrentCulture);
}

/// <summary>
/// One rendered measurement sample.
/// </summary>
public sealed record RfLabMeasurementPoint(double X, double Y);
