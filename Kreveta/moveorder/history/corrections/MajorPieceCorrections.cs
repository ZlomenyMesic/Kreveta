//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA1810

using Kreveta.consts;
using Kreveta.search.transpositions;

using System;

namespace Kreveta.moveorder.history.corrections;

internal static class MajorPieceCorrections {
    private const int TableSize = 16_384;
    
    private static readonly short[] WhiteCorrections;
    private static readonly short[] BlackCorrections;

    static MajorPieceCorrections() {
        WhiteCorrections = new short[TableSize];
        BlackCorrections = new short[TableSize];
    }

    internal static void Clear() {
        Array.Clear(WhiteCorrections, 0, TableSize);
        Array.Clear(BlackCorrections, 0, TableSize);
    }
    
    internal static void Update(in Board board, short shift) {
        
        ulong wHash = ZobristHash.GetMajorPieceHash(in board, Color.WHITE);
        ulong bHash = ZobristHash.GetMajorPieceHash(in board, Color.BLACK);

        if (wHash != 0UL) {
            int wIndex = (int)(wHash & (TableSize - 1));
            
            WhiteCorrections[wIndex] += shift;
            WhiteCorrections[wIndex] = (short)Math.Clamp((int)WhiteCorrections[wIndex], -1024, 1024);
        }

        if (bHash != 0UL) {
            int bIndex = (int)(bHash & (TableSize - 1));
        
            BlackCorrections[bIndex] += shift;
            BlackCorrections[bIndex] = (short)Math.Clamp((int)BlackCorrections[bIndex], -1024, 1024);
        }
    }

    internal static int Get(in Board board) {
        ulong wHash = ZobristHash.GetMajorPieceHash(in board, Color.WHITE);
        ulong bHash = ZobristHash.GetMajorPieceHash(in board, Color.BLACK);
        
        if (wHash == 0UL || bHash == 0UL) return 0;

        int wIndex = (int)(wHash & (TableSize - 1));
        int bIndex = (int)(bHash & (TableSize - 1));
        
        return WhiteCorrections[wIndex] + BlackCorrections[bIndex];
    }
}

#pragma warning restore CA1810