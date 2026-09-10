namespace GraphSvg.Render;

/// <summary>Палитра и метрики, повторяющие приложенный экспорт из Visio.</summary>
public sealed class SvgStyle
{
    public string AccentFill { get; init; } = "#003274";
    public string AccentTextColor { get; init; } = "#ffffff";
    public string NodeFill { get; init; } = "#eaeaea";
    public string NodeTextColor { get; init; } = "#000000";
    public string NodeStroke { get; init; } = "#c7c8c8";
    public double NodeStrokeWidth { get; init; } = 0.25;

    public string AccentEdgeColor { get; init; } = "#ff9933";
    public string EdgeColor { get; init; } = "#a5a5a5";
    public double EdgeStrokeWidth { get; init; } = 0.75;

    public string FontFamily { get; init; } = "Calibri";
    public double FontSize { get; init; } = 14;
    public double CornerRadius { get; init; } = 12.75;

    /// <summary>Отступ прямой части линии от границы прямоугольника до изгиба.</summary>
    public double EdgePadding { get; init; } = 14;
}