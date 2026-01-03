//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;
using Kreveta.approx;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
// ReSharper disable InconsistentNaming

namespace Kreveta.nnue;

internal unsafe sealed class NNUEEvaluator {
    private const int EmbedDims = NNUEWeights.EmbedDims;
    private const int H1Neurons = NNUEWeights.H1Neurons;
    private const int H2Neurons = NNUEWeights.H2Neurons;
    private const int H1Input   = EmbedDims * 2;

    private static readonly int[] BucketTable = [
        -1, -1,        // shouldn't ever happen
        0, 0, 0, 0, 0, // very late endgames
        1, 1, 1, 1, 1,
        2, 2, 2, 2,
        3, 3, 3, 3,
        4, 4, 4, 4,
        5, 5, 5,
        6, 6, 6,       // late opening/early middlegame
        7, 7, 7        // early opening, needs extra precision
    ];
    
    internal const int QScale = 1024;

    private readonly short[] _accWhite;
    private readonly short[] _accBlack;

    internal short Score;
    
    internal NNUEEvaluator(in NNUEEvaluator other) {
        _accWhite = new short[EmbedDims];
        _accBlack = new short[EmbedDims];
        
        fixed (short* src = other._accWhite)
        fixed (short* dst = _accWhite)
            Unsafe.CopyBlock(dst, src, NNUEWeights.EmbedDims * 2);
        
        fixed (short* src = other._accBlack)
        fixed (short* dst = _accBlack)
            Unsafe.CopyBlock(dst, src, NNUEWeights.EmbedDims * 2);
        
        Score = other.Score;
    }

    internal NNUEEvaluator(in Board board) {
        _accWhite = new short[EmbedDims];
        _accBlack = new short[EmbedDims];

        int count = ExtractFeatures(in board, out var whiteFeat, out var blackFeat);

        for (int i = 0; i < count; i++) {
            AddEmbedding(whiteFeat[i], _accWhite);
            AddEmbedding(blackFeat[i], _accBlack);
        }

        UpdateEvaluation(board.SideToMove, count + 2);
    }
    

    internal void Update(in Board board, Move move, Color moved) {
        var wAdd = stackalloc int[2];
        var wRem = stackalloc int[2];
        var bAdd = stackalloc int[2];
        var bRem = stackalloc int[2];

        int wac = 0, wrc = 0, bac = 0, brc = 0;
        
        int wKing = BB.LS1B(board.Pieces[5]);
        int bKing = BB.LS1B(board.Pieces[11]);

        int start = move.Start;
        int end   = move.End;
        
        PType piece = move.Piece;
        PType capt  = move.Capture;
        PType prom  = move.Promotion;

        Color opp = moved == Color.WHITE 
            ? Color.BLACK : Color.WHITE;
        
        if (piece != PType.KING) {
            // deactivate source (except for king moves,
            // kings are not stored in the accumulator)
            wRem[wrc++] = FeatureIndex(Color.WHITE, wKing, (int)piece, moved, start);
            bRem[brc++] = FeatureIndex(Color.BLACK, bKing, (int)piece, moved, start);

            // activate the piece on the destination square
            // (once again except for king moves and this
            // time for promotions as well)
            if (prom == PType.NONE) {
                wAdd[wac++] = FeatureIndex(Color.WHITE, wKing, (int)piece, moved, end);
                bAdd[bac++] = FeatureIndex(Color.BLACK, bKing, (int)piece, moved, end);
            }
        }

        // deactivate the captured piece
        if (capt != PType.NONE) {
            wRem[wrc++] = FeatureIndex(Color.WHITE, wKing, (int)capt, opp, end);
            bRem[brc++] = FeatureIndex(Color.BLACK, bKing, (int)capt, opp, end);
        }
        
        switch (prom) {
            // actual promotion - activate new piece
            case PType.KNIGHT or PType.BISHOP or PType.ROOK or PType.QUEEN: {
                wAdd[wac++] = FeatureIndex(Color.WHITE, wKing, (int)prom, moved, end);
                bAdd[bac++] = FeatureIndex(Color.BLACK, bKing, (int)prom, moved, end);
                break;
            }

            // en passant - deactivate the oddly captured pawn
            // and activate the moved pawn on its new square
            case PType.PAWN: {
                int capSq = moved == Color.WHITE 
                    ? end + 8 : end - 8;
                
                wAdd[wac++] = FeatureIndex(Color.WHITE, wKing, 0, moved, end);
                bAdd[bac++] = FeatureIndex(Color.BLACK, bKing, 0, moved, end);
                wRem[wrc++] = FeatureIndex(Color.WHITE, wKing, 0, opp, capSq);
                bRem[brc++] = FeatureIndex(Color.BLACK, bKing, 0, opp, capSq);
                
                break;
            }

            // castling - the king is ignored, but the
            // rook has to be updated accordingly
            case PType.KING:
                
                // start and end square of the rook
                (int, int) squares = end switch {
                    62 => (63, 61),
                    58 => (56, 59),
                    6  => (7, 5),
                    _  => (0, 3)
                };

                // only update for the opposite side. the side that has
                // just castled will have its accumulator fully rebuilt
                if (moved == Color.WHITE) {
                    bRem[brc++] = FeatureIndex(Color.BLACK, bKing, 3, moved, squares.Item1);
                    bAdd[bac++] = FeatureIndex(Color.BLACK, bKing, 3, moved, squares.Item2);
                } else {
                    wRem[wrc++] = FeatureIndex(Color.WHITE, wKing, 3, moved, squares.Item1);
                    wAdd[wac++] = FeatureIndex(Color.WHITE, wKing, 3, moved, squares.Item2);
                }
                
                break;
        }

        for (int i = 0; i < wac; i++)
            AddEmbedding(wAdd[i], _accWhite);
        
        for (int i = 0; i < bac; i++)
            AddEmbedding(bAdd[i], _accBlack);
        
        for (int i = 0; i < wrc; i++)
            SubEmbedding(wRem[i], _accWhite);
        
        for (int i = 0; i < brc; i++)
            SubEmbedding(bRem[i], _accBlack);

        // king moves require accumulator rebuild
        // (only for the color that moved the king)
        if (piece == PType.KING)
            RebuildAccumulator(in board, moved);
        
        UpdateEvaluation(opp, (int)ulong.PopCount(board.Occupied));
    }

