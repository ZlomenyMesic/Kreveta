//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;
using Kreveta.utils;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
// ReSharper disable InconsistentNaming

namespace Kreveta.nnue;

internal unsafe sealed class NNUEEvaluator {
    private const int EmbedDims = NNUEWeights.EmbedDims;
    private const int H1Neurons = NNUEWeights.H1Neurons;
    private const int H2Neurons = NNUEWeights.H2Neurons;
    private const int H1Input   = EmbedDims * 2;
    private const int ScaleInt  = (int)NNUEWeights.Scale;

    private readonly short[] _accWhite = new short[EmbedDims];
    private readonly short[] _accBlack = new short[EmbedDims];

    private readonly int[] _whiteFeat = new int[32];
    private readonly int[] _blackFeat = new int[32];

    internal short Score;

    internal NNUEEvaluator() { }

    internal NNUEEvaluator(in NNUEEvaluator other) {
        other._accWhite.AsSpan().CopyTo(_accWhite);
        other._accBlack.AsSpan().CopyTo(_accBlack);
        Score = other.Score;
    }

    internal NNUEEvaluator(in Board board) 
        => Update(in board);

    private void Update(in Board board) {
        Array.Clear(_accWhite);
        Array.Clear(_accBlack);

        int count = ExtractFeatures(in board);

        for (int i = 0; i < count; i++) {
            AddEmbedding(_whiteFeat[i], _accWhite);
            AddEmbedding(_blackFeat[i], _accBlack);
        }

        UpdateEvaluation(board.Color, count + 2);
    }

    internal void Update(in Board board, Move move, Color moved) {
        int wKing = BB.LS1B(board.Pieces[5]);
        int bKing = BB.LS1B(board.Pieces[11]);

        int start = move.Start;
        int end   = move.End;
        
        PType piece = move.Piece;
        PType capt  = move.Capture;
        PType prom  = move.Promotion;

        Color opp = moved == Color.WHITE 
            ? Color.BLACK : Color.WHITE;

        // deactivate source (except for king moves,
        // kings are not stored in the accumulator)
        if (piece != PType.KING)
            Deactivate(wKing, bKing, (int)piece, moved, start);

        // deactivate the captured piece
        if (capt != PType.NONE)
            Deactivate(wKing, bKing, (int)capt, opp, end);

        // activate the piece on the destination square
        // (once again except for king moves and this
        // time for promotions as well)
        if (piece != PType.KING && prom == PType.NONE) {
            Activate(wKing, bKing, (int)piece, moved, end);
        }
        
        else switch (prom) {
            // actual promotion - activate new piece
            case PType.KNIGHT or PType.BISHOP or PType.ROOK or PType.QUEEN: 
                Activate(wKing, bKing, (int)prom, moved, end); break;

            // en passant - deactivate the oddly captured pawn
            // and activate the moved pawn on its new square
            case PType.PAWN: {
                int capSq = moved == Color.WHITE 
                    ? end + 8 : end - 8;
                
                Activate(  wKing, bKing, 0, moved, end);
                Deactivate(wKing, bKing, 0, opp,   capSq);
                break;
            }

            // castling - the king is ignored, but the
            // rook has to be updated accordingly
            case PType.KING:
                switch (end) {
                    case 62: Deactivate(wKing, bKing, 3, moved, 63); Activate(wKing, bKing, 3, moved, 61); break;
                    case 58: Deactivate(wKing, bKing, 3, moved, 56); Activate(wKing, bKing, 3, moved, 59); break;
                    case 6:  Deactivate(wKing, bKing, 3, moved, 7);  Activate(wKing, bKing, 3, moved, 5);  break;
                    case 2:  Deactivate(wKing, bKing, 3, moved, 0);  Activate(wKing, bKing, 3, moved, 3);  break;
                }
                break;
        }

        // king moves require accumulator rebuild
        // (only for the color that moved the king)
        if (piece == PType.KING)
            RebuildAccumulator(in board, moved);

        UpdateEvaluation(opp, (int)ulong.PopCount(board.Occupied));
    }

