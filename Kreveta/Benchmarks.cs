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

    [Benchmark]
    public void ForArray() {
        int[] arr = [1, 2, 3, 4, 5, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15];

        for (int i = 0; i < arr.Length; i++) {
            int x = arr[i];
        }
    }

    [Benchmark]
    public void ForeachArray() {
        int[] arr = [1, 2, 3, 4, 5, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15];

        foreach (int i in arr) {
            int x = i;
        }
    }

    [Benchmark]
    public void ForList() {
        List<int> list = [1, 2, 3, 4, 5, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15];

        for (int i = 0; i < list.Count; i++) {
            int x = list[i];
        }
    }

    [Benchmark]
    public void ForeachList() {
        List<int> list = [1, 2, 3, 4, 5, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15];

        foreach (int i in list) {
            int x = i;
        }
    }
}
