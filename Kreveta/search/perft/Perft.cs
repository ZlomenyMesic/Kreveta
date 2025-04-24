//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Kreveta.search.perft;

internal static class Perft {
    internal static ulong Run([In, ReadOnly(true)] in Board board, int depth) {

        if (depth == 1) {
            return (ulong)Movegen.GetLegalMoves(board).Count();
        }

        if (PerftTT.TryGetNodes(board, depth, out ulong nodes)) {
            return nodes;
        }

        nodes = 0UL;

        List<Move> moves = [];
        Movegen.GetPseudoLegalMoves(board, board.Color, moves);

        for (int i = 0; i < moves.Count; i++) {

            Board child = board.Clone();
            child.PlayMove(moves[i]);

            // the move is illegal
            if (Movegen.IsKingInCheck(child, board.Color))
                continue;

            nodes += Run(child, depth - 1);
        }

        PerftTT.Store(board, depth, nodes);

        return nodes;
    }
}
