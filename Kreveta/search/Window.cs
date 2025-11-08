//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kreveta.search;

// the search window - holds the alpha and beta values, which are
// the pillars of PVS. they set a range/interval, in which both
// sides should be okay with their scores
[StructLayout(LayoutKind.Explicit, Size = 2 * sizeof(short))]
internal ref struct Window {
    internal static Window Infinite 
        => new(short.MinValue, short.MaxValue);

    // floor/lower bound, which acts as the upper bound for black
    // scores under alpha are bad for white and too good for black
    [field: FieldOffset(0)] 
    internal short Alpha;

    // ceiling/upper bound, which acts as the lower bound for black
    // scores above beta are too good for white and bad for black
    [field: FieldOffset(2)]
    internal short Beta;

    internal Window(short alpha, short beta) {
        Alpha = alpha;
        Beta  = beta;
    }

    // makes the window smaller by raising alpha or reducing beta, depending on the color
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryCutoff(short score, Color col) {

        // raising alpha (floor)
        if (col == Color.WHITE) {

            // fail low - we cannot raise the floor
            if (score <= Alpha)
                return false;

            // otherwise set the new lower bound
            Alpha = score;

            // cutoff?
            return Alpha >= Beta;
        }

        // reducing beta (ceiling)
        
        // fail low for black
        if (score >= Beta)
            return false; 

        // once again, set the new upper bound
        Beta = score;

        // cutoff?
        return Alpha >= Beta;
    }
}
