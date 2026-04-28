//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// ReSharper disable InconsistentNaming

namespace Kreveta.search.helpers;

// when an aspiration window search is completed, the possibilities
// are represented using this (whether the score exceeded bounds)
internal enum AspirationFail : sbyte {
    NONE      = 0,
    FAIL_HIGH = 1,
    FAIL_LOW  = -1
}