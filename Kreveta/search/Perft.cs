//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Kreveta.search;

internal static class Perft {
    internal static long Run([NotNull, In, ReadOnly(true)] in Board board, int depth) {

        if (depth == 1) {
            return Movegen.GetLegalMoves(board).Count();
        }

        long nodes = 0;

        List<Move> moves = [];
        Movegen.GetPseudoLegalMoves(board, board.color, moves);

        for (int i = 0; i < moves.Count; i++) {

            Board child = board.Clone();
            child.PlayMove(moves[i]);

            // the move is illegal
            if (Movegen.IsKingInCheck(child, board.color))
                continue;

            nodes += Run(child, depth - 1);
        }

        return nodes;
    }
}
