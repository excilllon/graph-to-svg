using GraphSvg.Model;

namespace GraphSvg.Layout;

public sealed class LayoutOptions
{
    public double NodeWidth { get; init; } = 127.56;      // как в примере Visio
    public double NodeHeight { get; init; } = 63.79;
    public double LayerGap { get; init; } = 85;           // горизонтальный зазор между слоями
    public double NodeGap { get; init; } = 35;            // вертикальный зазор внутри слоя
    public double Margin { get; init; } = 40;
    public double FontSize { get; init; } = 14;
    public int MaxCharsPerLine { get; init; } = 18;
    public bool LeftToRight { get; init; } = true;         // false — сверху вниз
    public int BarycenterPasses { get; init; } = 8;
}

public sealed class SugiyamaLayout
{
    private readonly LayoutOptions _o;

    public SugiyamaLayout(LayoutOptions? options = null) => _o = options ?? new LayoutOptions();

    public (double Width, double Height) Apply(DirectedGraph graph)
    {
        var nodes = graph.Nodes.ToList();
        if (nodes.Count == 0) return (_o.Margin * 2, _o.Margin * 2);

        AssignLayers(graph, nodes);
        OrderWithinLayers(graph, nodes);
        return AssignCoordinates(nodes);
    }

    /// <summary>Слой = длина самого длинного пути от источника. Обратные дуги игнорируются.</summary>
    private static void AssignLayers(DirectedGraph graph, List<Node> nodes)
    {
        var indegree = nodes.ToDictionary(n => n.Id, n => graph.InEdges(n.Id).Count(e => e.From != n.Id));
        var layer = nodes.ToDictionary(n => n.Id, _ => 0);

        // Топологический обход Кана; узлы, оставшиеся в циклах, добавляются по остаточному порядку.
        var queue = new Queue<string>(indegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var processed = new HashSet<string>(StringComparer.Ordinal);

        if (queue.Count == 0 && nodes.Count > 0)
            queue.Enqueue(nodes[0].Id);   // граф целиком цикличен — берём произвольную точку входа

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!processed.Add(id)) continue;

            foreach (var e in graph.OutEdges(id))
            {
                if (e.To == id) continue;                       // петля
                layer[e.To] = Math.Max(layer[e.To], layer[id] + 1);

                if (--indegree[e.To] <= 0 && !processed.Contains(e.To))
                    queue.Enqueue(e.To);
            }
        }

        // Узлы, недостижимые из-за циклов, ставим за их предшественниками.
        foreach (var n in nodes.Where(n => !processed.Contains(n.Id)))
        {
            var preds = graph.InEdges(n.Id).Where(e => e.From != n.Id).Select(e => layer[e.From]).ToList();
            layer[n.Id] = preds.Count > 0 ? preds.Max() + 1 : 0;
        }

        foreach (var n in nodes) n.Layer = layer[n.Id];
    }

    /// <summary>Барицентрическая сортировка: узел тянется к среднему положению соседей.</summary>
    private void OrderWithinLayers(DirectedGraph graph, List<Node> nodes)
    {
        var layers = nodes.GroupBy(n => n.Layer)
                          .OrderBy(g => g.Key)
                          .Select(g => g.ToList())
                          .ToList();

        foreach (var l in layers)
            for (var i = 0; i < l.Count; i++) l[i].Order = i;

        for (var pass = 0; pass < _o.BarycenterPasses; pass++)
        {
            var forward = pass % 2 == 0;
            var sequence = forward ? layers : Enumerable.Reverse(layers).ToList();

            foreach (var l in sequence)
            {
                foreach (var n in l)
                {
                    var neighbours = forward
                        ? graph.InEdges(n.Id).Select(e => graph[e.From])
                        : graph.OutEdges(n.Id).Select(e => graph[e.To]);

                    var orders = neighbours.Where(x => x.Layer != n.Layer)
                                           .Select(x => (double)x.Order)
                                           .ToList();

                    // Нет соседей в смежном слое — сохраняем текущую позицию.
                    n.Y = orders.Count > 0 ? orders.Average() : n.Order;
                }

                var sorted = l.OrderBy(n => n.Y).ThenBy(n => n.Order).ToList();
                for (var i = 0; i < sorted.Count; i++) sorted[i].Order = i;
            }
        }
    }

    private (double Width, double Height) AssignCoordinates(List<Node> nodes)
    {
        foreach (var n in nodes)
        {
            n.Width = _o.NodeWidth;
            n.Height = _o.NodeHeight;
        }

        var layers = nodes.GroupBy(n => n.Layer).OrderBy(g => g.Key).ToList();
        var maxCount = layers.Max(g => g.Count());
        var crossSize = maxCount * _o.NodeHeight + (maxCount - 1) * _o.NodeGap;

        foreach (var group in layers)
        {
            var ordered = group.OrderBy(n => n.Order).ToList();
            var span = ordered.Count * _o.NodeHeight + (ordered.Count - 1) * _o.NodeGap;
            var offset = (crossSize - span) / 2;   // центрируем слой

            for (var i = 0; i < ordered.Count; i++)
            {
                var main = _o.Margin + group.Key * (_o.NodeWidth + _o.LayerGap);
                var cross = _o.Margin + offset + i * (_o.NodeHeight + _o.NodeGap);

                if (_o.LeftToRight)
                {
                    ordered[i].X = main;
                    ordered[i].Y = cross;
                }
                else
                {
                    // Сверху вниз: слои идут по вертикали, узлы внутри слоя — по горизонтали.
                    ordered[i].X = _o.Margin + offset + i * (_o.NodeWidth + _o.NodeGap);
                    ordered[i].Y = _o.Margin + group.Key * (_o.NodeHeight + _o.LayerGap);
                }
            }
        }

        var width = nodes.Max(n => n.X + n.Width) + _o.Margin;
        var height = nodes.Max(n => n.Y + n.Height) + _o.Margin;
        return (width, height);
    }
}