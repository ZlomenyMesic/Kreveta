//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using BenchmarkDotNet.Attributes;

namespace Kreveta;

[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class Benchmarks {

    [GlobalSetup]
    public void Setup() {
    }
}
