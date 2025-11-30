//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

// Consider making public types internal
#pragma warning disable CA1515

// Mark members as static
#pragma warning disable CA1822

using BenchmarkDotNet.Attributes;

using Kreveta.consts;
using Kreveta.movegen;
using Kreveta.nnue;

using System;
// ReSharper disable InconsistentNaming

namespace Kreveta;

/*
 * CURRENT BENCHMARKS:
 * Board.PlayMove             23.17 ns
 * Board.PlayReversibleMove   21.02 ns
 * Eval.StaticEval            96.31 ns
 * ZobristHash.GetHash        23.47 ns
 * Movegen.GetLegalMoves      383.5 ns
 *
 *
 * 
 * FASTEST PERFT 6            1.443 s
 * FASTEST PERFT 7            24.83 s
 */

[MemoryDiagnoser]
[RankColumn, Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class Benchmarks {
    
    [GlobalSetup]
    public void Setup() {
    }
}

#pragma warning restore CA1822
#pragma warning restore CA1515

#pragma warning restore IDE0079