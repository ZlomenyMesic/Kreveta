//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using Kreveta.movegen;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kreveta.search;

internal static class TT {

    // minimum ply needed to use tt
    internal const int MIN_PLY = 6;
    // this aint working

    private enum ScoreType : byte {
        UpperBound,
        LowerBound,
        Exact
    }

    [StructLayout(LayoutKind.Explicit, Size = ENTRY_SIZE)]
    private record struct Entry {
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

    // size of a single hash entry
    internal const int ENTRY_SIZE = 16;

    // size of the tt array
    internal static int TT_SIZE = TTSize();

    // how many items are currently stored
    internal static long STORED = 0;

    private static Entry[] table = new Entry[TT_SIZE];

    //private static long tt_lookups = 0;

    // tt array size - megabytes * bytes_in_mb / entry_size
    // we also limit the size as per the maximum allowed array size (2 GB)
    private static int TTSize() {
        return (int)Math.Min(Options.Hash * (long)1048576 / ENTRY_SIZE, (long)int.MaxValue / ENTRY_SIZE);
    }

    // generate an index in the tt for a specific board hash
    // key collisions can (and will) occur, so we later also check the correctness of this index
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int TTIndex(ulong hash) {
        uint hash32 = (uint)hash ^ (uint)(hash >> 32);
        return (int)(hash32 % TT_SIZE);
    }

    internal static void Clear() {
        STORED = 0;

        TT_SIZE = TTSize();
        table = new Entry[TT_SIZE];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Store(Board board, int depth, int ply, Window window, short score, Move best_move) {
        Store(Zobrist.GetHash(board), depth, ply, window, score, best_move);
    }

    internal static void Store(ulong hash, int depth, int ply, Window window, short score, Move best_move) {

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

        // if we aren't overwriting an existing entry, increase the counter
        if (existing.hash == default) STORED++;

        // store the new entry or overwrite the old one if allowed
        table[i] = entry;
    }

    internal static bool GetBestMove(Board board, out Move best_move) {
        ulong hash = Zobrist.GetHash(board);
        best_move = default;

        int i = TTIndex(hash);
        Entry entry = table[i];

        // the position isn't saved
        if (entry.hash != hash)
            return false;

        best_move = entry.best_move;
        return best_move != default;
    }

    internal static bool GetScore(Board board, int depth, int ply, Window window, out short score) {
        ulong hash = Zobrist.GetHash(board);
        score = 0;

        int i = TTIndex(hash);
        Entry entry = table[i];

        // once again the position is not yet saved
        if (entry.hash != hash)
            return false;

        // the position is saved, but it's depth is shallower than ours
        if (entry.depth <= depth)
            return false;

        score = entry.score;

        // a checkmate score is reduced by the number of plies from the root so that shorter mates are preferred
        // but when we store it in the TT the score is made relative to the current position. So when we want to 
        // retrieve the score we have to subtract the current ply to make it relative to the root again.
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


    // hashfull tells us how filled is the hash table
    // in permill (entries per thousand). this number
    // is sent regularly to the GUI, which allows it
    // sent us a hash table clearing command (option)
    // in case we are too full to free some memory
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int HashFull() {
        return (int)((float)STORED / TT_SIZE * 100);
    }
}