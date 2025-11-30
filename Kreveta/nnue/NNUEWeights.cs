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
    internal static short[] Embedding;    // [40960 * 256]
    
    private static short[] S1_H1Kernel; // [16 * 512]
    private static int[]   S1_H1Bias;   // [16]
    private static short[] S1_H2Kernel; // [32  * 16]
    private static int[]   S1_H2Bias;   // [32]

    private static short[] S2_H1Kernel;
    private static int[]   S2_H1Bias;
    private static short[] S2_H2Kernel;
    private static int[]   S2_H2Bias;

    private static short[] S3_H1Kernel;
    private static int[]   S3_H1Bias;
    private static short[] S3_H2Kernel;
    private static int[]   S3_H2Bias;

    private static short[] S4_H1Kernel;
    private static int[]   S4_H1Bias;
    private static short[] S4_H2Kernel;
    private static int[]   S4_H2Bias;
    
    internal static short[] OutputKernel; // [32]
    internal static int     OutputBias;

    internal static short[][] H1Kernels;
    internal static short[][] H2Kernels;
    internal static int[][]   H1Biases;
    internal static int[][]   H2Biases;

    private  const int FeatCount = 40960;
    internal const int EmbedDims = 128;
    internal const int H1Neurons = 16;
    internal const int H2Neurons = 16;

    // global quantization constant
    internal const float Scale = 1024f;

    internal static void Load(string binPath) {
    byte[] rawBytes = File.ReadAllBytes(binPath);
    float[] all = new float[rawBytes.Length / 4];
    Buffer.BlockCopy(rawBytes, 0, all, 0, rawBytes.Length);

    int offset = 0;

    // === 1. EMBEDDING ===
    const int embedLen = FeatCount * EmbedDims;
    Embedding = new short[embedLen];
    for (int i = 0; i < embedLen; i++)
        Embedding[i] = Quantize(all[offset++]);

    // allocate subnet buffers
    S1_H1Kernel = new short[H1Neurons * EmbedDims * 2];
    S1_H1Bias   = new int[H1Neurons];
    S1_H2Kernel = new short[H2Neurons * H1Neurons];
    S1_H2Bias   = new int[H2Neurons];

    S2_H1Kernel = new short[S1_H1Kernel.Length];
    S2_H1Bias   = new int[S1_H1Bias.Length];
    S2_H2Kernel = new short[S1_H2Kernel.Length];
    S2_H2Bias   = new int[S1_H2Bias.Length];

    S3_H1Kernel = new short[S1_H1Kernel.Length];
    S3_H1Bias   = new int[S1_H1Bias.Length];
    S3_H2Kernel = new short[S1_H2Kernel.Length];
    S3_H2Bias   = new int[S1_H2Bias.Length];

    S4_H1Kernel = new short[S1_H1Kernel.Length];
    S4_H1Bias   = new int[S1_H1Bias.Length];
    S4_H2Kernel = new short[S1_H2Kernel.Length];
    S4_H2Bias   = new int[S1_H2Bias.Length];

    short[][] H1K = [ S1_H1Kernel, S2_H1Kernel, S3_H1Kernel, S4_H1Kernel ];
    int[][]   H1B = [ S1_H1Bias,   S2_H1Bias,   S3_H1Bias,   S4_H1Bias ];
    short[][] H2K = [ S1_H2Kernel, S2_H2Kernel, S3_H2Kernel, S4_H2Kernel ];
    int[][]   H2B = [ S1_H2Bias,   S2_H2Bias,   S3_H2Bias,   S4_H2Bias ];

    // === 2. LOAD SUBNETS IN REAL KERAS ORDER ===
    for (int subnet = 0; subnet < 4; subnet++) {
        LoadH1Kernel(all, H1K[subnet], ref offset);
        LoadH1Bias(all, H1B[subnet], ref offset);
    }

    for (int subnet = 0; subnet < 4; subnet++) {
        LoadH2Kernel(all, H2K[subnet], ref offset);
        LoadH2Bias(all, H2B[subnet], ref offset);
    }

    H1Kernels = H1K;
    H2Kernels = H2K;
    H1Biases  = H1B;
    H2Biases  = H2B;

    // === 3. OUTPUT LAYER ===
    OutputKernel = new short[H2Neurons];
    for (int i = 0; i < H2Neurons; i++)
        OutputKernel[i] = Quantize(all[offset++]);

    OutputBias = (int)MathF.Round(all[offset] * Scale);
}

// ---------- HELPERS (preserve neuron-major order) ----------

private static void LoadH1Kernel(float[] all, short[] dest, ref int offset) {
    const int rows = EmbedDims * 2;
    const int cols = H1Neurons;

    for (int r = 0; r < rows; r++) {
        int baseIdx = offset + r * cols;
        for (int c = 0; c < cols; c++) {
            int dst = c * rows + r;
            dest[dst] = Quantize(all[baseIdx + c]);
        }
    }

    offset += rows * cols;
}

private static void LoadH1Bias(float[] all, int[] dest, ref int offset) {
    for (int i = 0; i < dest.Length; i++)
        dest[i] = (int)MathF.Round(all[offset++] * Scale);
}

private static void LoadH2Kernel(float[] all, short[] dest, ref int offset) {
    const int rows = H1Neurons;
    const int cols = H2Neurons;

    for (int r = 0; r < rows; r++) {
        int baseIdx = offset + r * cols;
        for (int c = 0; c < cols; c++) {
            int dst = c * rows + r;
            dest[dst] = Quantize(all[baseIdx + c]);
        }
    }

    offset += rows * cols;
}

private static void LoadH2Bias(float[] all, int[] dest, ref int offset) {
    for (int i = 0; i < dest.Length; i++)
        dest[i] = (int)MathF.Round(all[offset++] * Scale);
}

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short Quantize(float x) {
        float v = x * Scale;
        v = MathF.Max(-32768f, MathF.Min(32767f, v));
        return (short)MathF.Round(v);
    }
}