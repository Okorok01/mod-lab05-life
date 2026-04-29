using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SkiaSharp;

namespace Life
{
    public class Settings
    {
        public int Width { get; set; } = 50;
        public int Height { get; set; } = 50;
        public double InitialDensity { get; set; } = 0.3;
        public int MaxGenerations { get; set; } = 1000;
        public int StablePeriod { get; set; } = 10;
        public string Rule { get; set; } = "B3/S23";
        public string SaveFile { get; set; } = "state.txt";
        public string LoadFile { get; set; } = "state.txt";
    }

    public class Board
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        private bool[,] cells;
        private HashSet<int> birthCounts;
        private HashSet<int> survivalCounts;

        public Board(int width, int height, string rule = "B3/S23")
        {
            Width = width;
            Height = height;
            cells = new bool[height, width];
            ParseRule(rule);
        }

        private void ParseRule(string rule)
        {
            var parts = rule.Split('/');
            birthCounts = new HashSet<int>();
            survivalCounts = new HashSet<int>();
            foreach (var ch in parts[0].Substring(1))
                birthCounts.Add(ch - '0');
            foreach (var ch in parts[1].Substring(1))
                survivalCounts.Add(ch - '0');
        }

        public bool this[int x, int y] =>
            x >= 0 && x < Width && y >= 0 && y < Height && cells[y, x];

