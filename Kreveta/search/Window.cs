/*
 * |============================|
 * |                            |
 * |    Kreveta chess engine    |
 * | engineered by ZlomenyMesic |
 * | -------------------------- |
 * |      started 4-3-2025      |
 * | -------------------------- |
 * |                            |
 * | read README for additional |
 * | information about the code |
 * |    and usage that isn't    |
 * |  included in the comments  |
 * |                            |
 * |============================|
 */

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kreveta.search;

[StructLayout(LayoutKind.Explicit, Size = 4)]
internal struct Window {
    internal static readonly Window Infinite = new(short.MinValue, short.MaxValue);

    // floor/lower bound
    // moves under alpha are too bad
    [FieldOffset(0)] internal short alpha;

    // ceiling/upper bound
    // moves above beta are too good and won't be allowed by the opponent
    [FieldOffset(2)] internal short beta;

    internal Window(short alpha, short beta) {
        this.alpha = alpha;
        this.beta = beta;
    }

    // makes the window smaller by raising alpha or reducing beta, depending on the color
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryCutoff(short score, int col) {

        // raising alpha (floor)
        if (col == 0) {

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


    // returns the "floor" (alpha)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Window LowerBound()
        => new(alpha, (short)(alpha + 1));

    // returns the "ceiling" (beta)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Window UpperBound()
    => new((short)(beta - 1), beta);

    // returns the "floor" for a specific color
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Window GetLowerBound(int col)
        => col == 0 ? LowerBound() : UpperBound();

    // returns the "ceiling" for a specific color
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Window GetUpperBound(int col)
        => col == 0 ? UpperBound() : LowerBound();


    // if a move fails low, it is not good enough to improve our position, so we won't play it
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly bool FailsLow(short score, int col) 
        => col == 0 
        ? (score <= alpha) 
        : (score >= beta);


    // if a move fails high, it is "too good" and the opponen won't allow it to be played
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly bool FailsHigh(short score, int col)
        => col == 0 
        ? (score >= beta) 
        : (score <= alpha);

    // return the "ensured" score for a color
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly short GetBoundScore(int col) 
        => col == 0 ? alpha : beta;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly bool CanFailHigh(int col)
        => col == 0 
        ? (beta < short.MaxValue) 
        : (alpha > short.MinValue);
}
