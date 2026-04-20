//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;

using System;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming

namespace Kreveta.moveorder.history;

// this is the exact same idea and very similar code to quiet history,
// except it's applied to captures. the logic is truly the same, so
// look at quiet history instead to understand it
internal static class CaptureHistory {
    private static readonly int[] CaptureScores  = new int[64 * 64];
    private static readonly int[] ButterflyBoard = new int[64 * 64];
    
    internal static void Shrink() {
        for (int i = 0; i < 64 * 64; i++) {
            CaptureScores[i]  /= 2;
            ButterflyBoard[i] /= 3;
        }
    }

    internal static void Clear() {
        Array.Clear(CaptureScores,  0, CaptureScores.Length);
        Array.Clear(ButterflyBoard, 0, ButterflyBoard.Length);
    }
    
    internal static void ChangeRep(Move move, int weight) {
        int start = move.Start;
        int end   = move.End;
        
        CaptureScores [start * 64 + end] += weight * Math.Abs(weight);
        ButterflyBoard[start * 64 + end]++;
    }

    internal static int GetRep(Move move) {
        int start = move.Start;
        int end   = move.End;

        int q  = CaptureScores [start * 64 + end];
        int bf = ButterflyBoard[start * 64 + end];
        
        return bf == 0 ? 0 : 10 * q / bf;
    }
}