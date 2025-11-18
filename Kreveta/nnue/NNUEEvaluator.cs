//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

// ReSharper disable InconsistentNaming
namespace Kreveta.nnue;

internal unsafe sealed class NNUEEvaluator {
    // NOTE: This evaluator expects NNUEWeights to have been quantized
    // to short (int16) and biases stored as int (int32). See NNUEWeights.Load.

    // accumulator is short[] (int16) for fastest PMADDWD dot-product.
    // Ensure quantization SCALE was chosen to avoid accumulator overflow.
    private readonly short[] _accumulator = new short[NNUEWeights.EmbedDims];

    // small activation buffers (int32 accumulators for hidden neurons after dot)
    // We'll store H1/H2 activations as int (accumulated int32) before clipping trim to short if desired.
    private readonly int[] _h1Activation = new int[NNUEWeights.H1Neurons];
    private readonly int[] _h2Activation = new int[NNUEWeights.H2Neurons];

    internal short Score { get; private set; }

    // shortcut to constants:
    private const int EmbedDims = NNUEWeights.EmbedDims;
    private const int H1Neurons = NNUEWeights.H1Neurons;
    private const int H2Neurons = NNUEWeights.H2Neurons;
        
    private const int ScaleInt = (int)NNUEWeights.Scale;

    internal NNUEEvaluator() {
        // nothing
    }

    internal NNUEEvaluator(in NNUEEvaluator other) {
        Array.Copy(other._accumulator, _accumulator, _accumulator.Length);
        Score = other.Score;
    }

    internal NNUEEvaluator(in Board board) {
        Update(in board);
    }

    internal void Update(in Board board) {
        // zero accumulator
        Array.Clear(_accumulator, 0, _accumulator.Length);

        // rebuild accumulator by summing embeddings for active features
        var features = ExtractFeatures(in board);
        foreach (int f in features)
            UpdateFeatureInAccumulator(f, true);

        UpdateEvaluation();
    }

