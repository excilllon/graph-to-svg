using System.Globalization;
using System.Text;
using GraphSvg.Model;

namespace GraphSvg.Render;

public sealed class SvgRenderer
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly SvgStyle _s;
    private readonly int _maxCharsPerLine;
    private readonly bool _leftToRight;

    public SvgRenderer(SvgStyle? style = null, int maxCharsPerLine = 18, bool leftToRight = true)
    {
        _s = style ?? new SvgStyle();
        _maxCharsPerLine = Math.Max(6, maxCharsPerLine);
        _leftToRight = leftToRight;
    }

   /// <summary>
    /// Отрисовка кадра диаграммы. Синим выделяется <paramref name="accentNodeId"/>,
    /// оранжевыми становятся исходящие из него дуги. null — нейтральный вариант без акцента.
    /// </summary>
    public string Render(DirectedGraph graph, double width, double height, string? accentNodeId = null)
    {
        var sb = new StringBuilder(16 * 1024);

        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="no"?>""");
        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="{N(width)}" height="{N(height)}" viewBox="0 0 {N(width)} {N(height)}" xml:space="preserve" color-interpolation-filters="sRGB" class="st10">""");

        AppendStyles(sb);
        AppendMarkers(sb);

        sb.AppendLine("""	<g id="page">""");

        // Сначала линии, затем блоки — блоки перекрывают концы линий, как в исходном файле.
        foreach (var edge in graph.Edges)
            AppendEdge(sb, graph, edge, accentNodeId);

        foreach (var node in graph.Nodes)
            AppendNode(sb, node, accentNodeId);

        sb.AppendLine("	</g>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static bool IsAccentEdge(Edge edge, string? accentNodeId) =>
        edge.IsAccent ?? string.Equals(edge.From, accentNodeId, StringComparison.Ordinal);

    private void AppendStyles(StringBuilder sb)
    {
        sb.AppendLine("	<style type=\"text/css\">");
        sb.AppendLine("	<![CDATA[");
        sb.AppendLine($"		.st1 {{fill:{_s.AccentFill};stroke:{_s.NodeStroke};stroke-width:{N(_s.NodeStrokeWidth)}}}");
        sb.AppendLine($"		.st2 {{fill:{_s.AccentTextColor};font-family:{_s.FontFamily};font-size:1.16666em;font-weight:bold}}");
        sb.AppendLine($"		.st3 {{fill:{_s.NodeFill};stroke:{_s.NodeStroke};stroke-width:{N(_s.NodeStrokeWidth)}}}");
        sb.AppendLine($"		.st4 {{fill:{_s.NodeTextColor};font-family:{_s.FontFamily};font-size:1.16666em;font-weight:bold}}");
        sb.AppendLine($"		.st5 {{marker-end:url(#mrkr-accent);stroke:{_s.AccentEdgeColor};stroke-linecap:round;stroke-linejoin:round;stroke-width:{N(_s.EdgeStrokeWidth)}}}");
        sb.AppendLine($"		.st6 {{fill:{_s.AccentEdgeColor};fill-opacity:1;stroke:{_s.AccentEdgeColor};stroke-opacity:1;stroke-width:0.22935741822689}}");
        sb.AppendLine($"		.st7 {{marker-end:url(#mrkr-normal);stroke:{_s.EdgeColor};stroke-linecap:round;stroke-linejoin:round;stroke-width:{N(_s.EdgeStrokeWidth)}}}");
        sb.AppendLine($"		.st8 {{fill:{_s.EdgeColor};fill-opacity:1;stroke:{_s.EdgeColor};stroke-opacity:1;stroke-width:0.22935741822689}}");
        sb.AppendLine("		.st9 {font-size:1em}");
        sb.AppendLine($"		.st11 {{fill:{_s.EdgeColor};font-family:{_s.FontFamily};font-size:0.9em}}");
        sb.AppendLine("		.st10 {fill:none;fill-rule:evenodd;font-size:12px;overflow:visible;stroke-linecap:square;stroke-miterlimit:3}");
        sb.AppendLine("	]]>");
        sb.AppendLine("	</style>");
    }

    private static void AppendMarkers(StringBuilder sb)
    {
        sb.AppendLine("""	<defs id="Markers">""");
        sb.AppendLine("""		<g id="lend4"><path d="M 2 1 L 0 0 L 2 -1 L 2 1 " style="stroke:none"/></g>""");

        foreach (var (id, cls) in new[] { ("mrkr-accent", "st6"), ("mrkr-normal", "st8") })
        {
            sb.AppendLine($"""		<marker id="{id}" class="{cls}" refX="-5.6" orient="auto" markerUnits="strokeWidth" overflow="visible">""");
            sb.AppendLine("""			<use xlink:href="#lend4" transform="scale(-2.8,-2.8)"/>""");
            sb.AppendLine("		</marker>");
        }

        sb.AppendLine("	</defs>");
    }

      private void AppendNode(StringBuilder sb, Node node, string? accentNodeId)
    {
        var isAccent = string.Equals(node.Id, accentNodeId, StringComparison.Ordinal);
        var shapeClass = isAccent ? "st1" : "st3";
        var textClass = isAccent ? "st2" : "st4";

        sb.AppendLine($"""		<g id="node-{Esc(node.Id)}">""");
        sb.AppendLine($"			<title>{Esc(node.Label)}</title>");
        sb.AppendLine($"""			<rect x="{N(node.X)}" y="{N(node.Y)}" width="{N(node.Width)}" height="{N(node.Height)}" rx="{N(_s.CornerRadius)}" ry="{N(_s.CornerRadius)}" class="{shapeClass}"/>""");

        AppendLabel(sb, node, textClass);
        sb.AppendLine("		</g>");
    }

    /// <summary>Многострочная подпись по центру блока; строки формируются переносом по словам.</summary>
    private void AppendLabel(StringBuilder sb, Node node, string textClass)
    {
        var lines = WrapText(node.Label, _maxCharsPerLine);
        var lineHeight = _s.FontSize * 1.2;
        var firstBaseline = node.CenterY - (lines.Count - 1) * lineHeight / 2 + _s.FontSize * 0.35;

        sb.AppendLine($"""			<text x="{N(node.CenterX)}" y="{N(firstBaseline)}" text-anchor="middle" class="{textClass}">""");

        for (var i = 0; i < lines.Count; i++)
        {
            var dy = i == 0 ? "0" : N(lineHeight);
            sb.AppendLine($"""				<tspan x="{N(node.CenterX)}" dy="{dy}" class="st9">{Esc(lines[i])}</tspan>""");
        }

        sb.AppendLine("			</text>");
    }

        private void AppendEdge(StringBuilder sb, DirectedGraph graph, Edge edge, string? accentNodeId)
    {
        if (!graph.TryGet(edge.From, out var a) || !graph.TryGet(edge.To, out var b))
            return;

        // Явно заданный флаг дуги имеет приоритет; иначе оранжевой становится дуга из акцентного узла.
        var accent = edge.IsAccent ?? string.Equals(edge.From, accentNodeId, StringComparison.Ordinal);
        var cls = accent ? "st5" : "st7";
        var points = BuildRoute(a, b);
        var path = BuildPath(points);

        sb.AppendLine($"""		<g id="edge-{Esc(edge.From)}-{Esc(edge.To)}">""");
        sb.AppendLine($"""			<path d="{path}" class="{cls}"/>""");

        if (!string.IsNullOrEmpty(edge.Label))
        {
            var mid = points[points.Count / 2];
            sb.AppendLine($"""			<text x="{N(mid.X)}" y="{N(mid.Y - 4)}" text-anchor="middle" class="st11">{Esc(edge.Label)}</text>""");
        }

        sb.AppendLine("		</g>");
    }

    /// <summary>Ортогональный маршрут между двумя блоками.</summary>
    private List<(double X, double Y)> BuildRoute(Node a, Node b)
    {
        var pts = new List<(double X, double Y)>();
        var pad = _s.EdgePadding;

        if (a == b)
        {
            // Петля: скоба справа от блока.
            var y1 = a.Y + a.Height * 0.3;
            var y2 = a.Y + a.Height * 0.7;
            pts.Add((a.X + a.Width, y1));
            pts.Add((a.X + a.Width + pad, y1));
            pts.Add((a.X + a.Width + pad, y2));
            pts.Add((a.X + a.Width, y2));
            return pts;
        }

        if (_leftToRight)
        {
            if (b.X > a.X + a.Width - 1)
            {
                // Прямое направление: правая граница -> левая граница.
                var start = (X: a.X + a.Width, Y: a.CenterY);
                var end = (X: b.X, Y: b.CenterY);
                var midX = (start.X + end.X) / 2;

                pts.Add(start);
                if (Math.Abs(start.Y - end.Y) > 0.5)
                {
                    pts.Add((midX, start.Y));
                    pts.Add((midX, end.Y));
                }
                pts.Add(end);
            }
            else
            {
                // Обратная дуга: обход сверху блоков.
                var start = (X: a.CenterX, Y: a.Y);
                var end = (X: b.CenterX, Y: b.Y);
                var top = Math.Min(a.Y, b.Y) - pad;

                pts.Add(start);
                pts.Add((start.X, top));
                pts.Add((end.X, top));
                pts.Add(end);
            }
        }
        else
        {
            if (b.Y > a.Y + a.Height - 1)
            {
                var start = (X: a.CenterX, Y: a.Y + a.Height);
                var end = (X: b.CenterX, Y: b.Y);
                var midY = (start.Y + end.Y) / 2;

                pts.Add(start);
                if (Math.Abs(start.X - end.X) > 0.5)
                {
                    pts.Add((start.X, midY));
                    pts.Add((end.X, midY));
                }
                pts.Add(end);
            }
            else
            {
                var start = (X: a.X + a.Width, Y: a.CenterY);
                var end = (X: b.X + b.Width, Y: b.CenterY);
                var right = Math.Max(a.X + a.Width, b.X + b.Width) + pad;

                pts.Add(start);
                pts.Add((right, start.Y));
                pts.Add((right, end.Y));
                pts.Add(end);
            }
        }

        return pts;
    }

    private static string BuildPath(List<(double X, double Y)> pts)
    {
        var sb = new StringBuilder();
        sb.Append("M").Append(N(pts[0].X)).Append(' ').Append(N(pts[0].Y));
        for (var i = 1; i < pts.Count; i++)
            sb.Append(" L").Append(N(pts[i].X)).Append(' ').Append(N(pts[i].Y));
        return sb.ToString();
    }

    /// <summary>Перенос по словам; слишком длинное слово режется принудительно.</summary>
    private static List<string> WrapText(string text, int maxChars)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return new List<string> { string.Empty };

        foreach (var paragraph in text.Split('\n'))
        {
            var current = new StringBuilder();

            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = current.Length == 0 ? word : $"{current} {word}";

                if (candidate.Length <= maxChars)
                {
                    current.Clear().Append(candidate);
                    continue;
                }

                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }

                var rest = word;
                while (rest.Length > maxChars)
                {
                    result.Add(rest[..maxChars]);
                    rest = rest[maxChars..];
                }
                current.Append(rest);
            }

            if (current.Length > 0) result.Add(current.ToString());
        }

        return result.Count > 0 ? result : new List<string> { string.Empty };
    }

    private static string N(double v) => Math.Round(v, 3).ToString(Inv);

    private static string Esc(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");
}