//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// ReSharper disable InconsistentNaming
// ReSharper disable SpecifyACultureInStringConversionExplicitly
using System.Diagnostics;

namespace KrevetaTuning;

internal static class Program {
    private const string PositionsPath = "positions.txt";
    private const string DatasetPath   = "dataset.txt";
    private const string OutputPath    = "output.txt";
    private const string StockfishPath = @"C:\Users\michn\Downloads\stockfish-windows-x86-64-avx2\stockfish\stockfish-windows-x86-64-avx2.exe";
    private const string KrevetaPath   = @"C:\Users\michn\Desktop\Kreveta\Kreveta\Kreveta\bin\Release\net9.0\Kreveta.exe";

    // time for evaluation of each single position in ms
    private const int MoveTime = 500;
    
    // how many "new engines" to create/test
    private const  int Cycles = 25;
    
    private static int PositionCount;

    private static float _krevetaBaseMSE;
    private static float _krevetaBaseMoveAccuracy;
    private static float _krevetaBaseTotal;
    
    // stores (shift_sum, number_of_shifts)
    private static readonly (float, int)[] Tweaks 
        = new (float, int)[ParamGenerator.ParamCount];
    
    internal static void Main() {
        var sw = Stopwatch.StartNew();
        
        GenerateStockfishOutputs();
        GenerateKrevetaBaseOutputs();

        for (int i = 0; i < Cycles; i++) {
            var newKreveta = new UCIEngine(KrevetaPath);
            var paramCMD = ParamGenerator.CreateCMD();
            
            Console.WriteLine($"Evaluating a new Kreveta ({i + 1}/{Cycles}) with params:\n{paramCMD}");
            newKreveta.Send(paramCMD);

            (float MSE, float MoveAccuracy) result = EvaluateKreveta(newKreveta);
            float total = result.MSE + 80 * (100 - result.MoveAccuracy);
            
            newKreveta.Quit();
            Console.WriteLine($"Done evaluating another Kreveta:\nMSE = {result.MSE}, MoveAccuracy = {result.MoveAccuracy}%");
            Console.WriteLine($"Is it better: {total < _krevetaBaseTotal}\n");

            UpdateTweaks(paramCMD, total);
        }

        StoreResult();
        
        sw.Stop();
        Console.WriteLine($"Selfplay finished in {sw.Elapsed}");
    }

    private static void GenerateStockfishOutputs() {
        Console.WriteLine("Generating Stockfish outputs...");
        var stockfish   = new UCIEngine(StockfishPath);
        var outputLines = new List<string>();

        int counter = 0;
        foreach (var fen in File.ReadLines(PositionsPath)) {
            if (string.IsNullOrWhiteSpace(fen) || fen.StartsWith('#'))
                continue;
            
            (int eval, string move) = stockfish.EvaluateFEN(fen, MoveTime);
            outputLines.Add($"{fen};{eval};{move}");
            
            Console.Write($"Finished: {++counter}/?\r");
        }

        PositionCount = counter;
        
        File.WriteAllLines(DatasetPath, outputLines);
        stockfish.Quit();
        Console.WriteLine("\nDone generating Stockfish outputs.");
    }

    private static void GenerateKrevetaBaseOutputs() {
        Console.WriteLine("Evaluating Kreveta base...");
        var kreveta = new UCIEngine(KrevetaPath);
        (float MSE, float MoveAccuracy) result = EvaluateKreveta(kreveta);
        
        _krevetaBaseMSE          = result.MSE;
        _krevetaBaseMoveAccuracy = result.MoveAccuracy;
        _krevetaBaseTotal        = _krevetaBaseMSE + 80 * (100 - _krevetaBaseMoveAccuracy);

        kreveta.Quit();
        Console.WriteLine($"\nDone evaluating Kreveta base:\nMSE = {_krevetaBaseMSE}, MoveAccuracy = {_krevetaBaseMoveAccuracy}%\n");
    }

    private static (float MSE, float MoveAccuracy) EvaluateKreveta(UCIEngine kreveta) {
        double totalSqError = 0;
        int    matchMoves   = 0;
        int    count        = 0;

        foreach (var line in File.ReadLines(DatasetPath)) {
            var parts = line.Split(';');
            
            string fen           = parts[0];
            int    stockfishEval = int.Parse(parts[1]);
            string stockfishMove = parts[2];

            (int eval, string move) = kreveta.EvaluateFEN(fen, MoveTime);
            
            double diff   = eval - stockfishEval;
            totalSqError += diff * diff;
            
            if (move == stockfishMove) 
                matchMoves++;
            
            Console.Write($"Finished: {++count}/{PositionCount}\r");
        }

        float mse          = (float)(totalSqError / count);
        float moveAccuracy = 100.0f * matchMoves / count;
        
        return (mse, moveAccuracy);
    }

    private static void UpdateTweaks(string paramCMD, float total) {
        int[] shifts = paramCMD.Split(' ')[1..(Tweaks.Length + 1)]
            .Select(int.Parse).ToArray();

        // this version seems to be better
        if (total < _krevetaBaseTotal) {
            for (int i = 0; i < shifts.Length; i++) {
                Tweaks[i].Item1 += shifts[i];
                Tweaks[i].Item2++;
            }
        }
        
        // this version is probably worse
        else if (total > _krevetaBaseTotal) {
            for (int i = 0; i < shifts.Length; i++) {
                Tweaks[i].Item1 -= (float)shifts[i] / 20;
                Tweaks[i].Item2++;
            }
        }
    }

    // TODO - read previously saved results and update
    // them instead of completely overwriting them
    private static void StoreResult() {
        var tweakResult = new List<string>();
        for (int i = 0; i < Tweaks.Length; i++) {
            tweakResult.Add(
                (Tweaks[i].Item1 / Tweaks[i].Item2).ToString()
            );
        }
        File.WriteAllLines(OutputPath, tweakResult);
    }
}