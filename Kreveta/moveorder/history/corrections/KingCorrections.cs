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

    internal static void Update(in Board board, short shift) {
        int wKingSq = (int)ulong.PopCount(board.Pieces[5]);
        int bKingSq = (int)ulong.PopCount(board.Pieces[11]);
        
        White[wKingSq] = (short)Math.Clamp(White[wKingSq] + shift, -1024, 1024);
        Black[bKingSq] = (short)Math.Clamp(Black[bKingSq] + shift, -1024, 1024);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short Get(in Board board) {
        int wKingSq = (int)ulong.PopCount(board.Pieces[5]);
        int bKingSq = (int)ulong.PopCount(board.Pieces[11]);
        
        return (short)((White[wKingSq] + Black[bKingSq]) / 512);
    }
}