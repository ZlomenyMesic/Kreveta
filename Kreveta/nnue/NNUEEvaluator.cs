//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA2014

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
    // NOTE: This evaluator expects NNUEWeights to have been quantized
    // to short (int16) and biases stored as int (int32). See NNUEWeights.Load.

    // Two accumulators: white / black (shorts)
    private readonly short[] _accW = new short[NNUEWeights.EmbedDims];
    private readonly short[] _accB = new short[NNUEWeights.EmbedDims];

    private readonly int[] _wFeatures = new int[32];
    private readonly int[] _bFeatures = new int[32];

    internal short Score;

    // shortcut to constants:
    private const int EmbedDims = NNUEWeights.EmbedDims;
    private const int H1Neurons = NNUEWeights.H1Neurons;
    private const int H2Neurons = NNUEWeights.H2Neurons;

    // H1 input width after concatenation (white + black)
    private const int H1Input = EmbedDims * 2;
    private const int ScaleInt = (int)NNUEWeights.Scale;

    internal NNUEEvaluator() { /* nothing */ }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal NNUEEvaluator(in NNUEEvaluator other) {
        Array.Copy(other._accW, _accW, NNUEWeights.EmbedDims);
        Array.Copy(other._accB, _accB, NNUEWeights.EmbedDims);
        Score = other.Score;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal NNUEEvaluator(in Board board)
        => Update(in board);

    private void Update(in Board board) {
        // zero accumulators
        Array.Clear(_accW, 0, NNUEWeights.EmbedDims);
        Array.Clear(_accB, 0, NNUEWeights.EmbedDims);

        // rebuild accumulator by summing embeddings for active features
        int featureCount = ExtractFeatures(in board);

        // White features -> _accW
        for (int i = 0; i < featureCount; i++) {
            UpdateFeature(_wFeatures[i], true, _accW);
            UpdateFeature(_bFeatures[i], true, _accB);
        }

        UpdateEvaluation(board.Color, featureCount + 2);
    }

    // incremental update on moves (pass in current king squares)
    internal void Update(in Board board, Move move, Color colMoved) {
        int wKing = BB.LS1B(board.Pieces[5]);
        int bKing = BB.LS1B(board.Pieces[11]);
        
        int start = move.Start;
        int end   = move.End;

        PType piece = move.Piece;
        PType capt  = move.Capture;
        PType prom  = move.Promotion;

        Color oppColor = colMoved == Color.WHITE 
            ? Color.BLACK : Color.WHITE;

        if (piece != PType.KING)
            Deactivate(wKing, bKing, (int)piece, colMoved, start);

        if (capt != PType.NONE) 
            Deactivate(wKing, bKing, (int)capt, oppColor, end);

        if (piece != PType.KING && prom == PType.NONE) 
            Activate(wKing, bKing, (int)piece, colMoved, end);

        else if (prom is PType.KNIGHT or PType.BISHOP or PType.ROOK or PType.QUEEN)
            Activate(wKing, bKing, (int)prom, colMoved, end);

        else if (prom == PType.PAWN) {
            int captureSq = colMoved == Color.WHITE 
                ? end + 8 
                : end - 8;
            Activate(  wKing, bKing, 0, colMoved, end);
            Deactivate(wKing, bKing, 0, oppColor, captureSq);
        } 
        else if (prom == PType.KING) {
            // handle rook moves for castling
            switch (end) {
                case 62:
                    Deactivate(wKing, bKing, 3, colMoved, 63);
                    Activate(  wKing, bKing, 3, colMoved, 61);
                    break;
                case 58:
                    Deactivate(wKing, bKing, 3, colMoved, 56);
                    Activate(  wKing, bKing, 3, colMoved, 59);
                    break;
                case 6:
                    Deactivate(wKing, bKing, 3, colMoved, 7);
                    Activate(  wKing, bKing, 3, colMoved, 5);
                    break;
                case 2:
                    Deactivate(wKing, bKing, 3, colMoved, 0);
                    Activate(  wKing, bKing, 3, colMoved, 3);
                    break;
            }
        }

        // if a king moves, the new feature embeddings are different,
        // so we must completely refresh the accumulator. the other
        // color ignores the king, so it can stay as is
        if (move.Piece == PType.KING)
            RebuildAccumulator(in board, colMoved);

        UpdateEvaluation(oppColor, (int)ulong.PopCount(board.Occupied));
    }

    private void RebuildAccumulator(in Board board, Color col) {
        int featureCount = ExtractFeatures(in board);

        if (col == Color.WHITE) {
            Array.Clear(_accW, 0, NNUEWeights.EmbedDims);
            
            // rebuild white accumulator
            for (int i = 0; i < featureCount; i++)
                UpdateFeature(_wFeatures[i], true, _accW);
        }
        else {
            Array.Clear(_accB, 0, NNUEWeights.EmbedDims);

            // rebuild black accumulator
            for (int i = 0; i < featureCount; i++)
                UpdateFeature(_bFeatures[i], true, _accB);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Activate(int wKing, int bKing, int piece, Color col, int sq) {
        int idxW = FeatureIndex(Color.WHITE, wKing, piece, col, sq);
        int idxB = FeatureIndex(Color.BLACK, bKing, piece, col, sq);

        UpdateFeature(idxW, true, _accW);
        UpdateFeature(idxB, true, _accB);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Deactivate(int wKing, int bKing, int piece, Color col, int sq) {
        int idxW = FeatureIndex(Color.WHITE, wKing, piece, col, sq);
        int idxB = FeatureIndex(Color.BLACK, bKing, piece, col, sq);

        UpdateFeature(idxW, false, _accW);
        UpdateFeature(idxB, false, _accB);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FeatureIndex(Color perspective, int kingSq, int piece, Color col, int sq) {
        int colBit = col == Color.WHITE ? 0 : 1;

        if (perspective == Color.WHITE) {
            kingSq ^= 56;
            sq     ^= 56;
        } else colBit = 1 - colBit;
        
        return kingSq * 640 + (piece * 2 + colBit) * 64 + sq;
    }

    // extract features: returns per-accumulator feature indices (skips kings)
    private int ExtractFeatures(in Board board) {
        // get king squares in python-chess indexing (we'll store kings in engine indexing)
        int wKing = BB.LS1B(board.Pieces[5]);   // engine square for white king
        int bKing = BB.LS1B(board.Pieces[11]);  // engine square for black king
        
        int count = 0;

        ReadOnlySpan<ulong> pieces = board.Pieces;
        
        // loop over all piece types except king
        for (byte i = 0; i < 5; i++) {

            // copy the respective piece bitboards for both colors
            ulong wCopy = pieces[i];
            ulong bCopy = pieces[6 + i];
            
            while (wCopy != 0UL) {
                byte sq = BB.LS1BReset(ref wCopy);
                _wFeatures[count]   = FeatureIndex(Color.WHITE, wKing, i, Color.WHITE, sq);
                _bFeatures[count++] = FeatureIndex(Color.BLACK, bKing, i, Color.WHITE, sq);
            }

            while (bCopy != 0UL) {
                byte sq = BB.LS1BReset(ref bCopy);
                _wFeatures[count]   = FeatureIndex(Color.WHITE, wKing, i, Color.BLACK, sq);
                _bFeatures[count++] = FeatureIndex(Color.BLACK, bKing, i, Color.BLACK, sq);
            }
        }
        
        return count;
    }

    // Update feature in accumulator: add or subtract embedding vector.
    // 'acc'    : target accumulator (either _accW or _accB)
    // 'featureIdx' : index in range [0 ... 40960-1]
    // 'add'    : true to add embedding, false to subtract
    // 'isWhite': true => use EmbeddingWhite, false => use EmbeddingBlack
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateFeature(int featureIdx, bool add, short[] accumulator) {
        int   baseIndex = featureIdx * NNUEWeights.EmbedDims; // index into embedding flat array
        short sign      = add ? (short)1 : (short)-1;

        ref short accRef = ref MemoryMarshal.GetArrayDataReference(accumulator);
        ref short embRef = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.Embedding);
        
        // vectorized loop (process 16 int16 elements at a time)
        for (int i = 0; i <= NNUEWeights.EmbedDims - 16; i += 16) {
            var va = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref accRef, i)));
            var vb = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref embRef, baseIndex + i)));

