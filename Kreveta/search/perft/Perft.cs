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
        
        // we probably could use something more sophisticated
        // than a stopwatch, but i'm lazy
        var sw = Stopwatch.StartNew();
        
        ulong nodes = CountNodes(Game.Board, depth);
        
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
        
        PerftTT.Clear();
    }
    
    private static ulong CountNodes([In, ReadOnly(true)] in Board board, int depth) {

        if (UCI.AbortSearch)
            return 0UL;

        if (depth == 1) {
            return (ulong)Movegen.GetLegalMoves(board).Length;
        }

        if (PerftTT.TryGetNodes(board, depth, out ulong nodes)) {
            return nodes;
        }

        nodes = 0UL;

        Span<Move> moves = Movegen.GetPseudoLegalMoves(board);

        for (int i = 0; i < moves.Length; i++) {

            Board child = board.Clone();
            child.PlayMove(moves[i]);

            // the move is illegal (we moved to or stayed in check)
            if (Movegen.IsKingInCheck(child, board.Color))
                continue;

            nodes += CountNodes(child, depth - 1);
        }

        PerftTT.Store(board, depth, nodes);

        return nodes;
    }
}