    private void RebuildAccumulator(in Board board, Color col) {
        int count = ExtractFeatures(in board);

        if (col == Color.WHITE) {
            Array.Clear(_accWhite);
            for (int i = 0; i < count; i++)
                AddEmbedding(_whiteFeat[i], _accWhite);
        }
        else {
            Array.Clear(_accBlack);
            for (int i = 0; i < count; i++)
                AddEmbedding(_blackFeat[i], _accBlack);
        }
    }

    private int ExtractFeatures(in Board board) {
        int wKing = BB.LS1B(board.Pieces[5]);
        int bKing = BB.LS1B(board.Pieces[11]);

        int count = 0;
        ReadOnlySpan<ulong> pieces = board.Pieces;

        for (byte pt = 0; pt < 5; pt++) {
            ulong w = pieces[pt];
            ulong b = pieces[6 + pt];

            while (w != 0) {
                byte sq = BB.LS1BReset(ref w);
                _whiteFeat[count]   = FeatureIndex(Color.WHITE, wKing, pt, Color.WHITE, sq);
                _blackFeat[count++] = FeatureIndex(Color.BLACK, bKing, pt, Color.WHITE, sq);
            }
            
            while (b != 0) {
                byte sq = BB.LS1BReset(ref b);
                _whiteFeat[count]   = FeatureIndex(Color.WHITE, wKing, pt, Color.BLACK, sq);
                _blackFeat[count++] = FeatureIndex(Color.BLACK, bKing, pt, Color.BLACK, sq);
            }
        }
        
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddEmbedding(int idx, short[] acc) {
        ref short accRef = ref MemoryMarshal.GetArrayDataReference(acc);
        ref short embRef = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.Embedding);

        int baseIdx = idx * EmbedDims;
        for (int i = 0; i <= EmbedDims - 16; i += 16) {
            var va = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref accRef, i)));
            var vb = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref embRef, baseIdx + i)));
            var vr = Avx2.Add(va, vb);
            
            Avx.Store((short*)Unsafe.AsPointer(ref Unsafe.Add(ref accRef, i)), vr);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SubEmbedding(int idx, short[] acc) {
        ref short accRef = ref MemoryMarshal.GetArrayDataReference(acc);
        ref short embRef = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.Embedding);

        int baseIdx = idx * EmbedDims;
        for (int i = 0; i <= EmbedDims - 16; i += 16) {
            var va = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref accRef, i)));
            var vb = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref embRef, baseIdx + i)));
            var vr = Avx2.Subtract(va, vb);
            
            Avx.Store((short*)Unsafe.AsPointer(ref Unsafe.Add(ref accRef, i)), vr);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Activate(int wK, int bK, int p, Color c, int sq) {
        int w = FeatureIndex(Color.WHITE, wK, p, c, sq);
        int b = FeatureIndex(Color.BLACK, bK, p, c, sq);
        
        AddEmbedding(w, _accWhite);
        AddEmbedding(b, _accBlack);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Deactivate(int wK, int bK, int p, Color c, int sq) {
        int w = FeatureIndex(Color.WHITE, wK, p, c, sq);
        int b = FeatureIndex(Color.BLACK, bK, p, c, sq);
        
        SubEmbedding(w, _accWhite);
        SubEmbedding(b, _accBlack);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FeatureIndex(Color perspective, int kingSq, int pieceType, Color col, int sq) {
        int colBit = col == Color.WHITE ? 0 : 1;
        
        if (perspective == Color.WHITE) {
            kingSq ^= 56;
            sq     ^= 56;
        } 
        else colBit ^= 1;
        
        return kingSq * 640 + (pieceType * 2 + colBit) * 64 + sq;
    }

    internal void UpdateEvaluation(Color active, int pcnt) {
        Span<short> concat = stackalloc short[H1Input];

        if (active == Color.WHITE) {
            _accWhite.AsSpan().CopyTo(concat[..EmbedDims]);
            _accBlack.AsSpan().CopyTo(concat[EmbedDims..]);
        } else {
            _accBlack.AsSpan().CopyTo(concat[..EmbedDims]);
            _accWhite.AsSpan().CopyTo(concat[EmbedDims..]);
        }

        int subnet = Math.Clamp(pcnt / 8, 0, 3);

        ref short concatRef    = ref MemoryMarshal.GetReference(concat);
        ref short h1kernelRef  = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.H1Kernels[subnet]);
        ref short h2kernelRef  = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.H2Kernels[subnet]);
        ref short outKernelRef = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.OutputKernel);

        ReadOnlySpan<int> h1biases = NNUEWeights.H1Biases[subnet];
        ReadOnlySpan<int> h2biases = NNUEWeights.H2Biases[subnet];

        Span<short> h1activation = stackalloc short[H1Neurons];
        Span<short> h2activation = stackalloc short[H2Neurons];

        // ---- Layer 1
        for (int j = 0; j < H1Neurons; j++) {
            int wBase = j * H1Input;
            var vs = Vector256<int>.Zero;

            for (int i = 0; i <= H1Input - 16; i += 16) {
                var va   = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref concatRef, i)));
                var vb   = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref h1kernelRef, wBase + i)));
                var prod = Avx2.MultiplyAddAdjacent(va, vb);
                
                vs = Avx2.Add(vs, prod);
            }

            int sum = HorizontalAdd(vs) / ScaleInt + h1biases[j];
            h1activation[j] = ClippedReLU(sum);
        }

        ref short h1Ref = ref MemoryMarshal.GetReference(h1activation);

        // ---- Layer 2
        for (int j = 0; j < H2Neurons; j++) {
            int wBase = j * H1Neurons;
            var vs = Vector256<int>.Zero;

            for (int i = 0; i <= H1Neurons - 16; i += 16) {
                var va   = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref h1Ref, i)));
                var vb   = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref h2kernelRef, wBase + i)));
                var prod = Avx2.MultiplyAddAdjacent(va, vb);
                
                vs = Avx2.Add(vs, prod);
            }

            int sum = HorizontalAdd(vs) / ScaleInt + h2biases[j];
            h2activation[j] = ClippedReLU(sum);
        }

        ref short h2Ref = ref MemoryMarshal.GetReference(h2activation);

        // ---- Output
        var vs3 = Vector256<int>.Zero;
        for (int i = 0; i <= H2Neurons - 16; i += 16) {
            var va = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref h2Ref, i)));
            var vb = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref outKernelRef, i)));
            
            vs3 = Avx2.Add(vs3, Avx2.MultiplyAddAdjacent(va, vb));
        }

        int pred = HorizontalAdd(vs3) / ScaleInt + NNUEWeights.OutputBias;
        
        float fp  = pred / NNUEWeights.Scale;
        float act = MathLUT.FastSigmoid(fp);
        
        Score = (short)(ProbToScore(act) * (active == Color.WHITE ? 1 : -1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HorizontalAdd(Vector256<int> v) {
        var high = Avx2.ExtractVector128(v, 1);
        var low  = v.GetLower();
        var sum  = Sse2.Add(low.AsInt32(), high.AsInt32());
        
        var sh = Sse2.Shuffle(sum, 0x4E);
        
        sum = Sse2.Add(sum, sh);
        sh  = Sse2.Shuffle(sum, 0xB1);
        sum = Sse2.Add(sum, sh);
        
        return sum.ToScalar();
    }

    // ClippedReLU is the activation function used throughout the
    // subnets' hidden layers. in python, it clamps all values into
    // 0-1, but here, due to quantization, 0-Scale range is used
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short ClippedReLU(int x) {
        // zero negatives - the second expression
        // is equal to zero if x is negative
        int z = x & ~(x >> 31);

        // detect overflow - if the subtraction is
        // negative, then over becomes all ones
        int over = z - ScaleInt >> 31;
        
        // now some magic happens. i can't explain
        return (short)(z & over | ScaleInt & ~over);
    }

    // the python training script turns cp score of the evaluation
    // engine into a probability in range 0 to 1. this is the inverse
    // of that function that turns the prediction back to cp score
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short ProbToScore(float p) {
        float o  = p / (1f - p);
        float ln = MathLUT.FastLn(o);
        
        return (short)(ln * 400f);
    }
}

