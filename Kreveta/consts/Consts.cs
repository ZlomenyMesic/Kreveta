//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// simplifying syntax later on
global using TT = Kreveta.search.transpositions.TranspositionTable;
global using TM = Kreveta.TimeManager;

using System;
using System.Runtime.Intrinsics.X86;

// ReSharper disable InconsistentNaming
namespace Kreveta.consts;

internal static class Consts {
    internal static readonly Random RNG = new(
        1013 * DateTime.Now.Millisecond * DateTime.Now.Second
    );

    // CPU-dependent optimizations, we must check whether they are supported
    internal static readonly bool UseAVX2 = Avx2.IsSupported;
    internal static readonly bool UseBMI2 = Bmi2.IsSupported;

    // capacity of the buffer in movegen. also used in MoveOrder and Perft
    internal const int MoveBufferSize = 128;
    
    // files may be indexed (sq % 8) or preferably (sq & 7)
    // ranks are indexed (sq / 8) or (sq >> 3)
    internal const string Files  = "abcdefgh";
    internal const string Pieces = "pnbrqk";
    
    internal static readonly ulong[] FileMask = [
        0x0101010101010101, 0x0202020202020202, 0x0404040404040404, 
        0x0808080808080808, 0x1010101010101010, 0x2020202020202020, 
        0x4040404040404040, 0x8080808080808080
    ];
    
    // chessboard files excluding edge squares
    internal static readonly ulong[] RelevantFileMask = [
        0x0001010101010100, 0x0002020202020200, 0x0004040404040400, 
        0x0008080808080800, 0x0010101010101000, 0x0020202020202000, 
        0x0040404040404000, 0x0080808080808000
    ];

    internal const string License = "MIT License"
                                    + "\n\nCopyright (c) 2025 Zlomený Měsíc"
                                    + "\n\nPermission is hereby granted, free of charge, to any person obtaining a copy "
                                    + "of this software and associated documentation files (the \"Software\"), to deal "
                                    + "in the Software without restriction, including without limitation the rights "
                                    + "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell "
                                    + "copies of the Software, and to permit persons to whom the Software is "
                                    + "furnished to do so, subject to the following conditions:"
                                    + "\n\nThe above copyright notice and this permission notice shall be included in all "
                                    + "copies or substantial portions of the Software."
                                    + "\n\nTHE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR "
                                    + "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, "
                                    + "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE "
                                    + "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER "
                                    + "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, "
                                    + "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE "
                                    + "SOFTWARE.";
}