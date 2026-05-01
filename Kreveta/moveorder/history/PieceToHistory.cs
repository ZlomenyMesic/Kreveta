//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;

using System;

namespace Kreveta.moveorder.history;

// unlike QuietHistory and/or CaptureHistory, PieceToHistory stores scores not using from-to
// squares, but piece-to. it only stores values from quiet moves, but retrieves values only
// for captures. this helps determine, which piece placement is optimal without the noise of
// captures, and then helps reorder captures a bit more reliably
internal static class PieceToHistory {
    private static readonly short[] Table = new short[2 * 6 * 64];
    
    internal static void Clear()
        => Array.Clear(Table, 0, Table.Length);

    internal static void Shrink() {
        for (int i = 0; i < Table.Length; i++)
            Table[i] /= 6;
    }

    private static int GetIndex(Color col, PType piece, int sq) 
        => ((int)col * 6 + (int)piece) * 64 + sq;

    internal static void Store(Color col, Move move, int weight) {
        var index = GetIndex(col, move.Piece, move.End);
        
        Table[index] += (short)weight;
        Table[index]  = (short)Math.Clamp((int)Table[index], -2048, 2048);
    }

    internal static short GetRep(Color col, Move move) {
        var index = GetIndex(col, move.Piece, move.End);
        return Table[index];
    }
}