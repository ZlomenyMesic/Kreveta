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
    internal const int MinProbingPly = 4;

    private enum ScoreType : byte {
        UPPER_BOUND,
        LOWER_BOUND,
        EXACT
    }

    [StructLayout(LayoutKind.Explicit, Size = EntrySize)]
    private record struct Entry {
        // 8 bytes
        [FieldOffset(0)] internal ulong Hash;

        // 2 bytes
        [FieldOffset(8)] internal short Score;

        // 1 byte
        [FieldOffset(10)] internal sbyte Depth;

        // 1 byte
        [FieldOffset(11)] internal ScoreType Type;

        // 4 bytes
        [FieldOffset(12)] internal Move BestMove;
    }

    // size of a single hash entry
    internal const int EntrySize = 16;

    // size of the tt array
    internal static int TableSize = GetTableSize();

    // how many items are currently stored
    internal static int Stored = 0;

    private static Entry[] table = new Entry[TableSize];

    //private static long tt_lookups = 0;

    // tt array size - megabytes * bytes_in_mb / entry_size
    // we also limit the size as per the maximum allowed array size (2 GB)
    private static int GetTableSize() {
        return (int)Math.Min(Options.Hash * (long)1048576 / EntrySize, (long)int.MaxValue / EntrySize);
    }

    // generate an index in the tt for a specific board hash
    // key collisions can (and will) occur, so we later also check the correctness of this index
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashIndex(ulong hash) {
        uint hash32 = (uint)hash ^ (uint)(hash >> 32);
        return (int)(hash32 % TableSize);
    }

    internal static void Clear() {
        Stored = 0;

        TableSize = GetTableSize();
        table = new Entry[TableSize];
    }

    // instead of using an age value, we decrement the depths
    // in the entries stored for the next search, so they aren't
    // as important. note that this is only used in a full game
    internal static void DecrementEntryDepths() {
        //for (int i = 0; i < TT_SIZE; i++) {
        //    table[i].depth -= 3;
        //}
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Store(Board board, int depth, int ply, Window window, short score, Move bestMove) {
        Store(Zobrist.GetHash(board), depth, ply, window, score, bestMove);
    }

    internal static void Store(ulong hash, int depth, int ply, Window window, short score, Move bestMove) {

        int i = HashIndex(hash);

        // maybe an entry is already saved
        Entry existing = table[i];

        // is the index already occupied with a result from a higher depth search?
        // key collisions may also be problematic - multiple positions
        // could have an identical key (i don't really care, though)
        if (existing.Hash != default && existing.Depth > depth) {
            return;
        }

        Entry entry = new() {
            Hash = hash,
            Depth = (sbyte)depth,
            BestMove = bestMove
        };

        //a checkmate score is reduced by the number of plies from the root so that shorter mates are preferred
        //but when we talk about a position being 'mate in X' then X is independent of the root distance. So we store
        //the score relative to the position by adding the current ply to the encoded mate distance (from the root).
        // (taken from MinimalChessEngine)
        if (Eval.IsMateScore(score)) {

            //
            //
            // TODO - MAYBE FIX THIS
            //
            //
            score += (short)(Math.Sign(score) * ply);
        }

        if (score >= window.beta) {
            entry.Type = ScoreType.UPPER_BOUND;
            entry.Score = window.beta;

        } else if (score <= window.alpha) {
            entry.Type = ScoreType.LOWER_BOUND;
            entry.Score = window.alpha;

        } else {
            entry.Type = ScoreType.EXACT;
            entry.Score = score;
        }

        // if we aren't overwriting an existing entry, increase the counter
        if (existing.Hash == default) 
            Stored++;

        // store the new entry or overwrite the old one if allowed
        table[i] = entry;
    }

    internal static bool GetBestMove(Board board, out Move bestMove) {
        ulong hash = Zobrist.GetHash(board);
        bestMove = default;

        int i = HashIndex(hash);
        Entry entry = table[i];

        // the position isn't saved
        if (entry.Hash != hash)
            return false;

        bestMove = entry.BestMove;
        return bestMove != default;
    }

    internal static bool GetScore(Board board, int depth, int ply, Window window, out short score) {
        ulong hash = Zobrist.GetHash(board);
        score = 0;

        int i = HashIndex(hash);
        Entry entry = table[i];

        // once again the position is not yet saved
        if (entry.Hash != hash)
            return false;

        // the position is saved, but it's depth is shallower than ours
        if (entry.Depth <= depth)
            return false;

        score = entry.Score;

        // a checkmate score is reduced by the number of plies from the root so that shorter mates are preferred
        // but when we store it in the TT the score is made relative to the current position. So when we want to 
        // retrieve the score we have to subtract the current ply to make it relative to the root again.
        // (and again MinimalChessEngine inspiration)
        if (Eval.IsMateScore(score)) {
            //
            // ANOTHER FIX HERE
            //
            score -= (short)(Math.Sign(score) * ply);
            return true;
        }

        if (entry.Type == ScoreType.EXACT) return true;
        if (entry.Type == ScoreType.LOWER_BOUND && score <= window.alpha) {
            //score = window.alpha;
            return true;
        }
        if (entry.Type == ScoreType.UPPER_BOUND && score >= window.beta) {
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
    internal static int Hashfull() {
        return (int)((float)Stored / TableSize * 1000);
    }
}