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
    private static readonly int[][] CaptureScores  = new int[64][];
    private static readonly int[][] ButterflyBoard = new int[64][];

    internal static void Init() {
        for (int i = 0; i < 64; i++) {
            CaptureScores[i]  = new int[64];
            ButterflyBoard[i] = new int[64];
        }
    }
    
    internal static void Shrink() {
        Parallel.For(0, 64, i => {
            Parallel.For(0, 64, j => {
                CaptureScores[i][j]  /= 2;
                ButterflyBoard[i][j] /= 3;
            });
        });
    }

    internal static void Clear() {
        Parallel.For(0, 64, i => {
            Array.Clear(CaptureScores[i]);
            Array.Clear(ButterflyBoard[i]);
        });
    }
    
    internal static void ChangeRep(Move move, int weight) {
        int start = move.Start;
        int end   = move.End;
        
        CaptureScores[start][end] += weight * Math.Abs(weight);
        ButterflyBoard[start][end]++;
    }

    internal static int GetRep(Move move) {
        int start = move.Start;
        int end   = move.End;

        int q  = CaptureScores[start][end];
        int bf = ButterflyBoard[start][end];
        
        return bf == 0 ? 0 : 10 * q / bf;
    }
}