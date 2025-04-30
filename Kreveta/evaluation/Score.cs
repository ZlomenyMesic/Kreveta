//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;

using System;
using System.Runtime.CompilerServices;

namespace Kreveta.evaluation;

internal static class Score {
    // any score above this threshold is considered "mate score"
    private const int MateScoreThreshold = 9000;

    // default mate score from which is then subtracted
    // some amount to prefer shorter mates (M1 = 9998)
    private const int MateScoreDefault = 9999;

    // maximum non-mate score that can be achieved (in centipawns)
    private const int NonMateScoreLimit = 1000;

    // creates a new mate score relative to the number of plies
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short GetMateScore(Color col, int ply)
        => (short)((col == Color.WHITE ? -1 : 1) * (MateScoreDefault - ply));

    // checks whether the score falls above the mate score threshold
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsMateScore(int score)
        => Math.Abs(score) > MateScoreThreshold;

    // some GUIs (such as Cutechess) are unable to print really large
    // scores that aren't mate, e.g. Cutechess only shows evaluation
    // below 15 pawns correctly, and above that all scores look the
    // same. for this reason, when printing a score (that is not a mate
    // score), we use the tangent hyperbolic function to make all scores
    // fall into a set interval.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int LimitScore(int score)
        => (int)Math.Round(NonMateScoreLimit * (float)Math.Tanh((double)score / 1125), 0);

    // when printing a mate score, we prefer the "mate in X" format,
    // so we convert the score to the number of plies until mate
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetMateInX(int score) {
        int x = MateScoreDefault - Math.Abs(score);
        return x * Math.Sign(score);
    }
}
