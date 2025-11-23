//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.IO;
using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming

// Non-nullable field must contain a non-null value when exiting constructor.
#pragma warning disable CS8618

namespace Kreveta.nnue;

internal static class NNUEWeights {

    // integer NNUE weights
    internal static short[] EmbeddingWhite; // [40960 * 256]
    internal static short[] EmbeddingBlack; // [40960 * 256]
    internal static short[] H1Kernel;       // [32 * 512]
    internal static int[]   H1Bias;         // [32]
    internal static short[] H2Kernel;       // [16  * 32]
    internal static int[]   H2Bias;         // [16]
    internal static short[] OutputKernel;   // [16]
    internal static int     OutputBias;

    private  const int FeatCount = 40960;
    internal const int EmbedDims = 256;
    internal const int H1Neurons = 32;
    internal const int H2Neurons = 32;

    // global quantization constant
    internal const float Scale = 1024f;

    internal static void Load(string binPath) {
        
        byte[] rawBytes = File.ReadAllBytes(binPath);
        float[] all = new float[rawBytes.Length / 4];
        Buffer.BlockCopy(rawBytes, 0, all, 0, rawBytes.Length);

        int off = 0;

        // 1. EMBEDDING WHITE [40960 * 256]
        const int embedLen = FeatCount * EmbedDims;
        EmbeddingWhite = new short[embedLen];

        for (int i = 0; i < embedLen; i++)
            EmbeddingWhite[i] = Q(all[off++]);
        
        // 2. EMBEDDING BLACK [40960 * 256]
        EmbeddingBlack = new short[embedLen];

        for (int i = 0; i < embedLen; i++)
            EmbeddingBlack[i] = Q(all[off++]);

        // 3. H1 KERNEL: Keras [512][32] => here [32][512]
        const int H1Input = EmbedDims * 2;

        float[] h1kerFloat = new float[H1Input * H1Neurons];
        Array.Copy(all, off, h1kerFloat, 0, h1kerFloat.Length);
        off += h1kerFloat.Length;

        H1Kernel = new short[h1kerFloat.Length];

        for (int r = 0; r < H1Input; r++) {
            int kerasRow = r * H1Neurons;
            for (int c = 0; c < H1Neurons; c++) {
                // transpose: store neuron-major
                int dst = c * H1Input + r;
                H1Kernel[dst] = Q(h1kerFloat[kerasRow + c]);
            }
        }

        // 4. H1 BIAS
        H1Bias = new int[H1Neurons];
        for (int i = 0; i < H1Neurons; i++)
            H1Bias[i] = (int)MathF.Round(all[off++] * Scale);

        // 5. H2 KERNEL: Keras [32][32] => here [32][32]
        float[] h2kerFloat = new float[H1Neurons * H2Neurons];
        Array.Copy(all, off, h2kerFloat, 0, h2kerFloat.Length);
        off += h2kerFloat.Length;

        H2Kernel = new short[h2kerFloat.Length];

        for (int r = 0; r < H1Neurons; r++) {
            int kerasRow = r * H2Neurons;
            for (int c = 0; c < H2Neurons; c++) {
                int dst = c * H1Neurons + r;
                H2Kernel[dst] = Q(h2kerFloat[kerasRow + c]);
            }
        }

        // 6. H2 BIAS
        H2Bias = new int[H2Neurons];
        for (int i = 0; i < H2Neurons; i++)
            H2Bias[i] = (int)MathF.Round(all[off++] * Scale);

        // 7. OUTPUT KERNEL [32]
        OutputKernel = new short[H2Neurons];
        for (int i = 0; i < H2Neurons; i++)
            OutputKernel[i] = Q(all[off++]);

        // 8. OUTPUT BIAS
        OutputBias = (int)MathF.Round(all[off] * Scale);

        return;

        // helper function: quantize one float → short
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static short Q(float x) {
            float v = x * Scale;
            v = MathF.Max(-32768f, MathF.Min(32767f, v));
            return (short)MathF.Round(v);
        }
    }
}

/*using System;
using System.IO;
// ReSharper disable InconsistentNaming

#pragma warning disable CS8618

namespace Kreveta.nnue;

internal static class NNUEWeights {
    
    // cache-friendly flat arrays frfr
    // embedding: [769 * 128]
    internal static float[] Embedding;
    
    // dense weights: neuron-major [32 * 128]
    internal static float[] H1Kernel;
    internal static float[] H1Bias;
    internal static float[] H2Kernel;
    internal static float[] H2Bias;
    internal static float[] OutputKernel;
    internal static float   OutputBias;

    // Architecture parameters
    private const  int EmbedCount = 768;
    internal const int EmbedDims  = 128;
    internal const int H1Neurons  = 32;
    internal const int H2Neurons  = 16;

    
    internal static void Load(string binPath) {
        byte[]  rawBytes   = File.ReadAllBytes(binPath);
        int     floatCount = rawBytes.Length / 4;
        float[] all        = new float[floatCount];

        Buffer.BlockCopy(rawBytes, 0, all, 0, rawBytes.Length);

        int off = 0;

        // embedding FLAT (no jagged conversion)
        Embedding = Take(EmbedCount * EmbedDims);
        
        // dense kernel, but rearranged into neuron-major layout.
        // the original Keras layout is row-major [128, 32]
        float[] h1kernelKeras = Take(EmbedDims * H1Neurons);
        H1Kernel = new float[EmbedDims * H1Neurons];
        
        // keras layout: denseKeras[row * 32 + col]
        // our neuron-major: DenseKernelByNeuron[col * 128 + row]
        for (int r = 0; r < EmbedDims; r++) {
            int kerasRow = r * H1Neurons;
            for (int c = 0; c < H1Neurons; c++) {
                H1Kernel[c * EmbedDims + r] = h1kernelKeras[kerasRow + c];
            }
        }
        
        H1Bias = Take(H1Neurons);
        
        float[] h2kernelKeras = Take(H1Neurons * H2Neurons);
        H2Kernel = new float[H1Neurons * H2Neurons];
        
        for (int r = 0; r < H1Neurons; r++) {
            int kerasRow = r * H2Neurons;
            for (int c = 0; c < H2Neurons; c++) {
                H2Kernel[c * H1Neurons + r] = h2kernelKeras[kerasRow + c];
            }
        }
        
        H2Bias       = Take(H2Neurons);
        OutputKernel = Take(H2Neurons);
        OutputBias   = Take(1)[0];

        return;

        float[] Take(int count) {
            float[] slice = new float[count];
            Array.Copy(all, off, slice, 0, count);
            off += count;
            return slice;
        }
    }
}*/