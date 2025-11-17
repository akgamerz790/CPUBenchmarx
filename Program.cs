using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class CoreOSBenchmark
{
    static List<byte[]> memoryHog = new List<byte[]>();
    static long totalMemoryAllocatedMB = 0;
    static object lockObj = new object();
    
    static long cpuOperations = 0;
    static long memoryOperations = 0;
    static DateTime startTime;

    static void Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     CoreOS BenchMarX Performance Index - v1.0                          ║");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("║         \"Checks How much Process Power ya COmputer can handle\"         ║");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("[!]  Cores Available: ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"{Environment.ProcessorCount}");
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("[!]  Memory Available: ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024)} MB");
        Console.ResetColor();
        Console.WriteLine();
        
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("[$] Running HEAVY stress test for 30 seconds...\n");
        Console.ResetColor();

        CancellationTokenSource cts = new CancellationTokenSource();
        startTime = DateTime.Now;

        Task cpuTask = Task.Run(() =>
        {
            Parallel.For(0, Environment.ProcessorCount, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            i => CPUStressTest(i, cts.Token));
        });

        Task memoryTask = Task.Run(() => MemoryStressTest(cts.Token));
        Task monitorTask = Task.Run(() => MonitorResources(cts.Token));

        Thread.Sleep(30000);
        cts.Cancel();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n\n[%] Calculating performance score...\n");
        Console.ResetColor();
        
        Task.WaitAll(new[] { cpuTask, memoryTask, monitorTask }, 5000);

        CalculatePerformanceIndex();

        memoryHog.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("\n\n[+] Press any key to exit...");
        Console.ResetColor();
        Console.ReadKey();
    }

    static void CPUStressTest(int threadId, CancellationToken token)
    {
        long localOps = 0;
        int value = 12345;
        double mathResult = 0;

        while (!token.IsCancellationRequested)
        {
            // Heavy integer operations (10x more)
            for (int i = 0; i < 100000; i++)
            {
                value = (value * 3 / 2) + ((value * value) % 70) + 1;
                value = value ^ (value << 3);
                value = ~value;
                value = (value << 2) | (value >> 2);
                value = value * 997 + 1337;
                localOps++;
            }

            // Heavy floating point operations (20x more)
            for (int i = 0; i < 20000; i++)
            {
                mathResult += Math.Sqrt(value + i);
                mathResult += Math.Pow((value % 100) + i, 3.5);
                mathResult += Math.Sin(value + i) * Math.Cos(value - i);
                mathResult += Math.Log(Math.Abs(value + i) + 1);
                mathResult += Math.Exp((value % 10) / 10.0);
                mathResult += Math.Tan(value * 0.001 + i);
                localOps++;
            }

            // Heavy algorithm tests (larger arrays, multiple sorts)
            for (int j = 0; j < 10; j++)
            {
                int[] arr = new int[1000];
                for (int i = 0; i < 1000; i++) arr[i] = (value * i) % 10000;
                Array.Sort(arr);
                Array.Reverse(arr);
                localOps += 1000;
            }

            // Matrix multiplication simulation
            for (int i = 0; i < 100; i++)
            {
                double sum = 0;
                for (int j = 0; j < 100; j++)
                {
                    // sum += (i * j * value) % 1000;
                    sum += i * j * value % 1000;
                }
                mathResult += sum;
                localOps += 100;
            }

            Interlocked.Add(ref cpuOperations, localOps);
            localOps = 0;
        }
    }

    static void MemoryStressTest(CancellationToken token)
    {
        int chunkSizeMB = 100; // Initial test value = [50]
        long localMemOps = 0;
        
        while (!token.IsCancellationRequested)
        {
            try
            {
                byte[] chunk = new byte[chunkSizeMB * 1024 * 1024];
                Random rnd = new Random();
                
                // Heavy sequential write (every 1KB instead of 4KB)
                //Heavy load func
                for (int i = 0; i < chunk.Length; i += 1024)
                {
                    chunk[i] = (byte)(i % 256);
                    localMemOps++;
                }
                
                // Heavy random access (10x more operations)
                for (int i = 0; i < 100000; i++)
                {
                    int pos = rnd.Next(chunk.Length);
                    byte val = chunk[pos];
                    chunk[pos] = (byte)(val + 1);
                    localMemOps += 2;
                }

                // Pattern writes
                for (int i = 0; i < chunk.Length; i += 512)
                {
                    chunk[i] = (byte)(i ^ 0xFF);
                    localMemOps++;
                }

                lock (lockObj)
                {
                    memoryHog.Add(chunk);
                    totalMemoryAllocatedMB += chunkSizeMB;
                    memoryOperations += localMemOps;
                }

                localMemOps = 0;
                Thread.Sleep(150); // Faster allocation
            }
            catch (OutOfMemoryException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n [!] Hit memory ceiling - maxed out");
                Console.ResetColor();
                break;
            }
        }
    }

    static void MonitorResources(CancellationToken token)
    {
        Process currentProcess = Process.GetCurrentProcess();
        int counter = 0;

        while (!token.IsCancellationRequested)
        {
            Thread.Sleep(2000);
            counter += 2;

            long memoryUsedMB = currentProcess.WorkingSet64 / (1024 * 1024);
            
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write($"[#]  [{counter}s] ");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("RAM: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{memoryUsedMB} MB");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write(" | CPU Ops: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{cpuOperations / 1000000}M");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write(" | Mem Ops: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"{memoryOperations / 1000000}M");
            Console.ResetColor();
        }
    }

    static void CalculatePerformanceIndex()
    {
        double elapsedSeconds = (DateTime.Now - startTime).TotalSeconds;

        // CPU Score (wider range with multiplier)
        long cpuOpsPerSec = cpuOperations / (long)elapsedSeconds;
        int cpuScore = (int)(cpuOpsPerSec / 1000); // More sensitive scaling
        int coreBonus = Environment.ProcessorCount * 2000; // Higher bonus
        cpuScore += coreBonus;

        // Memory Score (wider range)
        long memOpsPerSec = memoryOperations / (long)elapsedSeconds;
        int ramScore = (int)(memOpsPerSec / 500) + (int)(totalMemoryAllocatedMB * 50); // Higher multipliers

        // GPU Score (placeholder)
        int gpuScore = 0;
        
        // Overall Performance Index (wider range)
        int performanceIndex = (int)(cpuScore * 0.45 + ramScore * 0.35 + gpuScore * 0.20);

        // Display results with colors
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔═════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       CoreOS CPU BenchMarX Performance Index Report         ║");
        Console.WriteLine("╠═════════════════════════════════════════════════════════════╣");
        Console.ResetColor();
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("║ CPU Score:             ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{cpuScore,15:N0}");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("      ║");
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("║ RAM Score:             ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{ramScore,15:N0}");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("   ║");
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("║ GPU Score:             ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{gpuScore,15:N0}");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("      ║");
        
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╠═══════════════════════════════════════════════╣");
        
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write("║ PERFORMANCE INDEX:     ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.BackgroundColor = ConsoleColor.DarkMagenta;
        Console.Write($"{performanceIndex,15:N0}");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("      ║");
        Console.WriteLine("╚═══════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        // Performance tier with color
        string tier = GetPerformanceTier(performanceIndex);
        ConsoleColor tierColor = GetTierColor(performanceIndex);
        
        Console.ForegroundColor = tierColor;
        Console.WriteLine($"[!] System Tier: {tier}");
        Console.ResetColor();
        Console.WriteLine();

        // Detailed stats
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("════ Performance Breakdown ════");
        Console.ResetColor();
        
        PrintStatLine("Total CPU Operations:", $"{cpuOperations:N0}");
        PrintStatLine("CPU Ops/Second:", $"{cpuOperations / (long)elapsedSeconds:N0}");
        PrintStatLine("Total Memory Operations:", $"{memoryOperations:N0}");
        PrintStatLine("Memory Allocated:", $"{totalMemoryAllocatedMB:N0} MB");
        PrintStatLine("Memory Ops/Second:", $"{memoryOperations / (long)elapsedSeconds:N0}");
        PrintStatLine("Test Duration:", $"{elapsedSeconds:F2} seconds");
        PrintStatLine("Cores Tested:", $"{Environment.ProcessorCount}");
    }

    static void PrintStatLine(string label, string value)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write(label.PadRight(30));
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(value);
        Console.ResetColor();
    }

    static string GetPerformanceTier(int score)
    {
        if (score >= 999999999) return "#X WTF Bro's using NASA PC ";
        if (score >= 500000) return "SS-Tier (Legendary - Workstation Beast)";
        if (score >= 350000) return "S-Tier (Insane - Desktop Replacement HX)";
        if (score >= 250000) return "A+ Tier (Excellent - High-End Gaming)";
        if (score >= 180000) return "A-Tier (Very Good - Gaming/Creator)";
        if (score >= 120000) return "B+ Tier (Good - Performance H-Series)";
        if (score >= 80000) return "B-Tier (Solid - Mid-Range)";
        if (score >= 50000) return "C+ Tier (Decent - Entry Gaming)";
        if (score >= 30000) return "C-Tier (Fair - U-Series/Ultrabook)";
        if (score >= 15000) return "D-Tier (Low - Basic Computing)";
        if (score >= 100) return "Z-Tier  Tf are you using? ";
        // return "Are you sure u using a 'Computer'";
        return "F-Tier (Very Low - Needs Upgrade)";
    }

    static ConsoleColor GetTierColor(int score)
    {
        if (score >= 500000) return ConsoleColor.Magenta;  // SS-Tier
        if (score >= 350000) return ConsoleColor.Red;      // S-Tier
        if (score >= 250000) return ConsoleColor.DarkRed;  // A+ Tier
        if (score >= 180000) return ConsoleColor.Yellow;   // A-Tier
        if (score >= 120000) return ConsoleColor.DarkYellow; // B+ Tier
        if (score >= 80000) return ConsoleColor.Green;     // B-Tier
        if (score >= 50000) return ConsoleColor.DarkGreen; // C+ Tier
        if (score >= 30000) return ConsoleColor.Cyan;      // C-Tier
        if (score >= 15000) return ConsoleColor.Blue;      // D-Tier
        return ConsoleColor.DarkGray;                      // F-Tier
    }
}
