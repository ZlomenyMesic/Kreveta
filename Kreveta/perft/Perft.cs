//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;

using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;

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
    internal static void Run(int depth) {
        PerftTT.Clear();
        
        // we probably could use something more sophisticated
        // than a stopwatch, but i'm lazy to do so
        var sw = Stopwatch.StartNew();

        // the recursive search starts here
        ulong nodes = CountNodes(Game.Board, (byte)depth);

        sw.Stop();

        // precaution to not divide by zero
        long time = sw.ElapsedMilliseconds == 0 
            ? 1 : sw.ElapsedMilliseconds;
        
        // even if the search ended prematurely, we still
        // display the results found before that
        if (UCI.AbortSearch)
            UCI.Log("perft search aborted", UCI.LogLevel.WARNING);

        int nps = (int)Math.Round((decimal)nodes / time * 1000, 0);
        
        // print the results (statistics may be turned off
        // via the option, so we must force printing them
        // even if stats are disabled)
        UCI.LogStats(forcePrint: true,
            ("nodes found", nodes), 
            ("time spent",  sw.Elapsed),
            ("average NPS", nps));
    }
    
    private static unsafe ulong CountNodes([In, ReadOnly(true)] in Board board, byte depth) {

        if (UCI.AbortSearch)
            return 0UL;

        // once we get to depth 1, simply return the number of legal moves
        if (depth == 1) {
            return (ulong)Movegen.GetLegalMoves(board).Length;
        }

        // try to find this position at this depth in the perftt
        if (PerftTT.TryGetNodes(board, depth, out ulong nodes)) { 
            return nodes;
        }

        nodes = 0UL;
        depth--;

        // only generate pseudolegal moves, legality is checked inside
        // the loop to save time (early legality checking is wasteful)
        Span<Move> moves = Movegen.GetPseudoLegalMoves(board);

        fixed (Move* ptr = moves) {
            for (byte i = 0; i < moves.Length; i++) {

                // create a copy of the board and play the move
                Board child = board.Clone();
                child.PlayMove(ptr[i]);

                // the move is illegal (we moved to or stayed in check)
                if (Movegen.IsKingInCheck(child, board.Color))
                    continue;

                // otherwise continue the search deeper
                nodes += CountNodes(child, depth);
            }
        }

        // store the new position in perftt
        PerftTT.Store(board, ++depth, nodes);

        return nodes;
    }
}
