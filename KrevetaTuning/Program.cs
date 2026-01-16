//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// ReSharper disable InconsistentNaming
// ReSharper disable SpecifyACultureInStringConversionExplicitly
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace KrevetaTuning;

internal static class Program {
    private const string PositionsPath = "positions.txt";
    private const string DatasetPath   = "dataset.txt";
    private const string OutputPath    = "output.txt";
    private const string StockfishPath = @"C:\Users\michn\Downloads\stockfish-windows-x86-64-avx2\stockfish\stockfish-windows-x86-64-avx2.exe";
    private const string KrevetaPath   = @"C:\Users\michn\Desktop\Kreveta\Kreveta\Kreveta\bin\Release\net10.0\Kreveta.exe";
    
    private const int Depth = 8;
    
    // limit how many "new engines" to create/test
    private const int Cycles = 3_000_000;

    private const int ShiftsPerEval = 2;

    private static float _krevetaBaseMAE;
    private static float _krevetaBaseMoveAccuracy;
    
    // stores (shift_sum, number_of_shifts)
    private static readonly (float, int)[] Tweaks 
        = new (float, int)[ParamGenerator.ParamCount];

    private const           int                     MaxThreads    = 18;
    private static readonly CancellationTokenSource Cts           = new();
    private static readonly Lock                    EnginesLock   = new();
    private static readonly List<UCIEngine>         ActiveEngines = [];
    
    internal enum EvalMode { FullSearch, StaticEval }
    private const EvalMode _mode = EvalMode.FullSearch;
    
