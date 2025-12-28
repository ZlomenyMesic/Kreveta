//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming

namespace Kreveta.approx;

internal static partial class MathApprox {
    private const int PtCPMin = 5;
    private const int PtCPMax = 994;

    private static short[] PtCPTable = null!;

    private static void InitPtCPTable() {
        PtCPTable = new short[PtCPMax - PtCPMin + 1];

        for (short i = PtCPMin; i <= PtCPMax; i++) {
            float p  = (float)i / 1000;
            float o  = p / (1f - p);
            float cp = MathF.Log(o) * 400;
            
            PtCPTable[i - PtCPMin] = (short)cp;
        }
    }
    
    // the python training script turns cp score of the evaluation
    // engine into a probability in range 0 to 1. this is the inverse
    // of that function that turns the prediction back to cp score
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short FastPtCP(int act) {
        return act switch {
            <= PtCPMin => -2118,
            >= PtCPMax => 2044,
            _          => PtCPTable[act - PtCPMin]
        };
    }
}