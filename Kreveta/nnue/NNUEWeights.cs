//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA1810

using Kreveta.uci;

using System;
using System.IO;
using System.Reflection;
// ReSharper disable InconsistentNaming

#pragma warning disable CS8618

namespace Kreveta.nnue;

internal static class NNUEWeights {
    internal static readonly short[]   Embedding;
    internal static readonly short[][] H1Kernels;
    internal static readonly short[][] H2Kernels;
    internal static readonly short[][] H1Biases;
    internal static readonly short[][] H2Biases;
    internal static readonly short[][] OutputKernels;
    internal static readonly short[]   OutputBiases;

    private  const int FeatCount = 40960;
    internal const int EmbedDims = 128;
    internal const int H1Neurons = 16;
    internal const int H2Neurons = 16;

    static NNUEWeights() {
        var asm = Assembly.GetExecutingAssembly();
        using Stream? stream = asm.GetManifestResourceStream($"{Program.Name}.{Program.Network}");

        if (stream is null) {
            UCI.Log("Embedded NNUE weights not found");
            return;
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);

        byte[]  rawBytes = ms.ToArray();
        short[] all      = new short[rawBytes.Length / 2];

        Buffer.BlockCopy(rawBytes, 0, all, 0, rawBytes.Length);

        int offset = 0;

        // === 1. EMBEDDING ===
        const int embedLen = FeatCount * EmbedDims;
        Embedding = new short[embedLen];
        for (int i = 0; i < embedLen; i++)
            Embedding[i] = all[offset++];

        // allocate subnet buffers
        short[] s1H1Kernel     = new short[H1Neurons * EmbedDims * 2];
        short[] s1H1Bias       = new short[H1Neurons];
        short[] s1H2Kernel     = new short[H2Neurons * H1Neurons];
        short[] s1H2Bias       = new short[H2Neurons];
        short[] s1OutputKernel = new short[H2Neurons];

        short[] s2H1Kernel     = new short[s1H1Kernel.Length];
        short[] s2H1Bias       = new short[s1H1Bias.Length];
        short[] s2H2Kernel     = new short[s1H2Kernel.Length];
        short[] s2H2Bias       = new short[s1H2Bias.Length];
        short[] s2OutputKernel = new short[H2Neurons];

        short[] s3H1Kernel     = new short[s1H1Kernel.Length];
        short[] s3H1Bias       = new short[s1H1Bias.Length];
        short[] s3H2Kernel     = new short[s1H2Kernel.Length];
        short[] s3H2Bias       = new short[s1H2Bias.Length];
        short[] s3OutputKernel = new short[H2Neurons];

        short[] s4H1Kernel     = new short[s1H1Kernel.Length];
        short[] s4H1Bias       = new short[s1H1Bias.Length];
        short[] s4H2Kernel     = new short[s1H2Kernel.Length];
        short[] s4H2Bias       = new short[s1H2Bias.Length];
        short[] s4OutputKernel = new short[H2Neurons];

        short[] s5H1Kernel     = new short[s1H1Kernel.Length];
        short[] s5H1Bias       = new short[s1H1Bias.Length];
        short[] s5H2Kernel     = new short[s1H2Kernel.Length];
        short[] s5H2Bias       = new short[s1H2Bias.Length];
        short[] s5OutputKernel = new short[H2Neurons];

        short[] s6H1Kernel     = new short[s1H1Kernel.Length];
        short[] s6H1Bias       = new short[s1H1Bias.Length];
        short[] s6H2Kernel     = new short[s1H2Kernel.Length];
        short[] s6H2Bias       = new short[s1H2Bias.Length];
        short[] s6OutputKernel = new short[H2Neurons];

        short[] s7H1Kernel     = new short[s1H1Kernel.Length];
        short[] s7H1Bias       = new short[s1H1Bias.Length];
        short[] s7H2Kernel     = new short[s1H2Kernel.Length];
        short[] s7H2Bias       = new short[s1H2Bias.Length];
        short[] s7OutputKernel = new short[H2Neurons];

        short[] s8H1Kernel     = new short[s1H1Kernel.Length];
        short[] s8H1Bias       = new short[s1H1Bias.Length];
        short[] s8H2Kernel     = new short[s1H2Kernel.Length];
        short[] s8H2Bias       = new short[s1H2Bias.Length];
        short[] s8OutputKernel = new short[H2Neurons];

        short[][] H1K = [
            s1H1Kernel, s2H1Kernel, s3H1Kernel, s4H1Kernel,
            s5H1Kernel, s6H1Kernel, s7H1Kernel, s8H1Kernel
        ];

        short[][] H1B = [
            s1H1Bias, s2H1Bias, s3H1Bias, s4H1Bias,
            s5H1Bias, s6H1Bias, s7H1Bias, s8H1Bias
        ];

        short[][] H2K = [
            s1H2Kernel, s2H2Kernel, s3H2Kernel, s4H2Kernel,
            s5H2Kernel, s6H2Kernel, s7H2Kernel, s8H2Kernel
        ];

        short[][] H2B = [
            s1H2Bias, s2H2Bias, s3H2Bias, s4H2Bias,
            s5H2Bias, s6H2Bias, s7H2Bias, s8H2Bias
        ];

        short[][] OK = [
            s1OutputKernel, s2OutputKernel, s3OutputKernel, s4OutputKernel,
            s5OutputKernel, s6OutputKernel, s7OutputKernel, s8OutputKernel
        ];

        // === 2. LOAD SUBNETS IN REAL KERAS ORDER ===
        for (int subnet = 0; subnet < 8; subnet++) {
            LoadH1Kernel(all, H1K[subnet], ref offset);
            LoadH1Bias  (all, H1B[subnet], ref offset);
        }

        for (int subnet = 0; subnet < 8; subnet++) {
            LoadH2Kernel(all, H2K[subnet], ref offset);
            LoadH2Bias  (all, H2B[subnet], ref offset);
        }

        short[] OB = new short[8];

        // === 3. LOAD OUTPUTS PER SUBNET ===
        for (int subnet = 0; subnet < 8; subnet++) {
            for (int i = 0; i < H2Neurons; i++)
                OK[subnet][i] = all[offset++];

            OB[subnet] = all[offset++];
        }

        H1Kernels = H1K;
        H2Kernels = H2K;
        H1Biases  = H1B;
        H2Biases  = H2B;

        OutputKernels = OK;
        OutputBiases  = OB;
    }

    private static void LoadH1Kernel(short[] all, short[] dest, ref int offset) {
        const int rows = EmbedDims * 2;

        for (int r = 0; r < rows; r++) {
            int baseIdx = offset + r * H1Neurons;
            for (int c = 0; c < H1Neurons; c++) {
                int dst = c * rows + r;
                dest[dst] = all[baseIdx + c];
            }
        }

        offset += rows * H1Neurons;
    }

    private static void LoadH1Bias(short[] all, short[] dest, ref int offset) {
        for (int i = 0; i < dest.Length; i++)
            dest[i] = all[offset++];
    }

    private static void LoadH2Kernel(short[] all, short[] dest, ref int offset) {
        for (int r = 0; r < H1Neurons; r++) {
            int baseIdx = offset + r * H2Neurons;
            for (int c = 0; c < H2Neurons; c++) {
                int dst = c * H1Neurons + r;
                dest[dst] = all[baseIdx + c];
            }
        }

        offset += H1Neurons * H2Neurons;
    }

    private static void LoadH2Bias(short[] all, short[] dest, ref int offset) {
        for (int i = 0; i < dest.Length; i++)
            dest[i] = all[offset++];
    }
}

#pragma warning restore CA1810