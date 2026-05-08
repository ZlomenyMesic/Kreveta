//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;
using Kreveta.uci;
using Kreveta.uci.options;
using System;
using System.Diagnostics;

namespace Kreveta.perft;

// perft stands for performance test, and is used mainly to locate bugs in movegen
// or to simply measure the movegen speed. perft takes a depth as an argument, and
// displays the total number of legally achievable positions in that number of moves.
// for example, from the starting position, perft 1 displays 20, since white has 20
// different legal moves and thus 20 achievable positions. at depth 2 it would be
// 400, since black has the same number of responses. perft only counts leaf nodes
// at the exact depth, so terminal nodes (checkmates/stalemates/draws) appearing at
// a shallower depth aren't included
internal static class Perft {

    // a bunch of positions collected from Chess Programming Wiki
    private static readonly (string FEN, int Depth, long ExpectedNodes)[] Suite = [
        ("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",                 6, 119_060_324), // startpos
        ("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",     5, 193_690_690), // known as Kiwipete by Peter McKenzie
        ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",                                7, 178_633_661), // an endgame position
        ("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1",         5,  15_833_292), // a middlegame position
        ("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8",                5,  89_941_194), // this position is often buggy for engines
        ("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10", 5, 164_075_551) // another middlegame position
    ];

    /*
     * now, perft can be run two ways:
     * 
     * 'perft <d>' runs perft on the current position, at a specified depth
     * 'perft'     without any arguments runs perft on a bunch of hard-coded positions,
     *             and compares the results to known node counts, which should allow us
     *             to locate potential future bugs more easily
     */
    internal static void RunDefault() {
        int correct = 0;

        for (int i = 0; i < Suite.Length; i++) {
            var (fen, depth, expected) = Suite[i];

            // print the current FEN string
            UCI.Log($"\n{RGB.Yellow}Position {i + 1}/{Suite.Length}{RGB.Reset}: FEN {fen}");
            Game.SetPosFEN(["position", "fen", ..fen.Split(' ')]);

            // reset the perft hash table before each position
            if (Options.UsePerftHash)
                PerftHashTable.Init(depth);

            // do the actual node counting search
            ulong nodes = Options.UsePerftHash
                ? CountNodes      (ref Game.Board, (byte)depth)
                : CountNodesVirgin(ref Game.Board, (byte)depth);

            // break the loop
            if (UCI.ShouldAbortSearch)
                break;

            bool match = nodes == (ulong)expected;
            if (match) correct++;

            // print the found node count along with the known expected value
            UCI.LogStats(forcePrint: true, header: false,
                (" Depth",          depth),
                (" Nodes Found",    nodes),
                (" Nodes Expected", expected)
            );
        }

        // print the final result overviewing all positions
        UCI.Log("\nResult: ",                                  nl: false);
        UCI.Log(correct == Suite.Length ? RGB.Green : RGB.Red, nl: false);
        UCI.Log($"{correct}/{Suite.Length}{RGB.Reset} ",       nl: false);
        UCI.Log("positions matched the expected node count!");


        // clear the perft hash table once we're done
        if (Options.UsePerftHash)
            PerftHashTable.Clear();
    }

