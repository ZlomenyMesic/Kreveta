//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA1305

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

    // creates a new mate score relative to the number of plies
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short CreateMateScore(Color col, int ply)
        => (short)((MateScoreDefault - ply) * (col == Color.WHITE ? -1 : 1));

    // checks whether the score falls above the mate score threshold
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsMate(int score)
        => Math.Abs(score) > MateScoreThreshold;

    // when printing a mate score, we prefer the "mate in x" format,
    // so we convert the score to the number of plies until mate
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetMateInX(int score) {
        int x = MateScoreDefault - Math.Abs(score);
        return x * Math.Sign(score);
    }

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
        return $"{sign}{MathF.Round(score / 100f, 2):F2}";
    }
}

#pragma warning restore CA1305