using System.Numerics;
using ImGuiNET;

namespace StashValueTracker.UI;

/// <summary>Scoped ImGui style for the value window. Push() at the top of Draw, Pop() in a finally,
/// so styling never leaks into other plugins' UI. Counter-based pop is exception-safe.
/// NOT re-entrant: call Push()/Pop() exactly once per frame (don't nest).</summary>
public static class Theme
{
    private static int _colors;
    private static int _vars;

    public static void Push()
    {
        _colors = 0;
        _vars = 0;

        Color(ImGuiCol.Header, 0.20f, 0.34f, 0.45f, 0.85f);
        Color(ImGuiCol.HeaderHovered, 0.26f, 0.44f, 0.58f, 1f);
        Color(ImGuiCol.HeaderActive, 0.26f, 0.44f, 0.58f, 1f);
        Color(ImGuiCol.Button, 0.18f, 0.22f, 0.27f, 1f);
        Color(ImGuiCol.ButtonHovered, 0.26f, 0.44f, 0.58f, 1f);
        Color(ImGuiCol.ButtonActive, 0.30f, 0.52f, 0.68f, 1f);
        Color(ImGuiCol.FrameBg, 0.13f, 0.15f, 0.18f, 1f);
        Color(ImGuiCol.FrameBgHovered, 0.20f, 0.24f, 0.29f, 1f);
        Color(ImGuiCol.FrameBgActive, 0.24f, 0.30f, 0.36f, 1f);
        Color(ImGuiCol.CheckMark, 0.40f, 0.70f, 0.95f, 1f);
        Color(ImGuiCol.TableHeaderBg, 0.16f, 0.19f, 0.23f, 1f);
        Color(ImGuiCol.TableRowBgAlt, 1f, 1f, 1f, 0.025f);

        Var(ImGuiStyleVar.FrameRounding, 4f);
        Var(ImGuiStyleVar.GrabRounding, 4f);
        Var(ImGuiStyleVar.FrameBorderSize, 1f);
        Var(ImGuiStyleVar.FramePadding, new Vector2(6, 4));
        Var(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6));
        Var(ImGuiStyleVar.CellPadding, new Vector2(6, 3));
        Var(ImGuiStyleVar.IndentSpacing, 14f);
    }

    public static void Pop()
    {
        ImGui.PopStyleVar(_vars);
        ImGui.PopStyleColor(_colors);
        _vars = 0;
        _colors = 0;
    }

    private static void Color(ImGuiCol target, float r, float g, float b, float a)
    {
        ImGui.PushStyleColor(target, new Vector4(r, g, b, a));
        _colors++;
    }

    private static void Var(ImGuiStyleVar target, float value)
    {
        ImGui.PushStyleVar(target, value);
        _vars++;
    }

    private static void Var(ImGuiStyleVar target, Vector2 value)
    {
        ImGui.PushStyleVar(target, value);
        _vars++;
    }
}
