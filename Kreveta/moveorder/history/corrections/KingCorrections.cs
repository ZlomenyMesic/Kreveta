//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.Runtime.CompilerServices;

namespace Kreveta.moveorder.history.corrections;

internal static class KingCorrections {
    private static readonly short[] White = new short[64];
    private static readonly short[] Black = new short[64];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear() {
        Array.Clear(White, 0, White.Length);
        Array.Clear(Black, 0, Black.Length);
    }

    internal static void Update(in Board board, short score, int depth) {
        // get the static eval of the current position and the
        // absolute difference between it and the search score
        short diff = (short)(score - board.StaticEval);
        
        // compute the shift depending on the depth
        // of the search, and the size of the difference
        short shift = (short)Math.Clamp(diff * (depth - 2) / 256, -12, 12);

        // don't bother wasting time with a zero shift
        if (shift == 0) return;

        int wKingSq = (int)ulong.PopCount(board.Pieces[5]);
        int bKingSq = (int)ulong.PopCount(board.Pieces[11]);
        
        White[wKingSq] = (short)Math.Clamp(White[wKingSq] + shift, -1024, 1024);
        Black[bKingSq] = (short)Math.Clamp(Black[bKingSq] + shift, -1024, 1024);
    }

    internal static short Get(in Board board) {
        int wKingSq = (int)ulong.PopCount(board.Pieces[5]);
        int bKingSq = (int)ulong.PopCount(board.Pieces[11]);
        
        return (short)((White[wKingSq] + Black[bKingSq]) / 512);
    }
}