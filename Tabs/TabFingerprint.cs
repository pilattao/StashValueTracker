using System.Collections.Generic;
using StashValueTracker.Model;

namespace StashValueTracker.Tabs;

/// <summary>Order-independent hash of a tab's contents (multiset of GroupKey + StackSize),
/// used to reunite a renamed/moved/recoloured tab with its stored snapshot.</summary>
public static class TabFingerprint
{
    public static long Compute(IEnumerable<ItemSnapshot> items)
    {
        long acc = 1469598103934665603; // FNV offset basis, used as a commutative accumulator seed
        if (items == null) return acc;
        foreach (var i in items)
        {
            // Per-entry hash, then SUM-fold so order does not matter. (Sum, not XOR: XOR would
            // cancel duplicate entries — two identical stacks would vanish, colliding with an
            // empty tab. Addition is the correct order-independent multiset fold.)
            var name = i?.GroupKey ?? "";
            long h = 1125899906842597;
            foreach (var c in name) h = h * 31 + c;
            h = h * 1000003 + (i?.StackSize ?? 0);
            unchecked { acc += h; }
        }
        return acc;
    }
}
