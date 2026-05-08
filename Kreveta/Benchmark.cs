//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.uci;

using System;
using System.Diagnostics;

// ReSharper disable InconsistentNaming

namespace Kreveta;

// bench analyzes a predefined set of positions at a certain depth. the main goal is being
// able to differentiate individual versions of Kreveta, by assigning each one its unique
// bench node count. furthermore, bench may also be used to reliable measure search speed,
// as regular perft doesn't go further beyond just move generation
internal static class Benchmark {
    
    // here's a bunch of positions, which are all searched during bench
    private static readonly string[] Suite = [
        
        // initial position
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 0",
        
        // popular opening lines
        "r1bqkb1r/pppp1ppp/2n2n2/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4", // Ruy Lopez (Berlin defence)
        "rnbqkb1r/pp2pppp/3p1n2/8/3NP3/2N5/PPP2PPP/R1BQKB1R b KQkq - 2 5",     // Sicilian Defence (main line)
        "rn1qkbnr/pp2pppp/2p5/3pPb2/3P4/5N2/PPP2PPP/RNBQKB1R b KQkq - 2 4",    // Caro-Kann Defence (Advance variation)
        "rnbqk2r/ppp1bppp/4pn2/3p2B1/2PP4/2N5/PP2PPPP/R2QKBNR w KQkq - 4 5",   // Queen's Gambit Declined
        "rnbqk2r/ppp1ppbp/3p1np1/8/2PPP3/2N5/PP3PPP/R1BQKBNR w KQkq - 0 5",    // King's Indian Defense (Classical variation)
        "rnbqk1nr/pp3ppp/4p3/2ppP3/1b1P4/P1N5/1PP2PPP/R1BQKBNR b KQkq - 0 5",  // French Defense (Winawer)
        
        // opening blunders
        "rnbqkbnr/pppp2pp/5p2/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 0 4", // 1. e4 e5 2. Nf3 f6
        "rnbqkbnr/pppp1ppp/8/4p3/6P1/5P2/PPPPP2P/RNBQKBNR b KQkq - 0 3",   // 1. g4 e5 2. f3 (Qh4 - mate)
        "r1bqkbnr/pp2pppp/2n5/2pp4/3N4/6P1/PPPPPPBP/RNBQK2R b KQkq - 3 4", // hanging a knight
        
        // middlegame positions (many pieces, and many available moves)
        "2kr3r/1b1nqp1p/p2p1npQ/1pp5/4P3/PNN2P2/1PP3PP/1K1R1B1R b - - 1 16", // Kasparov's Immortal
        "2rq1rk1/pb1n1ppN/4p3/1pb5/3P1Pn1/P1N5/1PQ1B1PP/R1B2RK1 b - - 2 16", // Nerves of steel (Aronian vs. Anand)
        "2r1r3/5pk1/7p/8/Np4P1/1P1Rq1P1/6BP/1N1R3K b - - 0 37",              // Karpov vs. Kasparov (WCC 1985)
        "2rr2k1/pp3pp1/4b3/n2p3p/1b1P1B1P/1B3PP1/PP3KN1/R1R5 w - - 7 29",    // Carlsen vs. Caruana (WCC 2018)
        "r2r2k1/p4p1p/4pp2/1p6/3bq3/PP1N2P1/Q3PP1P/2R2RK1 w - - 3 23",       // Carlsen vs. Nepomniachtchi (WCC 2021)
        
        // basic middlegame/endgame positions (from unknown games)
        "r1b2rk1/1pq2pp1/1npb1B1p/p7/P1BP4/1NN4P/1PQ2PP1/4RRK1 b - - 0 1",
        "3b1r2/1p1q2kp/1n1p1pp1/2rPp1P1/4P2P/P1NQ4/1PRN1P2/1K4R1 w - - 0 1",
        "4r3/1b4p1/p4k1p/1p1p4/3N3P/P2B2P1/1PP2K2/8 b - - 0 1",
        "8/8/2k5/p1b1r2p/PrRR2pP/2K1B1P1/5P2/8 w - - 0 1",
        
        // 5-man positions (from Stockfish)
        "8/8/8/8/5kp1/P7/8/1K1N4 w - - 0 1",     // Kc2 - mate
        "8/8/8/5N2/8/p7/8/2NK3k w - - 0 1",      // Na2 - mate
        "8/3k4/8/8/8/4B3/4KB2/2B5 w - - 0 1",    // draw
        
        // 6-man positions (from Stockfish)
        "8/8/1P6/5pr1/8/4R3/7k/2K5 w - - 0 1",   // Re5 - mate
        "8/2p4P/8/kr6/6R1/8/8/1K6 w - - 0 1",    // Ka2 - mate
        "8/8/3P3k/8/1p6/8/1P6/1K3n2 b - - 0 1",  // Nd2 - draw

        // 7-man positions (from Stockfish)
        "8/R7/2q5/8/6k1/8/1P5p/K6R w - - 0 124", // draw
        
        // zugzwang positions, where null move pruning may fail, along with known best moves;
        // from the Chess Programming Wiki: https://www.chessprogramming.org/Null_Move_Test-Positions
        "8/8/p1p5/1p5p/1P5p/8/PPP2K1p/4R1rk w - - 0 1",    // Rf1
        "1q1k4/2Rr4/8/2Q3K1/8/8/8/8 w - - 0 1",            // Kh6
        "7k/5K2/5P1p/3p4/6P1/3p4/8/8 w - - 0 1",           // g5
        "8/6B1/p5p1/Pp4kp/1P5r/5P1Q/4q1PK/8 w - - 0 32",   // Qxh4
        "8/8/1p1r1k2/p1pPN1p1/P3KnP1/1P6/8/3R4 b - - 0 1", // Nxd5
        
        // other mate and stalemate positions
        "6k1/3b3r/1p1p4/p1n2p2/1PPNpP1q/P3Q1p1/1R1RB1P1/5K2 b - - 0 1", // Qf4 - mate in 5
        "r2r1n2/pp2bk2/2p1p2p/3q4/3PN1QP/2P3R1/P4PP1/5RK1 w - - 0 1"    // Qg7 - mate in 4
    ];

