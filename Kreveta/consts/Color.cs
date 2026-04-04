//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// ReSharper disable InconsistentNaming

using System;

namespace Kreveta.consts;

// this enum is NOT flags, however, marking it as flags allows
// usage of bitwise operators free of warnings, which results
// in more efficient color flips
[Flags]
internal enum Color : byte {
    WHITE = 0,
    BLACK = 1,
    NONE  = 2
}