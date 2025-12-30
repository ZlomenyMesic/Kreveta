//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA1810

using Kreveta.consts;
using Kreveta.search.transpositions;

using System;

namespace Kreveta.moveorder.history.corrections;

internal static class MinorPieceCorrections {
    private static readonly short[] WhiteCorrections;
    private static readonly short[] BlackCorrections;

    static MinorPieceCorrections() {
        WhiteCorrections = new short[32768];
        BlackCorrections = new short[32768];
    }

    internal static void Clear() {
        Array.Clear(WhiteCorrections, 0, 32768);
        Array.Clear(BlackCorrections, 0, 32768);
    }
    
    internal static void Update(in Board board, short shift) {
        ulong wHash = ZobristHash.GetMinorPieceHash(in board, Color.WHITE);
        ulong bHash = ZobristHash.GetMinorPieceHash(in board, Color.BLACK);

        if (wHash != 0UL) {
            int wIndex = (int)(wHash & 32767);
            
            WhiteCorrections[wIndex] += shift;
            WhiteCorrections[wIndex] = (short)Math.Clamp((int)WhiteCorrections[wIndex], -1024, 1024);
        }

        if (bHash != 0UL) {
            int bIndex = (int)(bHash & 32767);
        
            BlackCorrections[bIndex] += shift;
            BlackCorrections[bIndex] = (short)Math.Clamp((int)BlackCorrections[bIndex], -1024, 1024);
        }
    }

    internal static short Get(in Board board) {
        ulong wHash = ZobristHash.GetMinorPieceHash(in board, Color.WHITE);
        ulong bHash = ZobristHash.GetMinorPieceHash(in board, Color.BLACK);
        
        if (wHash == 0UL || bHash == 0UL) return 0;

        int wIndex = (int)(wHash & 32767);
        int bIndex = (int)(bHash & 32767);
        
        return (short)((WhiteCorrections[wIndex] + BlackCorrections[bIndex]) / 124);
    }
}

#pragma warning restore CA1810