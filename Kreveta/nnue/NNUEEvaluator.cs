//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA2014

using Kreveta.consts;
using Kreveta.movegen;

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

    // small activation buffers (int32 accumulators for hidden neurons after dot)
    private readonly int[] _h1Activation = new int[NNUEWeights.H1Neurons];
    private readonly int[] _h2Activation = new int[NNUEWeights.H2Neurons];

    private readonly int[] _wFeatures = new int[32];
    private readonly int[] _bFeatures = new int[32];

    internal short Score { get; private set; }

    // shortcut to constants:
    private const int EmbedDims = NNUEWeights.EmbedDims;   // 256
    private const int H1Neurons = NNUEWeights.H1Neurons;   // 32
    private const int H2Neurons = NNUEWeights.H2Neurons;   // 32

    // H1 input width after concatenation (white + black)
    private const int H1Input = EmbedDims * 2;             // 512

    private const int ScaleInt = (int)NNUEWeights.Scale;  // 512

    internal NNUEEvaluator() {
        // nothing
    }

    internal NNUEEvaluator(in NNUEEvaluator other) {
        Array.Copy(other._accW, _accW, NNUEWeights.EmbedDims);
        Array.Copy(other._accB, _accB, NNUEWeights.EmbedDims);
        Score = other.Score;
    }

    internal NNUEEvaluator(in Board board) {
        Update(in board);
    }

    private void Update(in Board board) {
        // zero accumulators
        Array.Clear(_accW, 0, NNUEWeights.EmbedDims);
        Array.Clear(_accB, 0, NNUEWeights.EmbedDims);

        // rebuild accumulator by summing embeddings for active features
        int featureCount = ExtractFeatures(in board);

        // White features -> _accW
        for (int i = 0; i < featureCount; i++) {
            UpdateFeature(_wFeatures[i], true, _accW, NNUEWeights.EmbeddingWhite);
            UpdateFeature(_bFeatures[i], true, _accB, NNUEWeights.EmbeddingBlack);
        }

        UpdateEvaluation();
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

        Color oppColor = colMoved == Color.WHITE ? Color.BLACK : Color.WHITE;

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

        UpdateEvaluation();
    }

    private void RebuildAccumulator(in Board board, Color col) {
        int featureCount = ExtractFeatures(in board);

        if (col == Color.WHITE) {
            Array.Clear(_accW, 0, NNUEWeights.EmbedDims);
            
            // rebuild white accumulator
            for (int i = 0; i < featureCount; i++)
                UpdateFeature(_wFeatures[i], true, _accW, NNUEWeights.EmbeddingWhite);
        }
        else {
            Array.Clear(_accB, 0, NNUEWeights.EmbedDims);

            // rebuild black accumulator
            for (int i = 0; i < featureCount; i++)
                UpdateFeature(_bFeatures[i], true, _accB, NNUEWeights.EmbeddingBlack);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Activate(int wKing, int bKing, int piece, Color col, int sq) {
        int idxW = FeatureIndex(wKing, piece, col, sq);
        int idxB = FeatureIndex(bKing, piece, col, sq);

        UpdateFeature(idxW, true, _accW, NNUEWeights.EmbeddingWhite);
        UpdateFeature(idxB, true, _accB, NNUEWeights.EmbeddingBlack);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Deactivate(int wKing, int bKing, int piece, Color col, int sq) {
        int idxW = FeatureIndex(wKing, piece, col, sq);
        int idxB = FeatureIndex(bKing, piece, col, sq);

        UpdateFeature(idxW, false, _accW, NNUEWeights.EmbeddingWhite);
        UpdateFeature(idxB, false, _accB, NNUEWeights.EmbeddingBlack);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FeatureIndex(int kingSq, int piece, Color col, int sq)
        => (kingSq ^ 56) * 640 
           + (piece * 2 + (col == Color.WHITE ? 0 : 1)) * 64 
           + (sq ^ 56);

    // Extract features: returns per-accumulator feature indices (skips kings)
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
                _wFeatures[count]   = FeatureIndex(wKing, i, Color.WHITE, sq);
                _bFeatures[count++] = FeatureIndex(bKing, i, Color.WHITE, sq);
            }

            while (bCopy != 0UL) {
                byte sq = BB.LS1BReset(ref bCopy);
                _wFeatures[count]   = FeatureIndex(wKing, i, Color.BLACK, sq);
                _bFeatures[count++] = FeatureIndex(bKing, i, Color.BLACK, sq);
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
    private static void UpdateFeature(int featureIdx, bool add, short[] accumulator, short[] embedding) {
        int   baseIndex = featureIdx * NNUEWeights.EmbedDims; // index into embedding flat array
        short sign      = add ? (short)1 : (short)-1;

        ref short accRef = ref MemoryMarshal.GetArrayDataReference(accumulator);
        ref short embRef = ref MemoryMarshal.GetArrayDataReference(embedding);
        
        // vectorized loop (process 16 int16 elements at a time)
        for (int i = 0; i <= NNUEWeights.EmbedDims - 16; i += 16) {
            var va = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref accRef, i)));
            var vb = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref embRef, baseIndex + i)));

            var vr = sign == 1 
                ? Avx2.Add(va, vb) 
                : Avx2.Subtract(va, vb);

            Avx.Store((short*)Unsafe.AsPointer(ref Unsafe.Add(ref accRef, i)), vr);
        }
    }

    // perform forward pass using AVX2 PMADDWD for dot-products
    private void UpdateEvaluation() {
        
        // concatenate accumulators: [accW (0..255), accB (256..511)]
        Span<short> concat = stackalloc short[H1Input];
        _accW.AsSpan().CopyTo(concat[..EmbedDims]);
        _accB.AsSpan().CopyTo(concat[EmbedDims..H1Input]);

        // H1 pass: input width = 512 (H1Input), kernel stored neuron-major [32 * 512]
        for (int j = 0; j < H1Neurons; j++) {
            int sum   = 0;
            int wBase = j * H1Input;

            var vsum = Vector256<int>.Zero;

            ref short inRef  = ref MemoryMarshal.GetReference(concat);
            ref short kerRef = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.H1Kernel);

            // process 16 int16 elements at a time
            for (int i = 0; i <= H1Input - 16; i += 16) {
                var va = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref inRef, i)));
                var vb = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref kerRef, wBase + i)));

                var prod = Avx2.MultiplyAddAdjacent(va, vb);
                vsum = Avx2.Add(vsum, prod);
            }

            sum += HorizontalAddVector256(vsum);

            // RESCALE by S: dot product currently carries factor S^2, we need to divide by S
            int dotScaled = sum >= 0
                ? (sum + (ScaleInt >> 1)) / ScaleInt  // round positive
                : (sum - (ScaleInt >> 1)) / ScaleInt; // round negative

            // add bias (already scaled by S)
            int combined = NNUEWeights.H1Bias[j] + dotScaled;

            int act = combined;
            if (act < 0) act = 0;

            _h1Activation[j] = act;
        }

        // second hidden layer
        for (int j = 0; j < H2Neurons; j++) {
            int sum = 0;
            int wBase = j * H1Neurons;

            var vsum = Vector256<int>.Zero;
            
            // ReSharper disable once StackAllocInsideLoop
            Span<short> h1Packed = stackalloc short[H1Neurons];

            for (int p = 0; p < H1Neurons; p++)
                h1Packed[p] = (short)_h1Activation[p];

            ref short h1PackRef = ref MemoryMarshal.GetReference(h1Packed);
            ref short kerRef = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.H2Kernel);

            for (int i = 0; i <= H1Neurons - 16; i += 16) {
                var va   = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref h1PackRef, i)));
                var vb   = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref kerRef, wBase + i)));
                var prod = Avx2.MultiplyAddAdjacent(va, vb);
                
                vsum = Avx2.Add(vsum, prod);
            }

            sum += HorizontalAddVector256(vsum);

            // RESCALE by S
            int dotScaled = sum >= 0
                ? (sum + (ScaleInt >> 1)) / ScaleInt
                : (sum - (ScaleInt >> 1)) / ScaleInt;

            int combined = NNUEWeights.H2Bias[j] + dotScaled;

            int act = combined;
            if (act < 0) act = 0;

            _h2Activation[j] = act;
        }

        // output layer
        int prediction = 0;

        var vsum3 = Vector256<int>.Zero;

        Span<short> h2Packed = stackalloc short[H2Neurons];
        for (int p = 0; p < H2Neurons; p++)
            h2Packed[p] = (short)_h2Activation[p];

        ref short h2PackRef = ref MemoryMarshal.GetReference(h2Packed);
        ref short outKerRef = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.OutputKernel);

        for (int i = 0; i <= H2Neurons - 16; i += 16) {
            var va   = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref h2PackRef, i)));
            var vb   = Avx.LoadVector256((short*)Unsafe.AsPointer(ref Unsafe.Add(ref outKerRef, i)));
            var prod = Avx2.MultiplyAddAdjacent(va, vb);
            
            vsum3 = Avx2.Add(vsum3, prod);
        }

        prediction += HorizontalAddVector256(vsum3);

        // RESCALE by S
        int dotScaled2 = prediction >= 0
            ? (prediction + (ScaleInt >> 1)) / ScaleInt
            : (prediction - (ScaleInt >> 1)) / ScaleInt;

        // Add output bias (already scaled by S)
        int scaledPrediction = NNUEWeights.OutputBias + dotScaled2;

        // Convert to float (units are now same as original float network * S)
        float pred = scaledPrediction / NNUEWeights.Scale;
        float prob = 1f / (1f + MathF.Exp(-pred));

        Score = ProbToScore(prob);
    }

    // horizontal add of Vector256<int> (8 lanes)
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
        p = 400f * MathF.Log(p / (1f - p));
        return (short)Math.Clamp(p, -3000, 3000);
    }
}

#pragma warning restore CA2014