        public void SetCell(int x, int y, bool alive)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
                cells[y, x] = alive;
        }

        public void Randomize(double density)
        {
            var rnd = new Random();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    cells[y, x] = rnd.NextDouble() < density;
        }

        public void Clear() => Array.Clear(cells, 0, cells.Length);

        public void LoadPattern(IEnumerable<(int x, int y)> coordinates)
        {
            Clear();
            foreach (var (x, y) in coordinates)
                SetCell(x, y, true);
        }

        private int CountNeighbors(int x, int y)
        {
            int count = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && nx < Width && ny >= 0 && ny < Height && cells[ny, nx])
                        count++;
                }
            return count;
        }

        public void NextGeneration()
        {
            bool[,] newCells = new bool[Height, Width];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int n = CountNeighbors(x, y);
                    if (cells[y, x])
                        newCells[y, x] = survivalCounts.Contains(n);
                    else
                        newCells[y, x] = birthCounts.Contains(n);
                }
            cells = newCells;
        }

        public int Population => cells.Cast<bool>().Count(c => c);

        public List<List<(int x, int y)>> FindConnectedComponents()
        {
            bool[,] visited = new bool[Height, Width];
            var components = new List<List<(int, int)>>();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (cells[y, x] && !visited[y, x])
                    {
                        var comp = new List<(int, int)>();
                        var stack = new Stack<(int, int)>();
                        stack.Push((x, y));
                        visited[y, x] = true;
                        while (stack.Count > 0)
                        {
                            var (cx, cy) = stack.Pop();
                            comp.Add((cx, cy));
                            for (int dy = -1; dy <= 1; dy++)
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    if (dx == 0 && dy == 0) continue;
                                    int nx = cx + dx, ny = cy + dy;
                                    if (nx >= 0 && nx < Width && ny >= 0 && ny < Height &&
                                        cells[ny, nx] && !visited[ny, nx])
                                    {
                                        visited[ny, nx] = true;
                                        stack.Push((nx, ny));
                                    }
                                }
                        }
                        components.Add(comp);
                    }
            return components;
        }

        public string ClassifyComponent(List<(int x, int y)> component)
        {
            int minX = component.Min(p => p.x);
            int minY = component.Min(p => p.y);
            var normalized = new HashSet<(int, int)>(
                component.Select(p => (p.x - minX, p.y - minY))
            );
            var patterns = new Dictionary<string, HashSet<(int, int)>>()
            {
                { "Block", new HashSet<(int, int)> { (0,0), (1,0), (0,1), (1,1) } },
                { "Beehive", new HashSet<(int, int)> { (1,0),(2,0),(0,1),(3,1),(1,2),(2,2) } },
                { "Loaf", new HashSet<(int, int)> { (1,0),(2,0),(0,1),(3,1),(1,2),(3,2),(2,3) } },
                { "Boat", new HashSet<(int, int)> { (0,0),(1,0),(0,1),(2,1),(1,2) } },
                { "Tub", new HashSet<(int, int)> { (1,0),(0,1),(2,1),(1,2) } },
                { "Pond", new HashSet<(int, int)> { (1,0),(2,0),(0,1),(3,1),(0,2),(3,2),(1,3),(2,3) } }
            };
            foreach (var kv in patterns)
            {
                var basePattern = kv.Value;
                var transforms = GenerateTransformations(basePattern);
                if (transforms.Any(t => t.SetEquals(normalized)))
                    return kv.Key;
            }
            return "Unknown";
        }

        private static List<HashSet<(int, int)>> GenerateTransformations(HashSet<(int, int)> shape)
        {
            var transformations = new List<HashSet<(int, int)>>();
            for (int reflect = 0; reflect <= 1; reflect++)
            {
                for (int angle = 0; angle < 4; angle++)
                {
                    var transformed = new HashSet<(int, int)>();
                    foreach (var (x, y) in shape)
                    {
                        int nx = x, ny = y;
                        if (reflect == 1) nx = -nx;
                        switch (angle)
                        {
                            case 1: (nx, ny) = (-ny, nx); break;
                            case 2: (nx, ny) = (-nx, -ny); break;
                            case 3: (nx, ny) = (ny, -nx); break;
                        }
                        transformed.Add((nx, ny));
                    }
                    int minX = transformed.Min(p => p.Item1);
                    int minY = transformed.Min(p => p.Item2);
                    var norm = new HashSet<(int, int)>(
                        transformed.Select(p => (p.Item1 - minX, p.Item2 - minY))
                    );
                    transformations.Add(norm);
                }
            }
            return transformations;
        }

        public void SaveToFile(string path)
        {
            using var writer = new StreamWriter(path);
            writer.WriteLine($"{Width} {Height}");
            for (int y = 0; y < Height; y++)
            {
                var line = new char[Width];
                for (int x = 0; x < Width; x++)
                    line[x] = cells[y, x] ? 'O' : '.';
                writer.WriteLine(new string(line));
            }
        }

        public static Board LoadFromFile(string path)
        {
            using var reader = new StreamReader(path);
            var header = reader.ReadLine().Split(' ');
            int width = int.Parse(header[0]);
            int height = int.Parse(header[1]);
            var board = new Board(width, height);
            for (int y = 0; y < height; y++)
            {
                string line = reader.ReadLine();
                for (int x = 0; x < width && x < line.Length; x++)
                    board.SetCell(x, y, line[x] == 'O');
            }
            return board;
        }

        public int FindStableGeneration(int stablePeriod, int maxGenerations)
        {
            var popHistory = new Queue<int>();
            for (int gen = 0; gen < maxGenerations; gen++)
            {
                int pop = Population;
                popHistory.Enqueue(pop);
                if (popHistory.Count > stablePeriod)
                {
                    popHistory.Dequeue();
                    if (popHistory.Distinct().Count() == 1)
                        return gen - stablePeriod + 1;
                }
                NextGeneration();
            }
            return -1;
        }
    }

    class Program
    {
        static Settings settings;

        static void Main(string[] args)
        {
            Console.WriteLine("Исследование игры 'Жизнь'");
            LoadSettings();

            while (true)
            {
                Console.WriteLine("\nВыберите действие:");
                Console.WriteLine("1 – Смоделировать и сохранить/загрузить");
                Console.WriteLine("2 – Загрузить колонию и исследовать классификацию");
                Console.WriteLine("3 – Построить график перехода в стабильное состояние");
                Console.WriteLine("4 – Выход");
                Console.Write("> ");
                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1": RunSimulation(); break;
                    case "2": LoadAndClassify(); break;
                    case "3": BuildStabilityPlot(); break;
                    case "4": return;
                    default: Console.WriteLine("Неизвестная команда"); break;
                }
            }
        }

        static void LoadSettings()
        {
            string settingsPath = "settings.json";
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                settings = JsonSerializer.Deserialize<Settings>(json);
                Console.WriteLine("Настройки загружены.");
            }
            else
            {
                settings = new Settings();
                var defJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, defJson);
                Console.WriteLine($"Создан файл настроек {settingsPath}. Отредактируйте его и перезапустите.");
            }
        }

        static void RunSimulation()
        {
            Board board;
            Console.Write("Загрузить состояние из файла? (y/n): ");
            if (Console.ReadLine()?.ToLower() == "y")
            {
                Console.Write("Путь к файлу (по умолчанию из настроек): ");
                var path = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(path)) path = settings.LoadFile;
                if (File.Exists(path))
                {
                    board = Board.LoadFromFile(path);
                    Console.WriteLine($"Состояние загружено ({board.Width}x{board.Height})");
                }
                else
                {
                    Console.WriteLine("Файл не найден, создаю новую решётку.");
                    board = new Board(settings.Width, settings.Height, settings.Rule);
                    board.Randomize(settings.InitialDensity);
                }
            }
            else
            {
                board = new Board(settings.Width, settings.Height, settings.Rule);
                board.Randomize(settings.InitialDensity);
            }

            PrintBoard(board);
            for (int gen = 1; gen <= 10; gen++)
            {
                board.NextGeneration();
                Console.WriteLine($"Поколение {gen}, популяция: {board.Population}");
                if (gen % 5 == 0) PrintBoard(board);
            }

            Console.Write("Сохранить состояние? (y/n): ");
            if (Console.ReadLine()?.ToLower() == "y")
            {
                Console.Write("Путь к файлу: ");
                var path = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(path)) path = settings.SaveFile;
                board.SaveToFile(path);
                Console.WriteLine($"Сохранено в {path}");
            }
        }

        static void LoadAndClassify()
        {
            Console.Write("Введите путь к файлу с колонией: ");
            var path = Console.ReadLine();
            if (!File.Exists(path))
            {
                Console.WriteLine("Файл не найден.");
                return;
            }
            var board = Board.LoadFromFile(path);
            Console.WriteLine($"Колония загружена, размер {board.Width}x{board.Height}, популяция: {board.Population}");

            int stableGen = board.FindStableGeneration(settings.StablePeriod, settings.MaxGenerations);
            if (stableGen == -1)
                Console.WriteLine("Стабильная фаза не достигнута.");
            else
                Console.WriteLine($"Фаза стабилизации на поколении {stableGen}.");

            var components = board.FindConnectedComponents();
            Console.WriteLine($"Найдено компонент: {components.Count}, всего клеток: {board.Population}");
            var classification = new Dictionary<string, int>();
            foreach (var comp in components)
            {
                string type = board.ClassifyComponent(comp);
                if (!classification.ContainsKey(type)) classification[type] = 0;
                classification[type]++;
            }
            foreach (var kv in classification.OrderByDescending(kv => kv.Value))
                Console.WriteLine($"{kv.Key}: {kv.Value}");
        }

 static void BuildStabilityPlot()
{
    try
    {
        Console.WriteLine("Запуск исследования (может занять время)...");

        double[] densities = { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9 };
        int trialsPerDensity = 15;
        var avgGenerations = new List<double>();
        var dataLines = new List<string> { "Density\tAverageGenerations" };

        foreach (var density in densities)
        {
            int totalGens = 0, validTrials = 0;
            for (int t = 0; t < trialsPerDensity; t++)
            {
                var board = new Board(settings.Width, settings.Height, settings.Rule);
                board.Randomize(density);
                int gen = board.FindStableGeneration(settings.StablePeriod, settings.MaxGenerations);
                if (gen >= 0)
                {
                    totalGens += gen;
                    validTrials++;
                }
            }
            double avg = validTrials > 0 ? (double)totalGens / validTrials : settings.MaxGenerations;
            avgGenerations.Add(avg);
            dataLines.Add($"{density:F2}\t{avg:F1}");
            Console.WriteLine($"Плотность {density:F2}: среднее поколение = {avg:F1} (успешных {validTrials})");
        }

        // Надёжный способ: из bin/Debug/netX.X -> четыре раза вверх до корня решения
        string baseDir = AppContext.BaseDirectory; // ...\Life\bin\Debug\net8.0\
        string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        string dataDir = Path.Combine(projectRoot, "Data");
        Directory.CreateDirectory(dataDir);

        Console.WriteLine($"Папка сборки:        {baseDir}");
        Console.WriteLine($"Корень проекта:      {projectRoot}");
        Console.WriteLine($"Папка для сохранения: {dataDir}");

        string dataPath = Path.Combine(dataDir, "data.txt");
        File.WriteAllLines(dataPath, dataLines);
        Console.WriteLine($"data.txt сохранён в {dataPath}");

        string plotPath = Path.Combine(dataDir, "plot.png");
        GeneratePlot(densities, avgGenerations.ToArray(), plotPath);
        Console.WriteLine($"График plot.png сохранён в {plotPath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ОШИБКА: {ex.Message}");
    }
}
static void GeneratePlot(double[] xValues, double[] yValues, string filePath)
{
    try
    {
        if (xValues == null || yValues == null || xValues.Length == 0 || yValues.Length == 0)
        {
            Console.WriteLine("Нет данных для графика.");
            return;
        }

        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        int width = 800, height = 600;
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        if (surface == null)
        {
            Console.WriteLine("Не удалось создать поверхность SkiaSharp.");
            return;
        }

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        // Заголовок
        var titlePaint = new SKPaint { Color = SKColors.Black, TextSize = 26, IsAntialias = true, TextAlign = SKTextAlign.Center };
        canvas.DrawText("Game of Life: Stabilization", width / 2, 45, titlePaint);

        // Подписи осей
        var axisLabelPaint = new SKPaint { Color = SKColors.Black, TextSize = 18, IsAntialias = true, TextAlign = SKTextAlign.Center };
        canvas.DrawText("Density", width / 2, height - 20, axisLabelPaint);

        canvas.Save();
        canvas.RotateDegrees(-90);
        canvas.DrawText("Avg Generations to Stability", -height / 2, 30, axisLabelPaint);
        canvas.Restore();

        // Отступы (левый можно уменьшить, т.к. ось теперь от 0, а метка 0.1 сдвинута)
        float marginLeft = 80, marginRight = 40, marginTop = 80, marginBottom = 80;
        float plotLeft = marginLeft, plotRight = width - marginRight;
        float plotTop = marginTop, plotBottom = height - marginBottom;

        // ---------- ВАЖНОЕ ИЗМЕНЕНИЕ: начинаем ось X не с 0.1, а с 0.0 ----------
        float xAxisMin = 0.0f, xAxisMax = 0.9f; // теперь между осью Y и 0.1 появится отступ
        float xScale = (plotRight - plotLeft) / (xAxisMax - xAxisMin);

        // Границы по Y – автоматически с шагом 50
        float yMax = (float)yValues.Max();
        if (yMax == 0) yMax = 1;
        float yAxisMax = (float)(Math.Ceiling(yMax / 50.0) * 50);
        if (yAxisMax < 50) yAxisMax = 50;
        float yScale = (plotBottom - plotTop) / yAxisMax;

        // Оси
        var axisPaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };
        canvas.DrawLine(plotLeft, plotBottom, plotRight, plotBottom, axisPaint); // X
        canvas.DrawLine(plotLeft, plotTop, plotLeft, plotBottom, axisPaint);    // Y

        // Метки по X – теперь начинаем прорисовку с 0.1 (0.0 не рисуем)
        var tickPaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
        var labelTickPaint = new SKPaint { Color = SKColors.Black, TextSize = 16, IsAntialias = true, TextAlign = SKTextAlign.Center };

        for (double val = 0.1; val <= xAxisMax + 0.001; val += 0.1)
        {
            float x = plotLeft + (float)(val - xAxisMin) * xScale;
            canvas.DrawLine(x, plotBottom, x, plotBottom + 5, tickPaint);
            canvas.DrawText(val.ToString("F1"), x, plotBottom + 22, labelTickPaint);
        }

        // Метки по Y (шаг 50)
        var rightAlignPaint = new SKPaint { Color = SKColors.Black, TextSize = 16, IsAntialias = true, TextAlign = SKTextAlign.Right };
        for (float val = 0; val <= yAxisMax + 0.01; val += 50)
        {
            float y = plotBottom - val * yScale;
            canvas.DrawLine(plotLeft - 5, y, plotLeft, y, tickPaint);
            canvas.DrawText(val.ToString("F0"), plotLeft - 10, y + 6, rightAlignPaint);
        }

        // Линия графика
        var linePaint = new SKPaint { Color = SKColors.Blue, StrokeWidth = 3, IsAntialias = true, Style = SKPaintStyle.Stroke };
        for (int i = 0; i < xValues.Length - 1; i++)
        {
            float x1 = plotLeft + ((float)xValues[i] - xAxisMin) * xScale;
            float y1 = plotBottom - (float)yValues[i] * yScale;
            float x2 = plotLeft + ((float)xValues[i + 1] - xAxisMin) * xScale;
            float y2 = plotBottom - (float)yValues[i + 1] * yScale;
            if (x1 >= plotLeft && x1 <= plotRight && x2 >= plotLeft && x2 <= plotRight)
                canvas.DrawLine(x1, y1, x2, y2, linePaint);
        }

        // Точки (можно убрать)
        var pointPaint = new SKPaint { Color = SKColors.Red, IsAntialias = true, Style = SKPaintStyle.Fill };
        for (int i = 0; i < xValues.Length; i++)
        {
            float x = plotLeft + ((float)xValues[i] - xAxisMin) * xScale;
            float y = plotBottom - (float)yValues[i] * yScale;
            if (x >= plotLeft && x <= plotRight)
                canvas.DrawCircle(x, y, 4, pointPaint);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);
        Console.WriteLine("График успешно создан.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка при создании графика: {ex.Message}");
    }
}

        static void PrintBoard(Board board, int maxWidth = 40, int maxHeight = 20)
        {
            int w = Math.Min(board.Width, maxWidth);
            int h = Math.Min(board.Height, maxHeight);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                    Console.Write(board[x, y] ? 'O' : '.');
                Console.WriteLine();
            }
        }
    }
}