#pragma warning disable CA1508
            var vr = sign == 1 
                ? Avx2.Add(va, vb) 
                : Avx2.Subtract(va, vb);
#pragma warning restore CA1508
            
            Avx.Store((short*)Unsafe.AsPointer(ref Unsafe.Add(ref accRef, i)), vr);
        }
    }

    // perform forward pass using AVX2 PMADDWD for dot-products
    internal void UpdateEvaluation(Color active, int pcnt) {
        
        // concatenate accumulators: [accW (0..255), accB (256..511)]
        Span<short> concat = stackalloc short[H1Input];

        if (active == Color.WHITE) {
            _accW.AsSpan().CopyTo(concat[..EmbedDims]);
            _accB.AsSpan().CopyTo(concat[EmbedDims..H1Input]);
        } else {
            _accB.AsSpan().CopyTo(concat[..EmbedDims]);
            _accW.AsSpan().CopyTo(concat[EmbedDims..H1Input]);
        }

        int subnet = Math.Clamp(pcnt / 8, 0, 3);

        ref short concatRef = ref MemoryMarshal.GetReference(concat);
        
        ref short h1_kernelRef = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.H1Kernels[subnet]);
        ref short h2_kernelRef = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.H2Kernels[subnet]);
        
        ReadOnlySpan<int> h1_bias = NNUEWeights.H1Biases[subnet];
        ReadOnlySpan<int> h2_bias = NNUEWeights.H2Biases[subnet];
        
        Span<short> h1_activation = stackalloc short[H1Neurons];
        Span<short> h2_activation = stackalloc short[H2Neurons];
        ref short   h1_ActRef     = ref MemoryMarshal.GetReference(h1_activation);
        ref short   h2_ActRef     = ref MemoryMarshal.GetReference(h2_activation);
        
        for (int j = 0; j < H1Neurons; j++) {
            int wBase = j * H1Input;

            var vsum = Vector256<int>.Zero;
            
            // process 16 int16 elements at a time
            for (int i = 0; i <= H1Input - 16; i += 16) {
                var va   = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref concatRef, i)));
                var vb   = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref h1_kernelRef, wBase + i)));
                var prod = Avx2.MultiplyAddAdjacent(va, vb);
                
                vsum = Avx2.Add(vsum, prod);
            }

            int sum = HorizontalAddVector256(vsum) / ScaleInt;

            // add bias (already scaled by S)
            sum += h1_bias[j];
            
            // scaled CReLU (Clipped ReLU) activation
            h1_activation[j] = (short)Math.Clamp(sum, 0, ScaleInt);
        }
        
        // second hidden layer
        for (int j = 0; j < H2Neurons; j++) {
            int wBase = j * H1Neurons;

            var vsum = Vector256<int>.Zero;

            for (int i = 0; i <= H1Neurons - 16; i += 16) {
                var va   = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref h1_ActRef, i)));
                var vb   = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref h2_kernelRef, wBase + i)));
                var prod = Avx2.MultiplyAddAdjacent(va, vb);
                
                vsum = Avx2.Add(vsum, prod);
            }

            int sum = HorizontalAddVector256(vsum) / ScaleInt;

            sum += h2_bias[j];
            h2_activation[j] = (short)Math.Clamp(sum, 0, ScaleInt);
        }

        // output layer
        var vsum3 = Vector256<int>.Zero;
        
        ref short outKerRef = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.OutputKernel);

        for (int i = 0; i <= H2Neurons - 16; i += 16) {
            var va   = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref h2_ActRef, i)));
            var vb   = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref outKerRef, i)));
            var prod = Avx2.MultiplyAddAdjacent(va, vb);
            
            vsum3 = Avx2.Add(vsum3, prod);
        }

        // rescale by scale
        int prediction = HorizontalAddVector256(vsum3) / ScaleInt;
        
        // add output bias (already scaled by S)
        int biasedPred = NNUEWeights.OutputBias + prediction;

        // convert to float (units are now same as original float network * S)
        float pred = biasedPred / NNUEWeights.Scale;
        float sigm = MathLUT.FastSigmoid(pred);

        Score = (short)(ProbToScore(sigm) * (active == Color.WHITE ? 1 : -1));
    }

    // horizontal add of Vector256<int> (8 lanes)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HorizontalAddVector256(Vector256<int> v) {
        // sum lanes using shuffle/add
        var high   = Avx2.ExtractVector128(v, 1);
        var low    = v.GetLower(); // lower 128
        var sum128 = Sse2.Add(low.AsInt32(), high.AsInt32());

        // now sum 4 lanes in sum128
        var shuf = Sse2.Shuffle(sum128, 0x4E); // swap pairs

        sum128 = Sse2.Add(sum128, shuf);
        shuf   = Sse2.Shuffle(sum128, 0xB1);
        sum128 = Sse2.Add(sum128, shuf);

        return sum128.ToScalar();
    }

    // the python training script turns cp into a probability,
    // this is the inverse function that returns cp instead
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short ProbToScore(float p) {
        float odds = p / (1f - p);
        float ln   = MathLUT.FastLn(odds);
        return (short)(ln * 400f);
    }
}

#pragma warning restore CA2014