    private void RebuildAccumulator(in Board board, Color col) {
        int count = ExtractFeatures(in board, out var whiteFeat, out var blackFeat);

        if (col == Color.WHITE) {
            Array.Clear(_accWhite);
            for (int i = 0; i < count; i++)
                AddEmbedding(whiteFeat[i], _accWhite);
        }
        else {
            Array.Clear(_accBlack);
            for (int i = 0; i < count; i++)
                AddEmbedding(blackFeat[i], _accBlack);
        }
    }

    private static int ExtractFeatures(in Board board, out Span<int> whiteFeat, out Span<int> blackFeat) {
        int wKing = BB.LS1B(board.Pieces[5]);
        int bKing = BB.LS1B(board.Pieces[11]);

        int count = (int)ulong.PopCount(board.Occupied) - 2;
        ReadOnlySpan<ulong> pieces = board.Pieces;
        
        whiteFeat = new int[count];
        blackFeat = new int[count];

        count = 0;

        for (byte pt = 0; pt < 5; pt++) {
            ulong w = pieces[pt];
            ulong b = pieces[6 + pt];

            while (w != 0) {
                byte sq = BB.LS1BReset(ref w);
                whiteFeat[count]   = FeatureIndex(Color.WHITE, wKing, pt, Color.WHITE, sq);
                blackFeat[count++] = FeatureIndex(Color.BLACK, bKing, pt, Color.WHITE, sq);
            }
            
            while (b != 0) {
                byte sq = BB.LS1BReset(ref b);
                whiteFeat[count]   = FeatureIndex(Color.WHITE, wKing, pt, Color.BLACK, sq);
                blackFeat[count++] = FeatureIndex(Color.BLACK, bKing, pt, Color.BLACK, sq);
            }
        }
        
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddEmbedding(int f, short[] acc) {
        fixed (short* accPtr = acc)
        fixed (short* embPtr = NNUEWeights.Embedding) {
            int baseIdx = f * EmbedDims;
            for (int i = 0; i <= EmbedDims - 16; i += 16) {
                var va = Avx.LoadVector256(accPtr + i);
                var vb = Avx.LoadVector256(embPtr + baseIdx + i);

                Avx.Store(accPtr + i, Avx2.Add(va, vb));
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SubEmbedding(int f, short[] acc) {
        fixed (short* accPtr = acc)
        fixed (short* embPtr = NNUEWeights.Embedding) {
            int baseIdx = f * EmbedDims;
            for (int i = 0; i <= EmbedDims - 16; i += 16) {
                var va = Avx.LoadVector256(accPtr + i);
                var vb = Avx.LoadVector256(embPtr + baseIdx + i);

                Avx.Store(accPtr + i, Avx2.Subtract(va, vb));
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FeatureIndex(Color perspective, int kingSq, int pieceType, Color col, int sq) {
        int colBit = (int)col;
        
        if (perspective == Color.WHITE) {
            kingSq ^= 56;
            sq     ^= 56;
        } 
        else colBit ^= 1;
        
        return kingSq * 640 + (pieceType * 2 + colBit << 6) + sq;
    }
    
    private void UpdateEvaluation(Color active, int pcnt) {
        Span<short> concat = stackalloc short[H1Input];

        if (active == Color.WHITE) {
            _accWhite.AsSpan().CopyTo(concat[..EmbedDims]);
            _accBlack.AsSpan().CopyTo(concat[EmbedDims..]);
        } else {
            _accBlack.AsSpan().CopyTo(concat[..EmbedDims]);
            _accWhite.AsSpan().CopyTo(concat[EmbedDims..]);
        }

        int bucket = Math.Min(7, pcnt / 4);//BucketTable[pcnt]));

        ReadOnlySpan<short> h1biases = NNUEWeights.H1Biases[bucket];
        ReadOnlySpan<short> h2biases = NNUEWeights.H2Biases[bucket];
        short               outBias  = NNUEWeights.OutputBiases[bucket];

        Span<short> h1activation = stackalloc short[H1Neurons];
        Span<short> h2activation = stackalloc short[H2Neurons];
        
        fixed (short* concatPtr    = concat)
        fixed (short* h1kernelPtr  = NNUEWeights.H1Kernels[bucket])
        fixed (short* h2kernelPtr  = NNUEWeights.H2Kernels[bucket])
        fixed (short* outKernelPtr = NNUEWeights.OutputKernels[bucket])
        fixed (short* h1ActPtr     = h1activation)
        fixed (short* h2ActPtr     = h2activation) {
            
            // 1st hidden layer
            for (int j = 0; j < H1Neurons; j++) {
                int wBase = j * H1Input;
                var vs = Vector256<int>.Zero;

                for (int i = 0; i <= H1Input - 16; i += 16) {
                    var va = Avx.LoadVector256(concatPtr + i);
                    var vb = Avx.LoadVector256(h1kernelPtr + wBase + i);
                    
                    vs = Avx2.Add(vs, Avx2.MultiplyAddAdjacent(va, vb));
                }

                int sum = (VectorSum(vs) >> 10) + h1biases[j];
                h1ActPtr[j] = (short)Math.Clamp(sum, 0, QScale);
            }

            // 2nd hidden layer
            for (int j = 0; j < H2Neurons; j++) {
                int wBase = j * H1Neurons;

                var va   = Avx.LoadVector256(h1ActPtr);
                var vb   = Avx.LoadVector256(h2kernelPtr + wBase);
                var prod = Avx2.MultiplyAddAdjacent(va, vb);

                int sum = (VectorSum(prod) >> 10) + h2biases[j];
                h2ActPtr[j] = (short)Math.Clamp(sum, 0, QScale);
            }

            // output layer
            var va_o   = Avx.LoadVector256(h2ActPtr);
            var vb_o   = Avx.LoadVector256(outKernelPtr);
            var prod_o = Avx2.MultiplyAddAdjacent(va_o, vb_o);
            
            int pred  = (VectorSum(prod_o) >> 10) + outBias;
            short act = MathApprox.FastSigmoid(pred);
            
            Score = (short)(MathApprox.FastPtCP(act) * (active == Color.WHITE ? 1 : -1));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int VectorSum(Vector256<int> v) {
        var s = Sse2.Add(v.GetLower(), Avx2.ExtractVector128(v, 1));
        return Vector128.Sum(s);
    }
}