    // this is called when the user runs 'perft <d>', e.g. specifies search depth
    internal static void RunAtDepth(int depth) {
        if (Options.UsePerftHash)
            PerftHashTable.Init(depth);
        
        // we probably could use something more sophisticated
        // than a stopwatch, but i'm too lazy to do so
        var sw = Stopwatch.StartNew();

        // generate all moves from the root
        Span<Move> moves  = stackalloc Move[128];
        int        mcount = Movegen.GetLegalMoves(ref Game.Board, moves);

        // we actually run perft at depth - 1 here for every root move,
        // since we want to be able to print the node count per-move
        ulong nodes = 0UL;
        for (int i = 0; i < mcount; i++) {
            ulong curNodes = 1UL;
            
            // of course, don't go any deeper if the initial depth was 1
            if (depth > 1) {
                Board child = Game.Board.CloneNoNNUE();
                child.PlayMove(moves[i], false);
            
                // the recursive search starts here
                curNodes = Options.UsePerftHash 
                    ? CountNodes      (ref child, (byte)(depth - 1))
                    : CountNodesVirgin(ref child, (byte)(depth - 1));
            }
            
            // print each move after 1 ply and its respective node count
            if (!UCI.ShouldAbortSearch)
                UCI.Log($"{moves[i].ToLAN()}: {curNodes} nodes");
            nodes += curNodes;
        }

        sw.Stop();

        // precaution to not divide by zero
        long time = sw.ElapsedMilliseconds == 0
            ? 1 : sw.ElapsedMilliseconds;

        // even if the search ended prematurely, we still
        // display the results found before that
        if (UCI.ShouldAbortSearch)
            UCI.Log("Perft search aborted :(");

        int nps = (int)Math.Round((decimal)nodes / time * 1000, 0);

        // print the results (statistics may be turned off
        // via the option, so we must force printing them
        // even if stats are disabled)
        UCI.LogStats(forcePrint: true, header: true,
            ("Nodes Found", nodes),
            ("Time Spent",  sw.Elapsed),
            ("Average NPS", nps));
        
        if (Options.UsePerftHash)
            PerftHashTable.Clear();
    }

    private static unsafe ulong CountNodes(ref Board board, byte depth) {
        if (UCI.ShouldAbortSearch)
            return 0UL;

        // once we get to depth 1, simply return the number of legal moves
        if (depth == 1) {
            if (PerftHashTable.TryGetNodes(in board, 1, out ulong leafNodes))
                return leafNodes;
            
            leafNodes = (ulong)Movegen.GetLegalMoves(ref board, stackalloc Move[128]);
            PerftHashTable.Store(in board, 1, leafNodes);
            return leafNodes;
        }

        // try to find this position at this depth in the perftt
        if (PerftHashTable.TryGetNodes(in board, depth, out ulong nodes)) {
            return nodes;
        }

        nodes = 0UL;
        depth--;

        // only generate pseudolegal moves, legality is checked inside
        // the loop to save time (early legality checking is wasteful)
        Span<Move> moves = stackalloc Move[Consts.MoveBufferSize];
        int count = Movegen.GetPseudoLegalMoves(ref board, moves);
        
        for (byte i = 0; i < count; i++) {

            // create a copy of the board and play the move
            Board child = board.CloneNoNNUE();
            child.PlayMove(moves[i], false);

            // the move is illegal (we moved to or stayed in check)
            if (Check.IsKingChecked(in child, board.SideToMove))
                continue;

            // otherwise continue the search deeper
            nodes += CountNodes(ref child, depth);
        }

        // store the new position in perftt
        PerftHashTable.Store(in board, ++depth, nodes);

        return nodes;
    }
    
    // the same method as above, but doesn't use PerftTT (this may be toggled
    // using the UCI options UsePerftHash). a separate function is used to
    // ensure maximum efficiency by avoiding if-checks
    private static unsafe ulong CountNodesVirgin(ref Board board, byte depth) {
        if (UCI.ShouldAbortSearch) return 0UL;
        if (depth == 1)            return (ulong)Movegen.GetLegalMoves(ref board, stackalloc Move[128]);

        ulong nodes = 0UL;
        depth--;
        
        Span<Move> moves = stackalloc Move[Consts.MoveBufferSize];
        int count = Movegen.GetPseudoLegalMoves(ref board, moves);
        
        for (byte i = 0; i < count; i++) {
            Board child = board.CloneNoNNUE();
            child.PlayMove(moves[i], false);
            
            if (Check.IsKingChecked(in child, board.SideToMove))
                continue;

            nodes += CountNodesVirgin(ref child, depth);
        }

        return nodes;
    }
}
