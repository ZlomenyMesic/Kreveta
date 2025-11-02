//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.moveorder;
using Kreveta.moveorder.historyheuristics;
using Kreveta.perft;
using Kreveta.search.transpositions;
using Kreveta.uci;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Kreveta;

internal static class Program {
    
    internal const string Name    = "Kreveta";
    internal const string Version = "1.2.0";
    internal const string Author  = "ZlomenyMesic";

    internal static int Main(string[] args) {
        using var cur = Process.GetCurrentProcess();
        
        // this does actually make stuff a bit faster
        cur.PriorityClass = ProcessPriorityClass.AboveNormal;

        // free manually allocated memory before exiting
        AppDomain.CurrentDomain.ProcessExit += FreeMemory;

        // although this could be useful, i am lazy
        if (args.Length != 0)
            UCI.Log("Command line arguments are not supported", UCI.LogLevel.WARNING);
        
        // this forces running static constructors to ensure everything
        // is initialized right at the beginning - otherwise crash :(
        RuntimeHelpers.RunClassConstructor(typeof(ZobristHash).TypeHandle);
        
        PextLookupTables.Init();
        LookupTables.Init();
        
        CounterMoveHistory.Init();
        QuietHistory.Init();
        
        Eval.Init();

        // the engine sometimes crashes unexplainably during initialization,
        // and this tiny delay actually seems to be suppressing the issue
        Thread.Sleep(250);
        
        // the default position is startpos to prevent crashes when
        // the user types go or perft without setting a position
        Game.Board = Board.CreateStartpos();

        // header text when launching the engine
        UCI.Log($"{Name}-{Version} by {Author}");
        UCI.InputLoop();
        
        return 0;
        
        // the manually allocated memory is spread throughout the whole
        // codebase, so different freeing methods are being called
        static void FreeMemory(object? sender, EventArgs e) {
            ((Action)TT.Clear + PerftTT.Clear + PawnCorrectionHistory.Clear + Killers.Clear + MoveOrder.Clear + LookupTables.Clear + ZobristHash.Clear)();
        }
    }
}