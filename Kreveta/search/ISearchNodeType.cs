//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// ReSharper disable InconsistentNaming
namespace Kreveta.search;

internal interface ISearchNodeType;

internal struct RootNode  : ISearchNodeType;
internal struct PVNode    : ISearchNodeType;
internal struct NonPVNode : ISearchNodeType;