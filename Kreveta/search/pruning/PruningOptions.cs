//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

namespace Kreveta.search.pruning;

internal readonly record struct PruningOptions {
    internal const bool AllowNullMovePruning        = true;
    //internal const bool AllowProbCut                = true;

    internal const bool AllowFutilityPruning        = true;

    internal const bool AllowLateMovePruning        = true;
    internal const bool AllowLateMoveReductions     = true;

    internal const bool AllowDeltaPruning           = true;
}
