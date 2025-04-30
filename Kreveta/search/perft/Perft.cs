//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Kreveta.search.perft;

internal static class Perft {
    internal static ulong Run([In, ReadOnly(true)] in Board board, int depth) {

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

            nodes += Run(child, depth - 1);
        }

        PerftTT.Store(board, depth, nodes);

        return nodes;
    }
}
