using System.Collections.Generic;
using StashValueTracker.Model;
using StashValueTracker.Tabs;
using Xunit;

namespace StashValueTracker.Tests;

public class TabFingerprintTests
{
    private static ItemSnapshot It(string group, int stack) =>
        new() { DisplayName = group, GroupKey = group, StackSize = stack, TotalValueEx = 1 };

    [Fact]
    public void Same_multiset_different_order_is_equal()
    {
        var a = TabFingerprint.Compute(new[] { It("Exalted Orb", 10), It("Divine Orb", 2) });
        var b = TabFingerprint.Compute(new[] { It("Divine Orb", 2), It("Exalted Orb", 10) });
        Assert.Equal(a, b);
    }

    [Fact]
    public void Different_content_differs()
    {
        var a = TabFingerprint.Compute(new[] { It("Exalted Orb", 10) });
        var b = TabFingerprint.Compute(new[] { It("Exalted Orb", 11) });
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Empty_is_stable_constant()
    {
        Assert.Equal(TabFingerprint.Compute(new ItemSnapshot[0]),
                     TabFingerprint.Compute(new List<ItemSnapshot>()));
    }

    [Fact]
    public void Duplicate_stacks_do_not_cancel_to_empty()
    {
        // Two identical entries must NOT fold away (a sum fold, not XOR).
        var two = new[] { It("Ex", 5), It("Ex", 5) };
        Assert.NotEqual(TabFingerprint.Compute(two), TabFingerprint.Compute(new ItemSnapshot[0]));
    }
}
