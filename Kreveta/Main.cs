//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.moveorder.history.corrections;
using Kreveta.nnue;
using Kreveta.nnue.approx;
using Kreveta.perft;
using Kreveta.search;
using Kreveta.search.transpositions;
using Kreveta.uci;

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Kreveta.consts;
using Kreveta.search.helpers;

namespace Kreveta;

internal static class Program {
    
    internal const string Name    = "Kreveta";
    internal const string Version = "2.3.1";
    internal const string Author  = "ZlomenyMesic";
    internal const string Network = "nnue-128-16-16-v4.bin";

    internal static int Main(string[] args) {

        // according to some LLMs, this may make the engine quite unstable. however, from my experience,
        // it doesn't make anything unstable at all, and even helps provide nice performance benefits.
        // we also wrap the whole in a try-catch statement, as it might fail on some systems
        try {
            using var cur = Process.GetCurrentProcess();
            cur.PriorityClass = ProcessPriorityClass.High;
        }

#pragma warning disable CA1031
        catch {
            UCI.Log("Unable to set process priority to High. Continuing with Normal priority.");
        }
#pragma warning restore CA1031

        // free manually allocated memory before exiting
        AppDomain.CurrentDomain.ProcessExit += FreeMemory;

        // not sure why and how anyone would attempt this, but fine
        if (args.Length != 0)
            UCI.Log("Command line arguments are not supported");

        // hardware support is checked at runtime time
        if (!Consts.UseAVX2 || !Consts.UseBMI2)
            UCI.Log("AVX2 or BMI2 instruction sets not supported. Fallbacks will be used, but performance may be significantly degraded.");

        // generate pseudo-random zobrist keys
        ZobristHash.Init();

        // pre-compute move lookup tables
        PextLookupTables.Init();
        LookupTables.Init();
        
        // compute adjacent file bitboards, and the static evaluation hash table
        Eval.Init();
        SETT.Realloc();
        
        // the build time is embedded into the executable
        string buildTime = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "BuildTimestamp").Value ?? "";
        
        // header text when launching the engine
        UCI.Log($"{RGB.Peach}{Name}-{Version}{RGB.Reset}", nl: false);
        UCI.Log($" by {Author} (built {buildTime})");
        
        // load the embedded nnue weights and pre-allocate the accumulator pool
        NNUEWeights.Load();
        NNUEEvaluator.Init();
        MathApprox.Init();
        
        // pre-allocate PV tables
        PVS.Init();
        
        ThreeFold.Init(0);
        
        // make startpos the default position to prevent crashes when the user
        // forgets to set a position, and tries to run a search or evaluation
        Game.Board = Board.CreateStartpos();
        
        UCI.InputLoop();
        return 0;
        
        // manually allocated memory is spread throughout the whole codebase
        static void FreeMemory(object? sender, EventArgs e) {
            TT.Clear();
            SETT.Clear();
            PerftHashTable.Clear();
            Corrections.Free();
            LookupTables.Clear();
            ZobristHash.Clear();
        }
    }
}