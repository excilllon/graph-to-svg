using GraphSvg.Layout;
using GraphSvg.Model;
using GraphSvg.Render;

namespace GraphSvg;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            string? input = null;
            var outputDir = ".";
            var topDown = false;
            var single = false;              // старое поведение: один файл без акцента-перебора
            var withNeutral = false;         // дополнительно сохранить кадр без выделения

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-i" or "--input" when i + 1 < args.Length: input = args[++i]; break;
                    case "-o" or "--out-dir" when i + 1 < args.Length: outputDir = args[++i]; break;
                    case "--top-down": topDown = true; break;
                    case "--single": single = true; break;
                    case "--with-neutral": withNeutral = true; break;
                    case "-h" or "--help": PrintUsage(); return 0;
                    default:
                        Console.Error.WriteLine($"Неизвестный аргумент: {args[i]}");
                        PrintUsage();
                        return 2;
                }
            }

            var graph = input is null
                ? BuildSampleGraph()
                : DirectedGraph.Parse(File.ReadLines(input));

            if (graph.Nodes.Count == 0)
            {
                Console.Error.WriteLine("Граф пуст: нечего рисовать.");
                return 2;
            }

            // Раскладка считается ОДИН раз — во всех файлах блоки стоят на одних и тех же местах,
            // поэтому кадры можно листать как покадровую анимацию.
            var options = new LayoutOptions { LeftToRight = !topDown };
            var (w, h) = new SugiyamaLayout(options).Apply(graph);

            var renderer = new SvgRenderer(new SvgStyle(), options.MaxCharsPerLine, options.LeftToRight);

            Directory.CreateDirectory(outputDir);

            if (single)
            {
                var path = Path.Combine(outputDir, "graph.svg");
                File.WriteAllText(path, renderer.Render(graph, w, h));
                Console.WriteLine($"Готово: {path}");
                return 0;
            }

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var written = 0;

            if (withNeutral)
            {
                var neutralName = FileNaming.MakeUnique(FileNaming.Sanitize("_граф"), used);
                var neutralPath = Path.Combine(outputDir, neutralName + ".svg");
                File.WriteAllText(neutralPath, renderer.Render(graph, w, h));
                Console.WriteLine($"  {neutralName}.svg — без акцента");
                written++;
            }

            // По одному файлу на узел: очередной блок синий, его исходящие дуги оранжевые.
            foreach (var node in graph.Nodes.OrderBy(n => n.Layer).ThenBy(n => n.Order))
            {
                var baseName = FileNaming.Sanitize(node.Label, node.Id);
                var fileName = FileNaming.MakeUnique(baseName, used);
                var path = Path.Combine(outputDir, fileName + ".svg");

                File.WriteAllText(path, renderer.Render(graph, w, h, node.Id));

                var outCount = graph.OutEdges(node.Id).Count();
                Console.WriteLine($"  {fileName}.svg — акцент «{node.Label}», оранжевых стрелок: {outCount}");
                written++;
            }

            Console.WriteLine($"Готово: {written} файлов в «{Path.GetFullPath(outputDir)}», размер кадра {w:F0}×{h:F0}.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка: {ex.Message}");
            return 1;
        }
    }

    private static DirectedGraph BuildSampleGraph()
    {
        var g = new DirectedGraph();

        g.AddNode("draft", "Черновик");
        g.AddNode("progress", "В проработке");
        g.AddNode("postponed", "Отложена");
        g.AddNode("cancelled", "Отменена");
        g.AddNode("preview", "На предварительном рассмотрении");
        g.AddNode("pdk", "На рассмотрении ПДК");
        g.AddNode("approved", "Утверждена");
        g.AddNode("project", "Создан проект");
        g.AddNode("zni", "ЗНИ к существующему проекту");

        g.AddEdge("draft", "postponed");
        g.AddEdge("draft", "progress");
        g.AddEdge("draft", "cancelled");
        g.AddEdge("postponed", "progress");
        g.AddEdge("progress", "cancelled");
        g.AddEdge("progress", "preview");
        g.AddEdge("preview", "progress");
        g.AddEdge("preview", "pdk");
        g.AddEdge("pdk", "approved");
        g.AddEdge("pdk", "preview");
        g.AddEdge("approved", "project");
        g.AddEdge("approved", "zni");

        return g;
    }

    private static void PrintUsage() => Console.WriteLine(
        """
        Использование: GraphSvg [-i graph.txt] [-o каталог] [--top-down] [--single] [--with-neutral]

          -i, --input       входной файл с описанием графа (без него берётся встроенный пример)
          -o, --out-dir     каталог для результатов (по умолчанию текущий)
              --top-down    диаграмма растёт сверху вниз, а не слева направо
              --single      один файл graph.svg без перебора акцентов
              --with-neutral дополнительно сохранить кадр без выделения

        По умолчанию создаётся по одному SVG на каждый узел: узел выделен синим,
        его исходящие стрелки — оранжевые. Имя файла = подпись узла (иначе идентификатор).

        Формат входного файла:
          # комментарий
          id: Подпись узла
          a -> b
          a -> b : подпись дуги
        """);
}