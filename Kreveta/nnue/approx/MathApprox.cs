//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// ReSharper disable InconsistentNaming
namespace Kreveta.nnue.approx;

// these things are used in NNUE. there are precomputed tables for sigmoid and a weird
// function converting the network output into a useful cp score. avoiding Math.Log and
// Math.Exp in this way is very beneficial, despite the extra allocated arrays
internal static partial class MathApprox {
    internal static void Init() {
        InitSigmTable();
        InitPtCPTable();
    }
}