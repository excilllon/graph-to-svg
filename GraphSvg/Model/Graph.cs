namespace GraphSvg.Model;

/// <summary>Узел ориентированного графа.</summary>
public sealed class Node
{
    public Node(string id, string? label = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Идентификатор узла не может быть пустым.", nameof(id));

        Id = id;
        Label = label ?? id;
    }

    public string Id { get; }

    /// <summary>Текст внутри прямоугольника. Переносится по словам при отрисовке.</summary>
    public string Label { get; set; }

    /// <summary>Акцентная заливка (как «Черновик» в примере). Задаётся вручную либо вычисляется как узел-источник.</summary>
    public bool IsAccent { get; set; }

    // --- Заполняется алгоритмом раскладки ---
    public int Layer { get; internal set; }
    public int Order { get; internal set; }
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public double Width { get; internal set; }
    public double Height { get; internal set; }

    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;

    public override string ToString() => Id;
}

/// <summary>Направленная дуга.</summary>
public sealed class Edge
{
    public Edge(string from, string to, string? label = null)
    {
        From = from;
        To = to;
        Label = label;
    }

    public string From { get; }
    public string To { get; }
    public string? Label { get; }

    /// <summary>Акцентный цвет линии (оранжевый). По умолчанию наследуется от узла-источника.</summary>
    public bool? IsAccent { get; set; }
}

/// <summary>Ориентированный граф с быстрым доступом по идентификатору.</summary>
public sealed class DirectedGraph
{
    private readonly Dictionary<string, Node> _nodes = new(StringComparer.Ordinal);
    private readonly List<Edge> _edges = new();

    public IReadOnlyCollection<Node> Nodes => _nodes.Values;
    public IReadOnlyList<Edge> Edges => _edges;

    public Node AddNode(string id, string? label = null, bool accent = false)
    {
        if (_nodes.TryGetValue(id, out var existing))
        {
            if (label is not null) existing.Label = label;
            if (accent) existing.IsAccent = true;
            return existing;
        }

        var node = new Node(id, label) { IsAccent = accent };
        _nodes.Add(id, node);
        return node;
    }

    public Edge AddEdge(string from, string to, string? label = null)
    {
        // Узлы создаются неявно — удобно при разборе текстового описания.
        AddNode(from);
        AddNode(to);

        var edge = new Edge(from, to, label);
        _edges.Add(edge);
        return edge;
    }

    public Node this[string id] => _nodes[id];

    public bool TryGet(string id, out Node node) => _nodes.TryGetValue(id, out node!);

    public IEnumerable<Edge> OutEdges(string id) => _edges.Where(e => e.From == id);

    public IEnumerable<Edge> InEdges(string id) => _edges.Where(e => e.To == id);

    /// <summary>
    /// Разбор простого текстового формата:
    ///   # комментарий
    ///   NodeId: Подпись узла        — объявление подписи
    ///   A -> B                      — дуга
    ///   A -> B : подпись дуги
    /// </summary>
    public static DirectedGraph Parse(IEnumerable<string> lines)
    {
        var graph = new DirectedGraph();

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//", StringComparison.Ordinal))
                continue;

            var arrow = line.IndexOf("->", StringComparison.Ordinal);
            if (arrow < 0)
            {
                // Объявление узла: "id: подпись"
                var colon = line.IndexOf(':');
                if (colon > 0)
                    graph.AddNode(line[..colon].Trim(), line[(colon + 1)..].Trim());
                else
                    graph.AddNode(line);
                continue;
            }

            var from = line[..arrow].Trim();
            var rest = line[(arrow + 2)..];

            string to;
            string? edgeLabel = null;

            var labelSep = rest.IndexOf(':');
            if (labelSep >= 0)
            {
                to = rest[..labelSep].Trim();
                edgeLabel = rest[(labelSep + 1)..].Trim();
            }
            else
            {
                to = rest.Trim();
            }

            graph.AddEdge(from, to, string.IsNullOrEmpty(edgeLabel) ? null : edgeLabel);
        }

        return graph;
    }
}