//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// ReSharper disable InconsistentNaming

namespace Kreveta.consts;

internal static class Consts {
    
    // capacity of the buffer in movegen. also used in MoveOrder and Perft
    internal const int MoveBufferSize = 110;
    
    // files may be indexed (sq % 8) or preferably (sq & 7)
    // ranks are indexed (sq / 8) or (sq >> 3)
    internal const string Files  = "abcdefgh";
    internal const string Pieces = "pnbrqk";
    
    internal static readonly ulong[] FileMask = [
        0x0101010101010101, 0x0202020202020202, 0x0404040404040404, 
        0x0808080808080808, 0x1010101010101010, 0x2020202020202020, 
        0x4040404040404040, 0x8080808080808080
    ];
    
    internal static readonly ulong[] RelevantFileMask = [
        0x0001010101010100, 0x0002020202020200, 0x0004040404040400, 
        0x0008080808080800, 0x0010101010101000, 0x0020202020202000, 
        0x0040404040404000, 0x0080808080808000
    ];
}