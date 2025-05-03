//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;

using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Kreveta.search.perft;

internal static class Perft {
    internal static void Run(int depth) {
        
        PerftTT.Clear();
        
        // we probably could use something more sophisticated
        // than a stopwatch, but i'm lazy
        var sw = Stopwatch.StartNew();

        ulong nodes = CountNodes(Game.Board, (byte)depth);

        sw.Stop();

        // precaution to not divide by zero
        long time = sw.ElapsedMilliseconds == 0 
            ? 1 : sw.ElapsedMilliseconds;
        
        if (UCI.AbortSearch)
            UCI.Log("perft search aborted", UCI.LogLevel.WARNING);
        
        // print the results
        UCI.Log($"nodes: {nodes}", UCI.LogLevel.INFO);
        UCI.Log($"time spent: {sw.Elapsed}", UCI.LogLevel.INFO);
        UCI.Log($"nodes per second: {Math.Round((decimal)nodes / time * 1000, 0)}", UCI.LogLevel.INFO);
    }
    
    private static unsafe ulong CountNodes([In, ReadOnly(true)] in Board board, byte depth) {

        if (UCI.AbortSearch)
            return 0UL;

        if (depth == 1) {
            return (ulong)Movegen.GetLegalMoves(board).Length;
        }

        if (PerftTT.TryGetNodes(board, depth, out ulong nodes)) {
            return nodes;
        }

        nodes = 0UL;
        depth--;

        Span<Move> moves = Movegen.GetPseudoLegalMoves(board);

        fixed (Move* ptr = moves) {
            for (byte i = 0; i < moves.Length; i++) {

                Board child = board.Clone();
                child.PlayMove(ptr[i]);

                // the move is illegal (we moved to or stayed in check)
                if (Movegen.IsKingInCheck(child, board.Color))
                    continue;

                nodes += CountNodes(child, depth);
            }
        }

        PerftTT.Store(board, ++depth, nodes);

        return nodes;
    }
}
