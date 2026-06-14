using System;
using System.Linq;
using ExileCore2;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.PoEMemory.Models;

namespace StashValueTracker.Pricing;

/// <summary>Wraps NinjaPricer's PluginBridge methods and expresses values in exalted.</summary>
public sealed class NinjaPricerBridge
{
    private readonly GameController _gc;
    private readonly Action<string> _logError;

    private Func<Entity, double>? _getEntityValue;
    private Func<BaseItemType, double>? _getBaseValue;
    private DateTime _nextRetry = DateTime.MinValue;

    private readonly TimeSpan _ratesExpiry = TimeSpan.FromMinutes(5);
    private DateTime _ratesRefreshedAt = DateTime.MinValue;
    private double _exaltedUnit;   // bridge value of one Exalted Orb (≈1)
    private double _divineUnit;    // bridge value of one Divine Orb

    public NinjaPricerBridge(GameController gc, Action<string> logError)
    {
        _gc = gc;
        _logError = logError;
    }

    public bool IsAvailable => EnsureMethods();

    public bool PricesReady
    {
        get { RefreshRates(); return _exaltedUnit > 0; }
    }

    public double DivinePerExalted
    {
        get { RefreshRates(); return _divineUnit > 0 ? _exaltedUnit / _divineUnit : 0; }
    }

    /// <summary>Exalted value of a live item entity's whole stack (the bridge already includes stack size). 0 if unavailable/unpriced.</summary>
    public double ExaltedValueOfStack(Entity item)
    {
        if (item == null || !EnsureMethods()) return 0;
        RefreshRates();
        if (_exaltedUnit <= 0) return 0;
        try
        {
            var v = _getEntityValue!(item);
            return v > 0 ? v / _exaltedUnit : 0;
        }
        catch (Exception ex)
        {
            _logError($"error pricing item: {ex.Message}");
            return 0;
        }
    }

    private bool EnsureMethods()
    {
        if (_getEntityValue != null && _getBaseValue != null) return true;
        if (DateTime.Now < _nextRetry) return false;

        _getEntityValue ??= _gc.PluginBridge.GetMethod<Func<Entity, double>>("NinjaPrice.GetValue");
        _getBaseValue ??= _gc.PluginBridge.GetMethod<Func<BaseItemType, double>>("NinjaPrice.GetBaseItemTypeValue");

        if (_getEntityValue == null || _getBaseValue == null)
        {
            _nextRetry = DateTime.Now.AddSeconds(2);
            return false;
        }
        return true;
    }

    private void RefreshRates()
    {
        if (!EnsureMethods()) return;
        if (DateTime.Now - _ratesRefreshedAt < _ratesExpiry && _exaltedUnit > 0) return;

        try
        {
            var exalted = FindBase("Exalted Orb");
            var divine = FindBase("Divine Orb");
            if (exalted != null) { var v = _getBaseValue!(exalted); if (v > 0) _exaltedUnit = v; }
            if (divine != null) { var v = _getBaseValue!(divine); if (v > 0) _divineUnit = v; }
            if (_exaltedUnit > 0) _ratesRefreshedAt = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logError($"error refreshing orb rates: {ex.Message}");
        }
    }

    private BaseItemType? FindBase(string baseName) =>
        _gc.Files.BaseItemTypes.Contents.Values
            .FirstOrDefault(b => b != null && string.Equals(b.BaseName, baseName, StringComparison.OrdinalIgnoreCase));
}
