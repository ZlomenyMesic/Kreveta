//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;

using System.ComponentModel;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.search.pruning;

// REVERSE FUTILITY PRUNING:
// apart from futility pruning, we also use reverse futility pruning - it's
// basically the same as fp, but we subtract the margin from the static eval
// and prune the branch if we fail high (so it's quite the opposite)
internal static class ReverseFutilityPruning {

    // minimum ply and maximum depth to allow rfp
    internal const int MinPly   = 5;
    internal const int MaxDepth = 4;
    internal const int MinDepth = 2;

    // higher margin => fewer reductions
    private const int RFMarginBase = 159;

    // if not improving we make the margin smaller
    private const int ImprovingPenalty = -10;

    private const int SQPly = 4;

    internal static bool TryPrune([In, ReadOnly(true)] in Board board, int depth, Color col, Window window, out short retScore) {
        retScore = default;

        short staticEval = Eval.StaticEval(board);

        int rfMargin = RFMarginBase * (depth + 1) * (col == Color.WHITE ? 1 : -1);

        staticEval -= (short)rfMargin;

        // we failed high (above beta). our opponent already has an alternative which
        // wouldn't allow this move/node/score to happen
        if (col == Color.WHITE
            ? staticEval >= window.Beta
            : staticEval <= window.Alpha) {

            retScore = QSearch.Search(board, SQPly, window);
            return true;
        }

        return false;
    }
}

