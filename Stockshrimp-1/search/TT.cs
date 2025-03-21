/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.evaluation;
using Stockshrimp_1.movegen;
using System.Runtime.InteropServices;

namespace Stockshrimp_1.search;

internal static class TT {

    // minimum ply needed to use tt
    internal const int MIN_PLY = 5;
    // this aint working

    private enum ScoreType : byte {
        UpperBound,
        LowerBound,
        Exact
    }

    [StructLayout(LayoutKind.Explicit, Size = ENTRY_SIZE)]
    private struct Entry {
        // 8 bytes
        [FieldOffset(0)] internal ulong hash;

        // 2 bytes
        [FieldOffset(8)] internal short score;

        // 1 byte
        [FieldOffset(10)] internal byte depth;

        // 1 byte
        [FieldOffset(11)] internal ScoreType type;

        // 4 bytes
        [FieldOffset(12)] internal Move best_move;
    }

    // tt memory limit (megabytes)
    internal static int MAX_TT_MBYTES = 48;

    // size of a single hash entry
    internal const int ENTRY_SIZE = 16;

    // size of the tt array
    internal static int TT_SIZE = TTSize();

    private static readonly Entry[] table = new Entry[TT_SIZE];

    //private static long tt_lookups = 0;

    // tt array size - megabytes * bytes_in_mb / entry_size
    // we also limit the size as per the maximum allowed array size (2 GB)
    private static int TTSize() {
        return Math.Min(MAX_TT_MBYTES * 1048576 / ENTRY_SIZE, int.MaxValue / ENTRY_SIZE);
    }

    // generate an index in the tt for a specific board hash
    // key collisions can (and will) occur, so we later also check the correctness of this index
    private static int TTIndex(ulong hash) {
        return (int)(hash % (ulong)TT_SIZE);
    }

    internal static void Clear() {
        for (int i = 0; i < table.Length; i++) {
            table[i] = default;
        }
    }

    internal static void Store(Board b, int depth, int ply, Window window, short score, Move best_move) {

        ulong hash = Zobrist.GetHash(b);
        int i = TTIndex(hash);

        // maybe an entry is already saved
        Entry existing = table[i];

        // is the index already occupied with a result from a higher depth search?
        // key collisions may also be problematic - multiple positions
        // could have an identical key (i don't really care, though)
        if (existing.hash != default && existing.depth > depth) {
            return;
        }

        Entry entry = new() {
            hash = hash,
            depth = (byte)depth,
            best_move = best_move
        };

        //a checkmate score is reduced by the number of plies from the root so that shorter mates are preferred
        //but when we talk about a position being 'mate in X' then X is independent of the root distance. So we store
        //the score relative to the position by adding the current ply to the encoded mate distance (from the root).
        // (taken from MinimalChessEngine)
        if (Eval.IsMateScore(score)) {
            score += (short)(Math.Sign(score) * ply);
        }

        if (score >= window.beta) {
            entry.type = ScoreType.UpperBound;
            entry.score = window.beta;

        } else if (score <= window.alpha) {
            entry.type = ScoreType.LowerBound;
            entry.score = window.alpha;

        } else {
            entry.type = ScoreType.Exact;
            entry.score = score;
        }

        // store the new entry or overwrite the old one if allowed
        table[i] = entry;
    }

    internal static bool GetBestMove(Board b, out Move best_move) {
        ulong hash = Zobrist.GetHash(b);
        best_move = default;

        int i = TTIndex(hash);
        Entry entry = table[i];

        // the position isn't saved
        if (entry.hash != hash)
            return false;

        best_move = entry.best_move;
        return best_move != default;
    }

    internal static bool GetScore(Board b, int depth, int ply, Window window, out short score) {
        ulong hash = Zobrist.GetHash(b);
        score = 0;

        int i = TTIndex(hash);
        Entry entry = table[i];

        // once again the position is not yet saved
        if (entry.hash != hash)
            return false;

        // the position is saved, but it's depth is shallower than ours
        if (entry.depth < depth)
            return false;

        score = entry.score;

        //a checkmate score is reduced by the number of plies from the root so that shorter mates are preferred
        //but when we store it in the TT the score is made relative to the current position. So when we want to 
        //retrieve the score we have to subtract the current ply to make it relative to the root again.
        // (and again MinimalChessEngine inspiration)
        if (Eval.IsMateScore(score)) {
            score -= (short)(Math.Sign(score) * ply);
            return true;
        }

        if (entry.type == ScoreType.Exact) return true;
        if (entry.type == ScoreType.LowerBound && score <= window.alpha) {
            //score = window.alpha;
            return true;
        }
        if (entry.type == ScoreType.UpperBound && score >= window.beta) {
            //score = window.beta;
            return true;
        }

        return false;
    }
}