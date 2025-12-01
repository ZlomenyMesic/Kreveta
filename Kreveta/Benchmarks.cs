//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
/*#pragma warning disable IDE0079

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

[MemoryDiagnoser]
[RankColumn, Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class Benchmarks {
    
    [GlobalSetup]
    public void Setup() {
    }
}

#pragma warning restore CA1822
#pragma warning restore CA1515

#pragma warning restore IDE0079*/