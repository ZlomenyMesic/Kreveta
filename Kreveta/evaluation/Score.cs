//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA1305

using System;
using System.Runtime.CompilerServices;

namespace Kreveta.evaluation;

internal static class Score {

    // any score above this threshold is considered "mate score"
    private const int MateScoreThreshold = 9000;

    // default mate score from which is then subtracted
    // some amount to prefer shorter mates (M1 = 9998)
    private const int MateScoreDefault = 9999;

    // creates a new mate score relative to the number of plies
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short GetMateScore(int ply) => (short)(-MateScoreDefault + ply);

    // checks whether the score falls above the mate score threshold
    [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static bool IsMate(int score) => Math.Abs(score) > MateScoreThreshold;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static bool IsWin(int score)  => score > MateScoreThreshold;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static bool IsLoss(int score) => score < -MateScoreThreshold;

    // when printing a mate score, we prefer the "mate in x" format,
    // so we convert the score to the number of plies until mate
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetMateInX(int score) {
        int x = MateScoreDefault - Math.Abs(score);
        return x * Math.Sign(score);
    }
    
    // to avoid weird loops and cycles when evaluating drawing shuffling
    // positions, the draw score for 3-fold repetition is deterministically
    // altered to be a bit more noisy, and break these cycles
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short GetNoisyDrawScore(ulong nodes) => (short)(-1 + (int)(nodes & 0x2));

    // maximum non-mate score that can be achieved (in centipawns)
    private const int NonMateScoreLimit = 1000;

    // some GUIs (such as Cutechess) are unable to print really large
    // scores that aren't mate, e.g. Cutechess only shows evaluation
    // below 15 pawns correctly, and above that all scores look the
    // same. for this reason, when printing a score (that is not a mate
    // score), we use the tangent hyperbolic function to make all scores
    // fall into a set interval.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int LimitScore(int score)
        => (int)Math.Round(NonMateScoreLimit * (float)Math.Tanh((double)score / 1125), 0);

    // converts a centipawn score to pawns, adds +/- signs, returns as string
    internal static string ToRegular(int score) {
        string sign = score > 0 ? "+" : score < 0 ? "" : " ";
        
        // make negative scores red and positive scores green
        return $"{score switch { < 0 => "\e[91m", 0 => "", > 0 => "\e[92m"}}" 
             + $"{sign}{MathF.Round(score / 100f, 2):F2}"
             + $"\e[0m";
    }
}

#pragma warning restore CA1305