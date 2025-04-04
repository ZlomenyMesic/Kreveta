//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta;
using Kreveta.evaluation;
using Kreveta.search;
using Kreveta.search.pruning;
using System.Runtime.CompilerServices;

// REVERSE FUTILITY PRUNING:
// apart from futility pruning, we also use reverse futility pruning - it's
// basically the same as fp but we subtract the margin from the static eval
// and prune the branch if we fail high (so it's quite the opposite)
internal static class RFP {

    // minimum ply and maximum depth to allow rfp
    private const int MIN_PLY = 5;
    private const int MAX_DEPTH = 4;
    private const int MIN_DEPTH = 2;

    // higher margin => fewer reductions
    private const int RF_MARGIN_BASE = 159;

    // if not improving we make the margin smaller
    private const int IMPROVING_PENALTY = -10;

    private const int QS_PLY = 4;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CanPrune(int depth, int ply, bool in_check, short pv_score) {
        return PruningOptions.ALLOW_REVERSE_FUTILITY_PRUNING 
            && ply >= MIN_PLY
            && depth >= MIN_DEPTH
            && depth <= MAX_DEPTH
            && !in_check
            && !Eval.IsMateScore(pv_score);
    }

    internal static bool TryPrune(Board b, int depth, Color col, Window window, out short ret_score) {
        ret_score = default;

        short s_eval = Eval.StaticEval(b);

        int rf_margin = RF_MARGIN_BASE * (depth + 1) * (col == Color.WHITE ? 1 : -1);

        // we failed high (above beta). our opponent already has an alternative which
        // wouldn't allow this move/node/score to happen
        if (window.FailsHigh((short)(s_eval - rf_margin), col)) {
            ret_score = PVSearch.QSearch(b, QS_PLY, window);

            return true;
        }

        return false;
    }
}

