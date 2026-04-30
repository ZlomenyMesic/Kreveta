//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.nnue.approx;

internal static partial class MathApprox {

    // minimum and maximum probabilities. the sigmoid activation of the
    // output layer rarely exceeds these values, and it is clamped into
    // this range, so there's no precision loss here
    private const int PtCPMin = 5;
    private const int PtCPMax = 994;

    private static short[] PtCPTable = null!;

    private static void InitPtCPTable() {
        PtCPTable = new short[PtCPMax - PtCPMin + 1];

        // now for each probability, we calculate the inverse of the
        // sigmoid function, which converts it back to centipawn score
        for (short i = PtCPMin; i <= PtCPMax; i++) {
            float p  = i / 1000f;
            float o  = p / (1f - p);
            float cp = MathF.Log(o) * 400f;
            
            PtCPTable[i - PtCPMin] = (short)cp;
        }
    }
    
    // the python training script turns cp score of the evaluation
    // engine into a probability in range 0 to 1. this is the inverse
    // of that function that turns the prediction back to cp score
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short FastPtCP(int act) {
        return act switch {
            <= PtCPMin => -2118, // table[0] - 1
            >= PtCPMax =>  2044, // table[^1] + 1
            _          =>  PtCPTable[act - PtCPMin]
        };
    }
}