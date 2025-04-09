//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kreveta.search;

[StructLayout(LayoutKind.Explicit, Size = 4)]
internal struct Window {
    internal static readonly Window Infinite = new(short.MinValue, short.MaxValue);

    // floor/lower bound
    // moves under alpha are too bad
    [field: FieldOffset(0)] internal short alpha;

    // ceiling/upper bound
    // moves above beta are too good and won't be allowed by the opponent
    [field: FieldOffset(2)] internal short beta;

    internal Window(short alpha, short beta) {
        this.alpha = alpha;
        this.beta = beta;
    }

    // makes the window smaller by raising alpha or reducing beta, depending on the color
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryCutoff(short score, Color col) {

        // raising alpha (floor)
        if (col == Color.WHITE) {

            // fail low
            if (score <= alpha)
                return false;

            alpha = score;

            // cutoff?
            return alpha >= beta;
        }

        // reducing beta (ceiling)
        else {

            // fail high
            if (score >= beta)
                return false; 

            beta = score;

            // cutoff?
            return beta <= alpha;
        }
    }
}
