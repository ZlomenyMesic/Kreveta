//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;

namespace Kreveta.consts;

[Flags]
internal enum CastlingRights : byte {
    NONE = 0,
    K    = 1, // white kingside
    Q    = 2, // white queenside
    k    = 4, // black kingside
    q    = 8, // black queenside
    ALL  = 15
}