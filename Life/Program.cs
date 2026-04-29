using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ScottPlot;

namespace Life
{
    public class Cell
    {
        public bool IsAlive { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public Cell(int x, int y, bool alive = false)
        {
            X = x;
            Y = y;
            IsAlive = alive;
        }
    }
    public class Board
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        private bool[,] grid;

        public Board(int width, int height)
        {
            Width = width;
            Height = height;
            grid = new bool[width, height];
        }

        public void Clear() => Array.Clear(grid, 0, grid.Length);
        public bool IsAlive(int x, int y) => grid[x, y];
        public void SetCell(int x, int y, bool alive) => grid[x, y] = alive;
        public void Randomize(double density, Random rand = null)
        {
            if (rand == null) rand = new Random();
            for (int i = 0; i < Width; i++)
                for (int j = 0; j < Height; j++)
                    grid[i, j] = rand.NextDouble() < density;
        }
        public int GetNeighborsCount(int x, int y)
        {
            int count = 0;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && nx < Width && ny >= 0 && ny < Height && grid[nx, ny])
                        count++;
                }
            return count;
        }
        public void NextGeneration()
        {
            var newGrid = new bool[Width, Height];
            for (int i = 0; i < Width; i++)
                for (int j = 0; j < Height; j++)
                {
                    int neighbors = GetNeighborsCount(i, j);
                    bool alive = grid[i, j];
                    if (alive)
                        newGrid[i, j] = (neighbors == 2 || neighbors == 3);
                    else
                        newGrid[i, j] = (neighbors == 3);
                }
            grid = newGrid;
        }
        public int GetAliveCount()
        {
            int count = 0;
            for (int i = 0; i < Width; i++)
                for (int j = 0; j < Height; j++)
                    if (grid[i, j]) count++;
            return count;
        }
        public List<(int x, int y)> GetAliveCells()
        {
            var list = new List<(int, int)>();
            for (int i = 0; i < Width; i++)
                for (int j = 0; j < Height; j++)
                    if (grid[i, j]) list.Add((i, j));
            return list;
        }
        public void SaveToFile(string filename)
        {
            var cells = GetAliveCells();
            using var writer = new StreamWriter(filename);
            writer.WriteLine($"{Width} {Height}");
            foreach (var (x, y) in cells)
                writer.WriteLine($"{x} {y}");
        }
        public void LoadFromFile(string filename)
        {
            if (!File.Exists(filename)) throw new FileNotFoundException();
            var lines = File.ReadAllLines(filename);
            var dims = lines[0].Split();
            int w = int.Parse(dims[0]), h = int.Parse(dims[1]);
            if (w != Width || h != Height) throw new InvalidOperationException("Размер поля не совпадает");
            Clear();
            for (int i = 1; i < lines.Length; i++)
            {
                var coords = lines[i].Split();
                int x = int.Parse(coords[0]), y = int.Parse(coords[1]);
                if (x >= 0 && x < Width && y >= 0 && y < Height)
                    grid[x, y] = true;
            }
        }
        public List<List<(int x, int y)>> FindClusters()
        {
            var visited = new bool[Width, Height];
            var clusters = new List<List<(int, int)>>();
            for (int i = 0; i < Width; i++)
                for (int j = 0; j < Height; j++)
                {
                    if (grid[i, j] && !visited[i, j])
                    {
                        var cluster = new List<(int, int)>();
                        var queue = new Queue<(int, int)>();
                        queue.Enqueue((i, j));
                        visited[i, j] = true;
                        while (queue.Count > 0)
                        {
                            var (x, y) = queue.Dequeue();
                            cluster.Add((x, y));
                            for (int dx = -1; dx <= 1; dx++)
                                for (int dy = -1; dy <= 1; dy++)
                                {
                                    if (dx == 0 && dy == 0) continue;
                                    int nx = x + dx, ny = y + dy;
                                    if (nx >= 0 && nx < Width && ny >= 0 && ny < Height &&
                                        grid[nx, ny] && !visited[nx, ny])
                                    {
                                        visited[nx, ny] = true;
                                        queue.Enqueue((nx, ny));
                                    }
                                }
                        }
                        clusters.Add(cluster);
                    }
                }
            return clusters;
        }
        public static string ClassifyCluster(List<(int x, int y)> cluster)
        {
            if (cluster.Count == 0) return "Empty";
            // Нормализация: сдвиг к (0,0)
            int minX = cluster.Min(p => p.x);
            int minY = cluster.Min(p => p.y);
            var normalized = cluster.Select(p => (p.x - minX, p.y - minY)).OrderBy(p => p.Item1).ThenBy(p => p.Item2).ToList();

            // Эталоны (относительные координаты)
            var block = new HashSet<(int, int)> { (0,0),(0,1),(1,0),(1,1) };
            var beehive = new HashSet<(int, int)> { (0,1),(0,2),(1,0),(1,3),(2,1),(2,2) };
            var loaf = new HashSet<(int, int)> { (0,1),(0,2),(1,0),(1,3),(2,1),(2,3),(3,2) };
            var blinkerH = new HashSet<(int, int)> { (0,0),(0,1),(0,2) };
            var blinkerV = new HashSet<(int, int)> { (0,0),(1,0),(2,0) };
            var glider = new HashSet<(int, int)> { (0,1),(1,2),(2,0),(2,1),(2,2) };
            var boat = new HashSet<(int, int)> { (0,0),(0,1),(1,0),(1,2),(2,1) };

            var set = new HashSet<(int, int)>(normalized);
            if (set.SetEquals(block)) return "Block";
            if (set.SetEquals(beehive)) return "Beehive";
            if (set.SetEquals(loaf)) return "Loaf";
            if (set.SetEquals(blinkerH) || set.SetEquals(blinkerV)) return "Blinker";
            if (set.SetEquals(glider)) return "Glider";
            if (set.SetEquals(boat)) return "Boat";
            return "Unknown";
        }
        public (int generations, bool stabilized) SimulateUntilStable(int maxGenerations, int stableThreshold)
        {
            int lastCount = GetAliveCount();
            int stableCounter = 0;
            for (int gen = 0; gen < maxGenerations; gen++)
            {
                NextGeneration();
                int newCount = GetAliveCount();
                if (newCount == lastCount)
                    stableCounter++;
                else
                    stableCounter = 0;
                lastCount = newCount;
                if (stableCounter >= stableThreshold)
                    return (gen + 1, true);
            }
            return (maxGenerations, false);
        }
    }
    public class Program
    {
        public class Config
        {
            public int BoardWidth { get; set; } = 100;
            public int BoardHeight { get; set; } = 100;
            public int SimulationDelayMs { get; set; } = 100;
            public int StableThreshold { get; set; } = 10;
            public int MaxGenerationsForStability { get; set; } = 1000;
            public int ExperimentRepetitions { get; set; } = 50;
            public double DensityStep { get; set; } = 0.1;
            public string DataFolder { get; set; } = "Data";
        }

        private static Config config;
        private static Board board;

        static void Main(string[] args)
        {
            LoadConfig();
            Directory.CreateDirectory(config.DataFolder);

            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "run":
                        if (args.Length > 1) RunSimulation(args[1]);
                        else RunSimulation(null);
                        break;
                    case "experiment":
                        RunExperiment();
                        break;
                    default:
                        ShowMenu();
                        break;
                }
            }
            else
                ShowMenu();
        }

        static void LoadConfig()
        {
            string configFile = "config.json";
            if (File.Exists(configFile))
            {
                string json = File.ReadAllText(configFile);
                config = JsonSerializer.Deserialize<Config>(json);
            }
            else
            {
                config = new Config();
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configFile, json);
                Console.WriteLine("Создан файл конфигурации по умолчанию.");
            }
        }

        static void ShowMenu()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== Игра Жизнь ===");
                Console.WriteLine("1. Запустить симуляцию со случайным полем");
                Console.WriteLine("2. Загрузить фигуру из файла");
                Console.WriteLine("3. Сохранить текущее состояние");
                Console.WriteLine("4. Провести эксперимент по стабилизации (график)");
                Console.WriteLine("5. Классификация фигур на поле (кластеры)");
                Console.WriteLine("6. Выход");
                Console.Write("Выберите пункт: ");
                string choice = Console.ReadLine();
                switch (choice)
                {
                    case "1":
                        RunSimulation(null);
                        break;
                    case "2":
                        LoadFigure();
                        break;
                    case "3":
                        SaveCurrentState();
                        break;
                    case "4":
                        RunExperiment();
                        break;
                    case "5":
                        ClassifyField();
                        break;
                    case "6":
                        return;
                    default:
                        Console.WriteLine("Неверный ввод. Нажмите любую клавишу...");
                        Console.ReadKey();
                        break;
                }
            }
        }

        static void RunSimulation(string stateFile)
        {
            board = new Board(config.BoardWidth, config.BoardHeight);
            if (!string.IsNullOrEmpty(stateFile) && File.Exists(stateFile))
                board.LoadFromFile(stateFile);
            else
                board.Randomize(0.3);
            int generation = 0;
            while (true)
            {
                Console.Clear();
                DrawBoard();
                Console.WriteLine($"Поколение: {generation} | Живых клеток: {board.GetAliveCount()}");
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape) break;
                if (key.Key == ConsoleKey.Space)
                {
                    board.NextGeneration();
                    generation++;
                }
                else if (key.Key == ConsoleKey.S)
                {
                    SaveCurrentState();
                }
            }
        }

        static void DrawBoard()
        {
            int maxDisplayHeight = Math.Min(config.BoardHeight, 40);
            int maxDisplayWidth = Math.Min(config.BoardWidth, 120);
            for (int j = 0; j < maxDisplayHeight; j++)
            {
                for (int i = 0; i < maxDisplayWidth; i++)
                    Console.Write(board.IsAlive(i, j) ? "█" : " ");
                Console.WriteLine();
            }
        }

        static void LoadFigure()
        {
            Console.Write("Введите имя файла (в папке Data): ");
            string filename = Console.ReadLine();
            string fullPath = Path.Combine(config.DataFolder, filename);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine("Файл не найден!");
                Console.ReadKey();
                return;
            }
            board = new Board(config.BoardWidth, config.BoardHeight);
            board.LoadFromFile(fullPath);
            Console.WriteLine("Фигура загружена. Нажмите любую клавишу для запуска симуляции...");
            Console.ReadKey();
            RunSimulation(fullPath);
        }

        static void SaveCurrentState()
        {
            if (board == null)
            {
                Console.WriteLine("Нет активного поля.");
                Console.ReadKey();
                return;
            }
            Console.Write("Имя файла для сохранения: ");
            string filename = Console.ReadLine();
            string fullPath = Path.Combine(config.DataFolder, filename);
            board.SaveToFile(fullPath);
            Console.WriteLine($"Сохранено в {fullPath}");
            Console.ReadKey();
        }

        static void ClassifyField()
        {
            if (board == null)
            {
                Console.WriteLine("Сначала запустите симуляцию или загрузите фигуру.");
                Console.ReadKey();
                return;
            }
            var clusters = board.FindClusters();
            Console.Clear();
            Console.WriteLine($"Всего живых клеток: {board.GetAliveCount()}");
            Console.WriteLine($"Всего кластеров (комбинаций): {clusters.Count}");
            var classification = new Dictionary<string, int>();
            foreach (var cluster in clusters)
            {
                string type = Board.ClassifyCluster(cluster);
                if (!classification.ContainsKey(type))
                    classification[type] = 0;
                classification[type]++;
            }
            Console.WriteLine("Классификация:");
            foreach (var kv in classification.OrderBy(k => k.Key))
                Console.WriteLine($"  {kv.Key}: {kv.Value}");
            Console.WriteLine("\nНажмите любую клавишу для возврата...");
            Console.ReadKey();
        }

        static void RunExperiment()
        {
            Console.WriteLine("Запуск эксперимента по стабилизации...");
            double minDensity = 0.05;
            double maxDensity = 0.95;
            double step = config.DensityStep;
            int repetitions = config.ExperimentRepetitions;
            var results = new List<(double density, double avgGenerations, double stdDev, int successCount)>();

            var rand = new Random(42);
            for (double density = minDensity; density <= maxDensity + 1e-6; density += step)
            {
                List<int> stableGens = new List<int>();
                for (int rep = 0; rep < repetitions; rep++)
                {
                    board = new Board(config.BoardWidth, config.BoardHeight);
                    board.Randomize(density, rand);
                    var (gens, stabilized) = board.SimulateUntilStable(config.MaxGenerationsForStability, config.StableThreshold);
                    if (stabilized)
                        stableGens.Add(gens);
                }
                double avg = stableGens.Count > 0 ? stableGens.Average() : 0;
                double std = stableGens.Count > 1 ? Math.Sqrt(stableGens.Select(v => (v - avg) * (v - avg)).Sum() / (stableGens.Count - 1)) : 0;
                results.Add((density, avg, std, stableGens.Count));
                Console.WriteLine($"Плотность {density:F2}: успешных {stableGens.Count}/{repetitions}, среднее поколений = {avg:F2}");
            }

            // Сохраняем данные в data.txt
            string dataPath = Path.Combine(config.DataFolder, "data.txt");
            using (var writer = new StreamWriter(dataPath))
            {
                writer.WriteLine("density\tavg_generations\tstd_dev\tsuccess_count");
                foreach (var r in results)
                    writer.WriteLine($"{r.density:F3}\t{r.avgGenerations:F2}\t{r.stdDev:F2}\t{r.successCount}");
            }
            Console.WriteLine($"Данные сохранены в {dataPath}");
            var plt = new Plot();
            double[] xs = results.Select(r => r.density).ToArray();
            double[] ys = results.Select(r => r.avgGenerations).ToArray();
            plt.Add.Scatter(xs, ys);
            plt.Title("Переход в стабильную фазу");
            plt.XLabel("Плотность заполнения");
            plt.YLabel("Среднее число поколений до стабилизации");
            string plotPath = Path.Combine(config.DataFolder, "plot.png");
            plt.SavePng(plotPath, 800, 600);
            Console.WriteLine($"График сохранён в {plotPath}");
            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadKey();
        }
    }
}