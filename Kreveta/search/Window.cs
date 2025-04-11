//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kreveta.search;

[StructLayout(LayoutKind.Explicit, Size = 2 * sizeof(short))]
internal struct Window {
    internal static readonly Window Infinite = new(short.MinValue, short.MaxValue);

    // floor/lower bound
    // moves under alpha are too bad
    [Required]
    [field: FieldOffset(0)] 
    internal short Alpha;

    // ceiling/upper bound
    // moves above beta are too good and won't be allowed by the opponent
    [Required]
    [field: FieldOffset(sizeof(short))] 
    internal short Beta;

    internal Window(short alpha, short beta) {
        Alpha = alpha;
        Beta = beta;
    }

    // makes the window smaller by raising alpha or reducing beta, depending on the color
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryCutoff(short score, Color col) {

        // raising alpha (floor)
        if (col == Color.WHITE) {

            // fail low
            if (score <= Alpha)
                return false;

            Alpha = score;

            // cutoff?
            return Alpha >= Beta;
        }

        // reducing beta (ceiling)
        else {

            // fail high
            if (score >= Beta)
                return false; 

            Beta = score;

            // cutoff?
            return Beta <= Alpha;
        }
    }
}
