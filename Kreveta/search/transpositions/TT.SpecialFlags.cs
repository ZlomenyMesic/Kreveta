//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
// ReSharper disable InconsistentNaming

namespace Kreveta.search.transpositions;

internal static partial class TT {
    // depending on where the score fell relatively to the
    // window when saving to TT, we store the score type
    [Flags]
    internal enum SpecialFlags : byte {
        SCORE_UPPER_BOUND = 1, // the score was above beta
        SCORE_LOWER_BOUND = 2, // the score was below alpha
        SCORE_EXACT       = 4, // the score fell right into the window

        //SHOULD_OVERWRITE  = 8  // the node is old and should be overwritten
    }
}