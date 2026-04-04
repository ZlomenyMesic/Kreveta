//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.Diagnostics;
using Kreveta.uci;

// ReSharper disable InconsistentNaming

namespace Kreveta;

internal static class Bench {
    private static readonly string[] FENs = [
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0",                // startpos
        "r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2",        // 1. e4 e5 2. Nf3 Nc6
        "rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq c6 0",           // 1. e4 c5 (sicilian)
        "rnbqkb1r/ppp2ppp/4pn2/3p4/2PP4/2N5/PP2PPPP/R1BQKBNR w KQkq - 2",        // queen's gambit declined
        "rnbqkbnr/pp2pppp/8/2pp4/8/5NP1/PPPPPPBP/RNBQK2R b KQkq - 1",            // king's indian attack
        "rnbqkbnr/pppp2pp/5p2/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 0",         // opening blunder #1
        "rnbqkbnr/pppp1ppp/8/4p3/6P1/5P2/PPPPP2P/RNBQKBNR b KQkq - 0",           // opening blunder #2 (M1)
        
        "r1b2rk1/1pq2pp1/1npb1B1p/p7/P1BP4/1NN4P/1PQ2PP1/4RRK1 b - - 0 1",       // middlegame #1
        "r2q1rk1/3nbppp/b1pp1n2/p3p3/1p1PP1P1/P5NP/1PP1NPB1/R1BQ1RK1 b - - 0 1", // middlegame #2
        "3b1r2/1p1q2kp/1n1p1pp1/2rPp1P1/4P2P/P1NQ4/1PRN1P2/1K4R1 w - - 0 1",     // middlegame #3
        
        "4r3/1b4p1/p4k1p/1p1p4/3N3P/P2B2P1/1PP2K2/8 b - - 0 1",                  // endgame #1
        "8/8/2k5/p1b1r2p/PrRR2pP/2K1B1P1/5P2/8 w - - 0 1",                       // endgame #2
    ];

    private const int DefaultDepth = 12;

    // externally accessed node counter
    internal static ulong Nodes;
    internal static bool  Finished;

    // bench analyzes a predefined set of positions at a certain depth. the goal is to
    // provide a reliable speed measurement, as real searches are performed, and to be
    // able to differentiate engine versions by assigning them the bench node count
    internal static void Run(ReadOnlySpan<string> tokens) {
        TM.MoveTime   = long.MaxValue;
        Game.FullGame = false;
        Nodes         = 0UL;
        
        // there can be either default or custom depth
        int depth = DefaultDepth;
        if (tokens.Length >= 2) _ = int.TryParse(tokens[1], out depth);

        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < FENs.Length; i++) {
            UCI.Log($"\nPosition {i + 1}/{FENs.Length}: FEN {FENs[i]}");
            
            // set the position and analyze it
            Game.SetPosFEN($"position fen {FENs[i]}".Split(' '));
            UCI.CmdGo($"go depth {depth}".Split(' '), bench: true);
            
            while (!Finished) { }
            Finished = false;
        }
        
        sw.Stop();
        long time = sw.ElapsedMilliseconds == 0 ? 1 : sw.ElapsedMilliseconds;

        UCI.Log(string.Empty);
        UCI.LogStats(forcePrint: true,             
            ("Nodes Searched", Nodes),
            ("Time Spent",     sw.Elapsed),
            ("Average NPS",    (int)Math.Round((decimal)Nodes / time * 1000, 0))
        );
    }
}