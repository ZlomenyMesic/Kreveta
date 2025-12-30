//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.moveorder.history;
using Kreveta.moveorder.history.corrections;
using Kreveta.nnue;
using Kreveta.perft;
using Kreveta.search.transpositions;
using Kreveta.uci;
using Kreveta.approx;

using System;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
using System.Threading;

namespace Kreveta;

internal static class Program {
    
    internal const string Name    = "Kreveta";
    internal const string Version = "2.2.2";
    internal const string Author  = "ZlomenyMesic";
    internal const string Network = "nnue-128-16-16-v4.bin";

    internal static int Main(string[] args) {
        
        // although this can make the engine a bit unstable,
        // it seems to bring quite nice performance benefits
        using var cur     = Process.GetCurrentProcess();
        cur.PriorityClass = ProcessPriorityClass.RealTime;

        // free manually allocated memory before exiting
        AppDomain.CurrentDomain.ProcessExit += FreeMemory;

        // not sure why and how anyone would attempt this, but fine
        if (args.Length != 0)
            UCI.Log("Command line arguments are not supported");

        // okay, i know this is really evil, but i am just far too lazy
        // to implement fallbacks, but i might get to it in the future
        if (!Avx2.IsSupported || !Bmi2.IsSupported) {
            UCI.Log("AVX2 and BMI2 hardware support is required. Your current CPU features:");
            UCI.Log($"  AVX2: {Avx2.IsSupported}");
            UCI.Log($"  BMI2: {Bmi2.IsSupported}");
            UCI.Log("This means you sadly won't be able to run this engine :(");
            Console.ReadKey();
        }

        // this forces running static constructors to ensure everything
        // is initialized right at the beginning; otherwise crash :(
        _ = typeof(ZobristHash);

        // pre-compute move lookup tables
        _ = typeof(PextLookupTables);
        _ = typeof(LookupTables);

        // history tables
        _ = typeof(QuietHistory);
        _ = typeof(CaptureHistory);
        
        // adjacent files
        _ = typeof(Eval);

        // load the embedded nnue weights
        _ = typeof(NNUEWeights);
        _ = typeof(MathApprox);

        ContinuationHistory.Init();
        
        // the engine sometimes crashes unexplainably during initialization,
        // and this tiny delay actually seems to be suppressing the issue
        Thread.Sleep(350);
        
        // the default position is startpos to prevent crashes when
        // the user types go or perft without setting a position
        Game.Board = Board.CreateStartpos();
        
        // header text when launching the engine
        UCI.Log($"{Name}-{Version} by {Author}");
        UCI.InputLoop();
        
        return 0;
        
        // manually allocated memory is spread throughout the whole
        // codebase, so different freeing methods are being called
        static void FreeMemory(object? sender, EventArgs e) {
            ((Action)TT.Clear + PerftTT.Clear + PawnCorrections.Clear + Killers.Clear + LookupTables.Clear + ZobristHash.Clear)();
        }
    }
}