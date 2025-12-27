//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;
using Kreveta.uci;

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
    internal static void Run(int depth) {
        PerftTT.Init(depth);
        
        // we probably could use something more sophisticated
        // than a stopwatch, but i'm too lazy to do so
        var sw = Stopwatch.StartNew();

        Span<Move> moves = stackalloc Move[128];
        int mcount = Movegen.GetLegalMoves(ref Game.Board, moves);

        ulong nodes = 0UL;
        for (int i = 0; i < mcount; i++) {
            ulong curNodes = 1UL;
            
            if (depth > 1) {
                Board child = Game.Board.Clone(false);
                child.PlayMove(moves[i], false);
            
                // the recursive search starts here
                curNodes = CountNodes(ref child, (byte)(depth - 1));
            }
            
            // print each move after 1 ply and its respective node count
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
        UCI.LogStats(forcePrint: true,
            ("Nodes found", nodes),
            ("Time spent",  sw.Elapsed),
            ("Average NPS", nps));
        
        PerftTT.Clear();
    }

    private static unsafe ulong CountNodes(ref Board board, byte depth) {
        if (UCI.ShouldAbortSearch)
            return 0UL;

        // once we get to depth 1, simply return the number of legal moves
        if (depth == 1) {
            if (PerftTT.TryGetNodes(in board, 1, out ulong leafNodes))
                return leafNodes;
            
            leafNodes = (ulong)Movegen.GetLegalMoves(ref board, stackalloc Move[128]);
            PerftTT.Store(in board, 1, leafNodes);
            return leafNodes;
        }

        // try to find this position at this depth in the perftt
        if (PerftTT.TryGetNodes(in board, depth, out ulong nodes)) {
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
            Board child = board.Clone(false);
            child.PlayMove(moves[i], false);

            // the move is illegal (we moved to or stayed in check)
            if (Check.IsKingChecked(in child, board.Color))
                continue;

            // otherwise continue the search deeper
            nodes += CountNodes(ref child, depth);
        }

        // store the new position in perftt
        PerftTT.Store(in board, ++depth, nodes);

        return nodes;
    }
}
