//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;

// ReSharper disable InconsistentNaming

namespace Kreveta.consts;

[Flags]
internal enum CastRights : byte {
    NONE  = 0,
    
    //[FlagDisplayName("K")]
    K     = 1, // white kingside
    //[FlagDisplayName("Q")]
    Q     = 2, // white queenside
    //[FlagDisplayName("k")]
    k     = 4, // black kingside
    //[FlagDisplayName("q")]
    q     = 8, // black queenside
    
    ALL   = K | Q | k | q,
    WHITE = K | Q,
    BLACK = k | q
}