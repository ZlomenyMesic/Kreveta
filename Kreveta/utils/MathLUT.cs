//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// ReSharper disable InconsistentNaming
namespace Kreveta.utils;

internal static partial class MathLUT {
    static MathLUT() {
        InitSigmTable();
        InitPtCPTable();
    }
}