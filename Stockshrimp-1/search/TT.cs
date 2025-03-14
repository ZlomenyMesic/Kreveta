/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.evaluation;
using Stockshrimp_1.movegen;
using System.Runtime.InteropServices;

namespace Stockshrimp_1.search {
    internal static class TT {

        // minimum ply needed to use tt
        internal const int MIN_PLY = 4;
        // this aint working

        private enum ScoreType : byte {
            UpperBound,
            LowerBound,
            Exact
        }

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        private struct Entry {
            // 2 bytes
            [FieldOffset(0)] internal short score;

            // 1 byte
            [FieldOffset(2)] internal byte depth;

            // 1 byte
            [FieldOffset(3)] internal ScoreType type;

            // 4 bytes
            [FieldOffset(4)] internal Move best_move;
        }

        private static readonly Dictionary<ulong, Entry> table = [];

        internal static void Clear() {
            table.Clear();
        }

        internal static void Store(Board b, int depth, int ply, Window window, short score, Move best_move) {
            ulong hash = Zobrist.GetHash(b);

            Entry entry = new() {
                depth = depth < 0 ? default : (byte)depth,
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

            // we try to add the entry
            // if it already exists, we overwrite it
            if (!table.TryAdd(hash, entry)) {

                // we have to make sure we are not overwriting a higher depth search result
                if (entry.depth >= table[hash].depth)
                    table[hash] = entry;
            }
        }

        internal static bool GetBestMove(Board b, out Move best_move) {
            ulong hash = Zobrist.GetHash(b);
            best_move = default;

            // the position has not yet been stored in tt
            if (!table.TryGetValue(hash, out Entry entry))
                return false;

            best_move = entry.best_move;
            return best_move != default;
        }

        internal static bool GetScore(Board b, int depth, int ply, Window window, out short score) {
            ulong hash = Zobrist.GetHash(b);
            score = 0;

            // once again position has not yet been saved
            if (!table.TryGetValue(hash, out Entry entry))
                return false;

            // the position is saved, but it's depth is shallower than ours
            if (entry.depth < depth)
                return false;

            score = entry.score;

            //a checkmate score is reduced by the number of plies from the root so that shorter mates are preferred
            //but when we store it in the TT the score is made relative to the current position. So when we want to 
            //retrieve the score we have to subtract the current ply to make it relative to the root again.
            // (and again MinimalChessEngine inspiration)
            if (Eval.IsMateScore(score))
                score -= (short)(Math.Sign(score) * ply);

            return entry.type  == ScoreType.Exact
                || (entry.type == ScoreType.LowerBound && score <= window.alpha)
                || (entry.type == ScoreType.UpperBound && score >= window.beta);
        }
    }
}