    private const int DefaultDepth = 12;

    // externally accessed node counter
    internal static          ulong Nodes;
    internal static volatile bool  Finished;
    
    internal static void Run(ReadOnlySpan<string> tokens) {
        TM.MoveTime   = long.MaxValue;
        Game.FullGame = false;
        Nodes         = 0UL;
        
        // there can be either default or custom depth
        int depth = DefaultDepth;
        if (tokens.Length >= 2) _ = int.TryParse(tokens[1], out depth);

        var sw = Stopwatch.StartNew();
        
        // go through every position, and search it at the specified depth. we start the
        // searches simply by simulating UCI commands. although this approach is quite
        // dirty, it's probably the most compact way of writing this
        for (int i = 0; i < Suite.Length; i++) {
            UCI.Log($"\nPosition {i + 1}/{Suite.Length}: FEN {Suite[i]}");
            
            // set the position and analyze it
            Game.SetPosFEN($"position fen {Suite[i]}".Split(' '));
            UCI.CmdGo($"go depth {depth}".Split(' '), bench: true);
            
            // this approach is disgusting, but it works
            while (!Finished);
            Finished = false;
        }
        
        sw.Stop();

        // make sure we're not dividing by zero
        long time = Math.Max(1, sw.ElapsedMilliseconds);
        long nps  = (long)Math.Round((decimal)Nodes / time * 1000, 0);

        // print the final bench results (total nodes, spent time, and NPS)
        UCI.Log(string.Empty);
        UCI.LogStats(forcePrint: true, header: true,
            ("Nodes Searched", Nodes),
            ("Time Spent",     sw.Elapsed),
            ("Average NPS",    nps)
        );
    }
}