    internal static void Main() {
        Console.CancelKeyPress += (_, e) => {
            e.Cancel = true;
            Cts.Cancel();
        };

        try {
            MainWrapperAsync(Cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) {
            Console.WriteLine("Tuning canceled by user.");
        }
        finally {
            CleanupEngines();
            Console.WriteLine("All engines closed. Exiting...");
        }
    }

    private static async Task MainWrapperAsync(CancellationToken token) {
        var sw = Stopwatch.StartNew();

        if (!File.Exists(DatasetPath))
            GenerateStockfishOutputs();
        GenerateKrevetaBaseOutputs();
        
        ReadExistingOutput();

        var semaphore = new SemaphoreSlim(MaxThreads);
        var tasks = new List<Task>();

        for (int i = 0; i < Cycles; i++) {
            token.ThrowIfCancellationRequested();
            await semaphore.WaitAsync(token);

            int cycleNum = i;
            var task = Task.Run(() => {
                try {
                    EvaluateKrevetaThread(cycleNum, token);
                }
                catch (OperationCanceledException) {
                    // task will end gracefully
                }
                finally {
                    semaphore.Release();
                }
            }, token);

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        sw.Stop();
        Console.WriteLine($"Tuning finished in {sw.Elapsed}");
    }

    private static void GenerateStockfishOutputs() {
        Console.WriteLine("Generating Stockfish outputs...");
        var outputLines = new List<string>();

        int counter = 0;
        foreach (var fen in File.ReadLines(PositionsPath)) {
            if (string.IsNullOrWhiteSpace(fen) || fen.StartsWith('#'))
                continue;
            
            var stockfish = new UCIEngine(StockfishPath);
            
            // Stockfish gets more time to evaluate the positions,
            // so that the tweaks based on its outputs are precise
            (int eval, string move) = stockfish.EvaluateFEN(fen, 20, EvalMode.FullSearch);
            eval = Math.Clamp(eval, -1000, 1000);
            
            stockfish.Quit();
            
            outputLines.Add($"{fen};{eval};{move}");
            Console.Write($"Finished: {++counter}/?\r");
        }
        
        File.WriteAllLines(DatasetPath, outputLines);
        Console.WriteLine("\nDone generating Stockfish outputs.");
    }

    private static void GenerateKrevetaBaseOutputs() {
        Console.WriteLine("Evaluating Kreveta base...");
        var kreveta = new UCIEngine(KrevetaPath);
        (float MAE, float MoveAccuracy) result = EvaluateKreveta(kreveta, CancellationToken.None);
        
        _krevetaBaseMAE          = result.MAE;
        _krevetaBaseMoveAccuracy = result.MoveAccuracy;
        
        kreveta.Quit();
        Console.WriteLine($"\nDone evaluating Kreveta base:\nMSE = {_krevetaBaseMAE}, MoveAccuracy = {_krevetaBaseMoveAccuracy}%\n");
    }

    private static void EvaluateKrevetaThread(int num, CancellationToken token) {
        token.ThrowIfCancellationRequested();

        var newKreveta = new UCIEngine(KrevetaPath);

        lock (EnginesLock)
            ActiveEngines.Add(newKreveta);

        try {
            var paramCMD = ParamGenerator.CreateCMD(ShiftsPerEval);
            Console.WriteLine($"Evaluating a new Kreveta ({num + 1}/{Cycles}) with params:\n{paramCMD}\n");

            newKreveta.Send(paramCMD);
            (float MAE, float MoveAccuracy) result = EvaluateKreveta(newKreveta, token);

            Console.WriteLine($"Done evaluating Kreveta {num + 1}/{Cycles}:\nMAE = {result.MAE}, MoveAccuracy = {result.MoveAccuracy}%\n"
                            + $"Is it better: unsure\n");

            UpdateTweaks(paramCMD, result.MAE, result.MoveAccuracy);
            StoreResult();
        }
        finally {
            newKreveta.Quit();

            lock (EnginesLock)
                ActiveEngines.Remove(newKreveta);
        }
    }

    private static (float MAE, float MoveAccuracy) EvaluateKreveta(UCIEngine kreveta, CancellationToken token) {
        double totalError = 0;
        int    matchMoves = 0;
        int    count      = 0;

        foreach (var line in File.ReadLines(DatasetPath)) {
            token.ThrowIfCancellationRequested();

            var parts = line.Split(';');
            
            string fen           = parts[0];
            int    stockfishEval = int.Parse(parts[1]);
            string stockfishMove = parts[2];

            (int eval, string move) = kreveta.EvaluateFEN(fen, Depth, _mode);
            
            double diff = eval - stockfishEval;
            totalError += Math.Abs(diff);
            
            if (move == stockfishMove) 
                matchMoves++;

            count++;
        }

        float mae          = (float)(totalError / count);
        float moveAccuracy = 100.0f * matchMoves / count;
        
        return (mae, moveAccuracy);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private static void UpdateTweaks(string paramCMD, float MAE, float moveAccuracy) {
        int[] shifts = paramCMD.Split(' ')[1..(Tweaks.Length + 1)]
            .Select(int.Parse).ToArray();
        
        // if in static eval mode, don't measure move accuracy
        bool areMovesMoreAccurate = _mode != EvalMode.StaticEval 
                                    && moveAccuracy > _krevetaBaseMoveAccuracy;
        
        bool areMovesSame = _mode == EvalMode.StaticEval 
                                    || moveAccuracy >= _krevetaBaseMoveAccuracy;

        // this version seems to be better
        if ((areMovesSame && MAE < _krevetaBaseMAE) || areMovesMoreAccurate) {
            // update only when this version is better
            for (int i = 0; i < shifts.Length; i++) {
                if (shifts[i] == 0)
                    continue;
            
                Tweaks[i].Item1 += shifts[i];
                Tweaks[i].Item2++;
            }
        }
    }

    private static void ReadExistingOutput() {
        if (File.Exists(OutputPath)) {
            var lines = File.ReadAllLines(OutputPath);

            lock (Tweaks) {
                for (int i = 0; i < Tweaks.Length; i++) {
                    var toks = lines[i].Split(' ');
                
                    Tweaks[i].Item1 = float.Parse(toks[0]);
                    Tweaks[i].Item2 = int.Parse(toks[1]);
                }
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.Synchronized)]
    private static void StoreResult() {
        var newLines = new List<string>();
        
        for (int i = 0; i < Tweaks.Length; i++) {
            var sum = Tweaks[i].Item1;
            var cnt = Tweaks[i].Item2;
            
            float meanShift = cnt == 0 ? 0 : sum / cnt;
            newLines.Add($"{sum} {cnt} mean_shift: {meanShift}");
        }
        
        File.WriteAllLines(OutputPath, newLines);
    }
    
    private static void CleanupEngines() {
        lock (EnginesLock) {
            foreach (UCIEngine engine in ActiveEngines.ToArray()) {
                try {
                    engine.Quit();
                }
                catch { /* ignore */ }
            }
            ActiveEngines.Clear();
        }
    }
}