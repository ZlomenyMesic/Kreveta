/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */





// REVERSE FUTILITY PRUNING
internal static class RFPruning {

    // minimum ply and maximum depth to allow rfp
    internal const int MIN_PLY = 50;
    internal const int MAX_DEPTH = 3;

    // higher margin => fewer reductions
    internal const int RF_MARGIN_BASE = 266;

    // if not improving we make the margin smaller
    internal const int IMPROVING_PENALTY = -10;

    // returns the margin which could potentialy raise alpha when added to the score
    internal static int GetMargin(int depth, int col, bool improving) {
        int margin = RF_MARGIN_BASE * (depth + 1);
        //+ (improving ? 0 : IMPROVING_PENALTY);

        return margin * (col == 0 ? 1 : -1);
    }
}