    // incremental update on moves (same logic as before)
    internal void Update(Move move, Color colMoved) {
        int start = move.Start;
        int end   = move.End;

        PType piece = move.Piece;
        PType capt  = move.Capture;
        PType prom  = move.Promotion;

        Color oppColor = colMoved == Color.WHITE ? Color.BLACK : Color.WHITE;

        Deactivate(piece, colMoved, start);

        if (capt != PType.NONE) Deactivate(capt, oppColor, end);

        if (prom == PType.NONE) 
            Activate(piece, colMoved, end);
            
        else if (prom is PType.KNIGHT or PType.BISHOP or PType.ROOK or PType.QUEEN)
            Activate(prom, colMoved, end);
            
        else if (prom == PType.PAWN) {
            int captureSq = colMoved == Color.WHITE 
                ? end + 8 
                : end - 8;
            Activate(PType.PAWN, colMoved, end);
            Deactivate(PType.PAWN, oppColor, captureSq);
        } 
        else if (prom == PType.KING) {
            Activate(PType.KING, colMoved, end);
                
            switch (end) {
                case 62:
                    Deactivate(PType.ROOK, colMoved, 63); Activate(PType.ROOK, colMoved, 61); break;
                case 58:
                    Deactivate(PType.ROOK, colMoved, 56); Activate(PType.ROOK, colMoved, 59); break;
                case 6:
                    Deactivate(PType.ROOK, colMoved, 7);  Activate(PType.ROOK, colMoved, 5);  break;
                case 2:
                    Deactivate(PType.ROOK, colMoved, 0);  Activate(PType.ROOK, colMoved, 3);  break;
            }
        }

        UpdateEvaluation();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Activate(PType piece, Color col, int sq) {
        int feature = CreateFeatureIndex(col, piece, sq);
        UpdateFeatureInAccumulator(feature, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Deactivate(PType piece, Color col, int sq) {
        int feature = CreateFeatureIndex(col, piece, sq);
        UpdateFeatureInAccumulator(feature, false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CreateFeatureIndex(Color col, PType piece, int sq)
        => ((int)col * 6 + (int)piece) * 64 + (sq ^ 56);

    // Extract features as before
    private static List<int> ExtractFeatures(in Board board) {
        var features = new List<int>(32);
            
        for (int sq = 0; sq < 64; sq++) {
            if ((board.Occupied & 1UL << sq) == 0UL) 
                continue;
                
            PType piece = board.PieceAt(sq);
            Color col   = (board.WOccupied & 1UL << sq) != 0UL
                ? Color.WHITE 
                : Color.BLACK;
                
            features.Add(CreateFeatureIndex(col, piece, sq));
        }
        return features;
    }

    // Update feature in accumulator: add or subtract embedding vector.
    // Uses AVX2 256-bit vectorized operations when available.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateFeatureInAccumulator(int feature, bool activate) {
        int baseIndex = feature * EmbedDims;
        short sign    = activate ? (short)1 : (short)-1;

        // Fast path with AVX2 (vectorized add/sub)
        // we'll process 16 int16 elements per loop (256 bits)
        int i = 0;
        ref short accRef = ref MemoryMarshal.GetArrayDataReference(_accumulator);
        ref short embRef = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.Embedding);

        for (; i <= EmbedDims - 16; i += 16) {
            var vAcc = Avx.LoadVector256(
                (short*)Unsafe.AsPointer(ref Unsafe.Add(ref accRef, i))
            );
            
            var vEmb = Avx.LoadVector256(
                (short*)Unsafe.AsPointer(ref Unsafe.Add(ref embRef, baseIndex + i))
            );

            var vRes = sign == 1
                ? Avx2.Add(vAcc, vEmb)
                : Avx2.Subtract(vAcc, vEmb);

            Avx.Store(
                (short*)Unsafe.AsPointer(ref Unsafe.Add(ref accRef, i)), 
                vRes
            );
        }

        // tail
        for (; i < EmbedDims; i++) {
            int idx = baseIndex + i;
            int tmp = _accumulator[i] + sign * NNUEWeights.Embedding[idx];
                
            if (tmp > short.MaxValue)
                tmp = short.MaxValue;
                
            else if (tmp < short.MinValue)
                tmp = short.MinValue;
                
            _accumulator[i] = (short)tmp;
        }
    }

    // UpdateEvaluation: perform forward pass using AVX2 PMADDWD for dot-products
    private void UpdateEvaluation() {
        // H1 pass
        for (int j = 0; j < H1Neurons; j++) {
            int sum   = 0;
            int wBase = j * EmbedDims;

            var vsum = Vector256<int>.Zero;
            int i = 0;
            
            ref short accRef = ref MemoryMarshal.GetArrayDataReference(_accumulator);
            ref short kerRef = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.H1Kernel);

            for (; i <= EmbedDims - 16; i += 16) {
                var va = Avx.LoadVector256(
                    (short*)Unsafe.AsPointer(ref Unsafe.Add(ref accRef, i))
                );
                
                var vb = Avx.LoadVector256(
                    (short*)Unsafe.AsPointer(ref Unsafe.Add(ref kerRef, wBase + i))
                );

                var prod = Avx2.MultiplyAddAdjacent(va, vb);
                vsum = Avx2.Add(vsum, prod);
            }

            sum += HorizontalAddVector256(vsum);
            
            // RESCALE by S: dot product currently carries factor S^2, we need to divide by S
            // to bring the sum into the same units as bias (which is scaled by S).
            int dotScaled = sum >= 0
                ? (sum + (ScaleInt >> 1)) / ScaleInt  // round positive
                : (sum - (ScaleInt >> 1)) / ScaleInt; // round negative
            
            // add bias (already scaled by S)
            int combined = NNUEWeights.H1Bias[j] + dotScaled;

            int act = combined;
            if (act < 0) 
                act = 0;

            _h1Activation[j] = act;
        }

        // second hidden layer (16 neurons)
        for (int j = 0; j < H2Neurons; j++) {
            int sum   = 0;
            int wBase = j * H1Neurons;

            var vsum = Vector256<int>.Zero;
            int i = 0;
            
// stack allocation inside a loop may result in stack overflow.
// that is incorrect though, as this loop is so tiny that it
// will never ever cause any issues
#pragma warning disable CA2014
            // ReSharper disable once StackAllocInsideLoop
            Span<short> h1Packed = stackalloc short[H1Neurons];
#pragma warning restore CA2014
            
            for (int p = 0; p < H1Neurons; p++) 
                h1Packed[p] = (short)_h1Activation[p];

            ref short h1PackRef = ref MemoryMarshal.GetReference(h1Packed);
            ref short kerRef = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.H2Kernel);

            for (; i <= H1Neurons - 16; i += 16) {
                var va = Avx.LoadVector256(
                    (short*)Unsafe.AsPointer(ref Unsafe.Add(ref h1PackRef, i))
                );
                
                var vb = Avx.LoadVector256(
                    (short*)Unsafe.AsPointer(ref Unsafe.Add(ref kerRef, wBase + i))
                );
                
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
            if (act < 0) 
                act = 0;

            _h2Activation[j] = act;
        }

        // output layer
        int prediction = 0;

        var vsum3 = Vector256<int>.Zero;
        int i3 = 0;

        Span<short> h2Packed = stackalloc short[H2Neurons];
        for (int p = 0; p < H2Neurons; p++) 
            h2Packed[p] = (short)_h2Activation[p];

        ref short h2PackRef = ref MemoryMarshal.GetReference(h2Packed);
        ref short outKerRef = ref MemoryMarshal.GetArrayDataReference(NNUEWeights.OutputKernel);

        for (; i3 <= H2Neurons - 16; i3 += 16) {
            var va = Avx.LoadVector256(
                (short*)Unsafe.AsPointer(ref Unsafe.Add(ref h2PackRef, i3))
            );
            
            var vb = Avx.LoadVector256(
                (short*)Unsafe.AsPointer(ref Unsafe.Add(ref outKerRef, i3))
            );
            
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
        float predFloat = scaledPrediction / NNUEWeights.Scale;
        float prob      = 1f / (1f + MathF.Exp(-predFloat));
        
        Score = InverseCPToP(prob);
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

    // the python training script turned cp -> probability. undo that
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short InverseCPToP(float p) {
        const float epsilon = 1e-6f;
        p = Math.Clamp(p, epsilon, 1 - epsilon);
            
        float val = 400f * MathF.Log(p / (1f - p));
        return (short)Math.Clamp((int)val, -3000, 3000);
    }
}

/*
using Kreveta.consts;
using Kreveta.movegen;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming
namespace Kreveta.nnue;

internal sealed class NNUEEvaluator {

// reusable buffer to avoid repeated allocations
private readonly float[] _h1LayerActivation = new float[NNUEWeights.H1Neurons];
private readonly float[] _h2LayerActivation = new float[NNUEWeights.H2Neurons];
private readonly float[] _accumulator       = new float[NNUEWeights.EmbedDims];

internal short Score { get; private set; }

internal NNUEEvaluator() {}

internal NNUEEvaluator(in NNUEEvaluator other) {
    Array.Copy(other._accumulator,_accumulator, NNUEWeights.EmbedDims);
    Score = other.Score;
}

internal NNUEEvaluator(in Board board) {
    Update(in board);
}

internal void Update(in Board board) {
    Array.Clear(_accumulator, 0, _accumulator.Length);

    // rebuild the accumulator from scratch
    var features = ExtractFeatures(in board);
    foreach (int f in features)
        UpdateFeatureInAccumulator(f, true);

    UpdateEvaluation();
}

internal void Update(Move move, Color colMoved) {
    int start = move.Start;
    int end   = move.End;

    PType piece = move.Piece;
    PType capt  = move.Capture;
    PType prom  = move.Promotion;

    Color oppColor = colMoved == Color.WHITE
        ? Color.BLACK : Color.WHITE;

    // deactivate the piece that moved from its starting square
    Deactivate(piece, colMoved, start);

    // deactivate a potential capture
    if (capt != PType.NONE)
        Deactivate(capt, oppColor, end);

    // regular move - just put the piece on its new square
    if (prom == PType.NONE)
        Activate(piece, colMoved, end);

    // activate the new piece in case of promotion
    else if (prom is PType.KNIGHT or PType.BISHOP or PType.ROOK or PType.QUEEN)
        Activate(prom, colMoved, end);

    // en passant - remove the captured pawn
    else if (prom == PType.PAWN) {

        // the pawn that is to be captured
        int captureSq = colMoved == Color.WHITE
            ? end + 8
            : end - 8;

        Activate(PType.PAWN, colMoved, end);
        Deactivate(PType.PAWN, oppColor, captureSq);
    }

    // castling
    else if (prom == PType.KING) {

        // first move the king to its new square
        Activate(PType.KING, colMoved, end);

        // and then move the respective rook
        switch (end) {
            // white kingside
            case 62: {
                Deactivate(PType.ROOK, colMoved, 63);
                Activate(PType.ROOK, colMoved, 61);
                break;
            }

            // white queenside
            case 58: {
                Deactivate(PType.ROOK, colMoved, 56);
                Activate(PType.ROOK, colMoved, 59);
                break;
            }

            // black kingside
            case 6: {
                Deactivate(PType.ROOK, colMoved, 7);
                Activate(PType.ROOK, colMoved, 5);
                break;
            }

            // black queenside
            case 2: {
                Deactivate(PType.ROOK, colMoved, 0);
                Activate(PType.ROOK, colMoved, 3);
                break;
            }
        }
    }

    UpdateEvaluation();
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
private void Activate(PType piece, Color col, int sq) {
    int feature = CreateFeatureIndex(col, piece, sq);
    UpdateFeatureInAccumulator(feature, true);
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
private void Deactivate(PType piece, Color col, int sq) {
    int feature = CreateFeatureIndex(col, piece, sq);
    UpdateFeatureInAccumulator(feature, false);
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static int CreateFeatureIndex(Color col, PType piece, int sq)
    => ((int)col * 6 + (int)piece) * 64 + (sq ^ 56);

// i don't care that a List is not as efficient as an array.
// this function is only called once, when the root position
// is being initialized. there is no performance loss
private static List<int> ExtractFeatures(in Board board) {
    List<int> features = [];

    for (int sq = 0; sq < 64; sq++) {
        if ((board.Occupied & 1UL << sq) == 0)
            continue;

        PType piece = board.PieceAt(sq);
        Color col   = (board.WOccupied & 1UL << sq) != 0
            ? Color.WHITE : Color.BLACK;

        features.Add(CreateFeatureIndex(col, piece, sq));
    }

    return features;
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
private void UpdateFeatureInAccumulator(int feature, bool activate) {
    int   embedIndex = feature;
    int   baseIndex  = embedIndex * NNUEWeights.EmbedDims;
    float sign       = activate ? 1.0f : -1.0f;

    float[] acc  = _accumulator;
    float[] emb  = NNUEWeights.Embedding;
    int vecWidth = Vector<float>.Count;

    int i = 0;

    // SIMD loop
    for (; i <= NNUEWeights.EmbedDims - vecWidth; i += vecWidth) {
        var vAcc = new Vector<float>(acc, i);
        var vEmb = new Vector<float>(emb, baseIndex + i);

        vAcc += vEmb * sign;
        vAcc.CopyTo(acc, i);
    }

    // scalar tail
    for (; i < NNUEWeights.EmbedDims; i++)
        acc[i] += emb[baseIndex + i] * sign;
}

// forward pass through the network for a single position.
// uses the accumulator as input and computes the evaluation
private void UpdateEvaluation() {
    int vecWidth = Vector<float>.Count;

    // 32-neuron hidden dense layer; reuses a buffer instead of more
    // allocating. no need to clear, as all entries are overwritten
    float[] h1Activation = _h1LayerActivation;
    float[] h2Activation = _h2LayerActivation;

    float[] acc      = _accumulator;
    float[] h1Bias   = NNUEWeights.H1Bias;
    float[] h1Kernel = NNUEWeights.H1Kernel;

    for (int j = 0; j < NNUEWeights.H1Neurons; j++) {
        float sum = h1Bias[j];

        int i = 0;
        int wBase = j * NNUEWeights.EmbedDims;

        // manual dot product with SIMD
        for (; i <= NNUEWeights.EmbedDims - vecWidth; i += vecWidth) {
            var vA = new Vector<float>(acc, i);
            var vW = new Vector<float>(h1Kernel, wBase + i);

            sum += Vector.Dot(vA, vW);
        }

        // scalar remainder
        for (; i < NNUEWeights.EmbedDims; i++)
            sum += acc[i] * h1Kernel[wBase + i];

        h1Activation[j] = Math.Max(0, sum);
    }

    float[] h2Bias   = NNUEWeights.H2Bias;
    float[] h2Kernel = NNUEWeights.H2Kernel;

    for (int j = 0; j < NNUEWeights.H2Neurons; j++) {
        float sum = h2Bias[j];

        int i = 0;
        int wBase = j * NNUEWeights.H1Neurons;

        // manual dot product with SIMD
        for (; i <= NNUEWeights.H1Neurons - vecWidth; i += vecWidth) {
            var vH1 = new Vector<float>(h1Activation, i);
            var vW  = new Vector<float>(h2Kernel, wBase + i);

            sum += Vector.Dot(vH1, vW);
        }

        // scalar remainder
        for (; i < NNUEWeights.H1Neurons; i++)
            sum += h1Activation[i] * h2Kernel[wBase + i];

        // ReLU activation
        h2Activation[j] = Math.Max(0, sum);
    }

    // output layer (single neuron)
    float   prediction   = NNUEWeights.OutputBias;
    float[] outputKernel = NNUEWeights.OutputKernel;

    int k = 0;
    for (; k <= NNUEWeights.H2Neurons - vecWidth; k += vecWidth) {
        var vH = new Vector<float>(h2Activation, k);
        var vW = new Vector<float>(outputKernel, k);

        prediction += Vector.Dot(vH, vW);
    }

    for (; k < NNUEWeights.H2Neurons; k++)
        prediction += h2Activation[k] * outputKernel[k];

    // sigmoid final probability
    prediction = 1f / (1f + MathF.Exp(-prediction));

    // inverse to match correct score format
    Score = InverseCPToP(prediction);
}

// the python training script turns all evaluations (in cp)
// into a probability score in range [0..1]. now that the
// network predicted a probability, it shall be turned back
// into a cp score using the inverse of the mentioned funcion
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static short InverseCPToP(float p) {
    const float epsilon = 1e-6f;
    p = Math.Clamp(p, epsilon, 1 - epsilon);

    int cp = (int)(400 * MathF.Log(p / (1 - p), MathF.E));
    return (short)Math.Clamp(cp, -3000, 3000);
}
}*/