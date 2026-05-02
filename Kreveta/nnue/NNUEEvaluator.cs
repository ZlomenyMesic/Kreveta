//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;
using Kreveta.nnue.approx;

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
    
    internal const int QScale = 1024;

    private readonly short[] _accWhite;
    private readonly short[] _accBlack;

    internal short Score;
    
    // pre-allocated pool of evaluators - one slot per ply.
    // avoids repeated heap allocations during board clones
    private const int PoolSize = 128;
    private static NNUEEvaluator[] _pool = null!;

    internal static void Init() {
        _pool = new NNUEEvaluator[PoolSize];
        
        for (int i = 0; i < PoolSize; i++)
            _pool[i] = new NNUEEvaluator();
    }
    
    // private default constructor used to populate the pool
    private NNUEEvaluator() {
        _accWhite = new short[EmbedDims];
        _accBlack = new short[EmbedDims];
    }
    
    internal NNUEEvaluator(in NNUEEvaluator other) {
        _accWhite = new short[EmbedDims];
        _accBlack = new short[EmbedDims];
        
        fixed (short* src = other._accWhite)
        fixed (short* dst = _accWhite)
            Unsafe.CopyBlock(dst, src, EmbedDims * 2);
        
        fixed (short* src = other._accBlack)
        fixed (short* dst = _accBlack)
            Unsafe.CopyBlock(dst, src, EmbedDims * 2);
        
        Score = other.Score;
    }

    internal NNUEEvaluator(in Board board) {
        _accWhite = new short[EmbedDims];
        _accBlack = new short[EmbedDims];

        Span<int> whiteFeat = stackalloc int[32];
        Span<int> blackFeat = stackalloc int[32];
        int count = ExtractFeatures(in board, whiteFeat, blackFeat);

        for (int i = 0; i < count; i++) {
            AddEmbedding(whiteFeat[i], _accWhite);
            AddEmbedding(blackFeat[i], _accBlack);
        }

        UpdateEvaluation(board.SideToMove, count + 2);
    }

    // copy evaluator data from src to the pool, and return the pool entry
    internal static NNUEEvaluator GetFromPool(in NNUEEvaluator src, int ply) {
        NNUEEvaluator inst = _pool[ply];
        
        fixed (short* s = src._accWhite)
        fixed (short* d = inst._accWhite)
            Unsafe.CopyBlock(d, s, EmbedDims * 2);
        
        fixed (short* s = src._accBlack)
        fixed (short* d = inst._accBlack)
            Unsafe.CopyBlock(d, s, EmbedDims * 2);
        
        inst.Score = src.Score;
        return inst;
    }
    
    // update both accumulators based on a move played on the board. directly
    // computing the forward pass and updating the evaluation is also possible
    internal void Update(in Board board, Move move, Color moved, bool updateEval) {
        int wKing = BB.LS1B(board.Pieces[5]);
        int bKing = BB.LS1B(board.Pieces[11]);

        int start = move.Start;
        int end   = move.End;
        
        PType piece = move.Piece;
        PType capt  = move.Capture;
        PType prom  = move.Promotion;

        Color opp = 1 - moved;
        
        if (piece != PType.KING) {
            
            // deactivate source (except for king moves, kings are not stored in the accumulator)
            SubEmbedding(FeatureIndex(Color.WHITE, wKing, (int)piece, moved, start), _accWhite);
            SubEmbedding(FeatureIndex(Color.BLACK, bKing, (int)piece, moved, start), _accBlack);

            // activate the piece on the destination square (once again except
            // for king moves, and this time for promotions as well)
            if (prom == PType.NONE) {
                AddEmbedding(FeatureIndex(Color.WHITE, wKing, (int)piece, moved, end), _accWhite);
                AddEmbedding(FeatureIndex(Color.BLACK, bKing, (int)piece, moved, end), _accBlack);
            }
        }

        // deactivate the captured piece
        if (capt != PType.NONE) {
            SubEmbedding(FeatureIndex(Color.WHITE, wKing, (int)capt, opp, end), _accWhite);
            SubEmbedding(FeatureIndex(Color.BLACK, bKing, (int)capt, opp, end), _accBlack);
        }
        
        switch (prom) {
            // actual promotion - activate new piece
            case PType.KNIGHT or PType.BISHOP or PType.ROOK or PType.QUEEN: {
                AddEmbedding(FeatureIndex(Color.WHITE, wKing, (int)prom, moved, end), _accWhite);
                AddEmbedding(FeatureIndex(Color.BLACK, bKing, (int)prom, moved, end), _accBlack);
                
                break;
            }

            // en passant
            case PType.PAWN: {
                int capSq = moved == Color.WHITE 
                    ? end + 8 : end - 8;
                
                // add the diagonally moved pawn
                AddEmbedding(FeatureIndex(Color.WHITE, wKing, 0, moved, end), _accWhite);
                AddEmbedding(FeatureIndex(Color.BLACK, bKing, 0, moved, end), _accBlack);
                
                // remove the oddly captured pawn
                SubEmbedding(FeatureIndex(Color.WHITE, wKing, 0, opp, capSq), _accWhite);
                SubEmbedding(FeatureIndex(Color.BLACK, bKing, 0, opp, capSq), _accBlack);
                
                break;
            }

            // castling - the king is ignored, but the rook must be updated accordingly
            case PType.KING:
                
                // start and end squares of the rook
                (int S, int E) sq = end switch {
                    62 => (63, 61),
                    58 => (56, 59),
                    6  => (7,  5),
                    _  => (0,  3)
                };

                // only update for the opposite side. the side that has
                // just castled will have its accumulator fully rebuilt
                if (moved == Color.WHITE) {
                    SubEmbedding(FeatureIndex(Color.BLACK, bKing, 3, moved, sq.S), _accBlack);
                    AddEmbedding(FeatureIndex(Color.BLACK, bKing, 3, moved, sq.E), _accBlack);
                } else {
                    SubEmbedding(FeatureIndex(Color.WHITE, wKing, 3, moved, sq.S), _accWhite);
                    AddEmbedding(FeatureIndex(Color.WHITE, wKing, 3, moved, sq.E), _accWhite);
                }
                
                break;
        }

        // king moves require accumulator rebuild (only for the moving color)
        if (piece == PType.KING)
            RebuildAccumulator(in board, moved);
        
        // compute the forward pass and update the evaluation
        if (updateEval)
            UpdateEvaluation(opp, (int)ulong.PopCount(board.Occupied));
    }

    private void RebuildAccumulator(in Board board, Color col) {
        Span<int> whiteFeat = stackalloc int[32];
        Span<int> blackFeat = stackalloc int[32];
        int count = ExtractFeatures(in board, whiteFeat, blackFeat);

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

    private static int ExtractFeatures(in Board board, Span<int> whiteFeat, Span<int> blackFeat) {
        ReadOnlySpan<ulong> pieces = board.Pieces;

        int wKing = BB.LS1B(pieces[5]);
        int bKing = BB.LS1B(pieces[11]);
        
        int count = 0;

        for (byte pt = 0; pt < 5; pt++) {
            ulong w = pieces[pt];
            ulong b = pieces[6 + pt];

            while (w != 0UL) {
                byte sq = BB.LS1BReset(ref w);
                whiteFeat[count]   = FeatureIndex(Color.WHITE, wKing, pt, Color.WHITE, sq);
                blackFeat[count++] = FeatureIndex(Color.BLACK, bKing, pt, Color.WHITE, sq);
            }
            
            while (b != 0UL) {
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
        
            if (Consts.UseAVX2) {
                for (int i = 0; i <= EmbedDims - 16; i += 16) {
                    var va = Avx.LoadVector256(accPtr + i);
                    var vb = Avx.LoadVector256(embPtr + baseIdx + i);
                    Avx.Store(accPtr + i, Avx2.Add(va, vb));
                }
            }
            else {
                for (int i = 0; i <= EmbedDims - 16; i += 16) {
                    var va = Vector256.Load(accPtr + i);
                    var vb = Vector256.Load(embPtr + baseIdx + i);
                    (va + vb).Store(accPtr + i);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SubEmbedding(int f, short[] acc) {
        fixed (short* accPtr = acc)
        fixed (short* embPtr = NNUEWeights.Embedding) {
            int baseIdx = f * EmbedDims;
        
            if (Consts.UseAVX2) {
                for (int i = 0; i <= EmbedDims - 16; i += 16) {
                    var va = Avx.LoadVector256(accPtr + i);
                    var vb = Avx.LoadVector256(embPtr + baseIdx + i);
                    Avx.Store(accPtr + i, Avx2.Subtract(va, vb));
                }
            }
            else {
                for (int i = 0; i <= EmbedDims - 16; i += 16) {
                    var va = Vector256.Load(accPtr + i);
                    var vb = Vector256.Load(embPtr + baseIdx + i);
                    (va - vb).Store(accPtr + i);
                }
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

        int bucket = Math.Min(7, pcnt / 4);

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
        
            if (Consts.UseAVX2) {
                // 1st hidden layer
                for (int j = 0; j < H1Neurons; j++) {
                    int wBase = j * H1Input;
                    var vs    = Vector256<int>.Zero;

                    for (int i = 0; i <= H1Input - 16; i += 16) {
                        var va = Avx.LoadVector256(concatPtr + i);
                        var vb = Avx.LoadVector256(h1kernelPtr + wBase + i);
                        vs     = Avx2.Add(vs, Avx2.MultiplyAddAdjacent(va, vb));
                    }

                    int sum = (VectorSum(vs) >> 10) + h1biases[j];
                    h1ActPtr[j] = (short)Math.Clamp(sum, 0, QScale);
                }
            } else {
                for (int j = 0; j < H1Neurons; j++) {
                    int wBase = j * H1Input;
                    var vs    = Vector256<int>.Zero;

                    for (int i = 0; i <= H1Input - 16; i += 16) {
                        var va = Vector256.Load(concatPtr + i);
                        var vb = Vector256.Load(h1kernelPtr + wBase + i);
                        vs    += MultiplyAddAdjacent(va, vb);
                    }

                    int sum = (VectorSum(vs) >> 10) + h1biases[j];
                    h1ActPtr[j] = (short)Math.Clamp(sum, 0, QScale);
                }
            }

            var va2 = Avx.LoadVector256(h1ActPtr);
            Vector256<int> prod;

            // 2nd hidden layer
            if (Consts.UseAVX2) {
                for (int j = 0; j < H2Neurons; j++) {
                    int wBase = j * H1Neurons;

                    var vb = Avx.LoadVector256(h2kernelPtr + wBase);
                    prod   = Avx2.MultiplyAddAdjacent(va2, vb);

                    int sum = (VectorSum(prod) >> 10) + h2biases[j];
                    h2ActPtr[j] = (short)Math.Clamp(sum, 0, QScale);
                }
            } else {
                for (int j = 0; j < H2Neurons; j++) {
                    int wBase = j * H1Neurons;
                    
                    var vb = Vector256.Load(h2kernelPtr + wBase);
                    prod   = MultiplyAddAdjacent(va2, vb);

                    int sum = (VectorSum(prod) >> 10) + h2biases[j];
                    h2ActPtr[j] = (short)Math.Clamp(sum, 0, QScale);
                }
            }

            // output layer
            Vector256<int> prod_o;

            if (Consts.UseAVX2) {
                var va_o = Avx.LoadVector256(h2ActPtr);
                var vb_o = Avx.LoadVector256(outKernelPtr);
                prod_o   = Avx2.MultiplyAddAdjacent(va_o, vb_o);
            } else {
                var va_o = Vector256.Load(h2ActPtr);
                var vb_o = Vector256.Load(outKernelPtr);
                prod_o   = MultiplyAddAdjacent(va_o, vb_o);
            }
        
            int   pred = (VectorSum(prod_o) >> 10) + outBias;
            short act  = MathApprox.FastSigmoid(pred);
        
            Score = (short)(MathApprox.FastPtCP(act) * (active == Color.WHITE ? 1 : -1));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> MultiplyAddAdjacent(Vector256<short> va, Vector256<short> vb) {
        if (Consts.UseAVX2)
            return Avx2.MultiplyAddAdjacent(va, vb);

        var (v0, v1) = Vector256.Widen(va);
        var (v2, v3) = Vector256.Widen(vb);
        var p0       = v0 * v2;
        var p1       = v1 * v3;

        return Vector256.Create(
            p0[0] + p0[1], p0[2] + p0[3], p0[4] + p0[5], p0[6] + p0[7],
            p1[0] + p1[1], p1[2] + p1[3], p1[4] + p1[5], p1[6] + p1[7]
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int VectorSum(Vector256<int> v)
        => Consts.UseAVX2 
            ? Vector128.Sum(Sse2.Add(v.GetLower(), Avx2.ExtractVector128(v, 1))) 
            : Vector128.Sum(v.GetLower() + v.GetUpper());
}

#pragma warning restore CA1810