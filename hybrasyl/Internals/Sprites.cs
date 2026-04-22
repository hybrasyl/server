// This file is part of Project Hybrasyl.
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
//
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
//
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
//
// (C) 2020-2023 ERISCO, LLC
//
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using System.Collections.Generic;

namespace Hybrasyl.Internals;

/// <summary>
///     Physical orientation of a door. East/West doors run along the screen's NW-to-SE diagonal and flip across
///     the x-axis (IsLeftRight == true on the wire). North/South doors run along the NE-to-SW diagonal and flip
///     across the y-axis.
/// </summary>
public enum DoorAxis
{
    EastWest,
    NorthSouth
}

/// <summary>
///     One physical door in retail DA, described as the full set of closed-state sprites and open-state sprites
///     for every panel that makes up the door. Sourced from Chaos.Client/docs/doors.md (hand-audited against retail
///     assets — the DarkAges.exe extraction at 0x0068b8b0 that was previously used to seed this data was incomplete
///     and contained junk/cross-paired entries, see docs/better-doors-plan.md).
///     <br /><br />
///     Door types captured:
///     <list type="bullet">
///         <item>1-tile doors: single sprite pair (e.g. 12484/12485).</item>
///         <item>2-tile doors: two-panel, both flip (e.g. 1993,1994 / 1996,1997).</item>
///         <item>3-tile "all change" doors: three-panel, all three flip (e.g. 2163-65 / 2167-69).</item>
///         <item>
///             3-tile "only center changes" doors: three panels exist visually but only the center sprite toggles;
///             the side panels are static jamb art (<see cref="OnlyCenterChanges" /> is true).
///         </item>
///         <item>4-tile doors: four-panel, all flip (e.g. 14874-77 / 14904-07).</item>
///         <item>
///             Archways: permanently-open, no closed state (<see cref="HasClosedVersion" /> is false).
///             Represented so map-load can recognize the sprite, but they are not interactive and do not produce
///             <see cref="DoorSpriteInfo.IsToggling" /> entries.
///         </item>
///     </list>
/// </summary>
public sealed record DoorDefinition(
    ushort[] ClosedSprites,
    ushort[] OpenSprites,
    DoorAxis Axis,
    bool OnlyCenterChanges,
    bool HasClosedVersion)
{
    public int PanelCount => HasClosedVersion ? ClosedSprites.Length : OpenSprites.Length;
    public bool IsLeftRight => Axis == DoorAxis.EastWest;

    //for odd panel counts (1 or 3) this is the true center; for even panel counts we don't use it anyway
    //(OnlyCenterChanges is only observed on 3-tile doors in doors.md).
    public int CenterPanelIndex => PanelCount / 2;

    /// <summary>
    ///     True when the panel at <paramref name="panelIndex" /> actually flips sprites on toggle. Archways
    ///     never toggle; center-only doors only toggle the middle panel.
    /// </summary>
    public bool IsPanelToggling(int panelIndex)
    {
        if (!HasClosedVersion)
            return false;

        if (!OnlyCenterChanges)
            return true;

        return panelIndex == CenterPanelIndex;
    }
}

/// <summary>
///     Reverse lookup entry: given a sprite ID, describes which <see cref="DoorDefinition" /> it belongs to, its
///     position within that door, and whether it's the closed-state or open-state sprite at that position.
/// </summary>
public sealed record DoorSpriteInfo(
    DoorDefinition Definition,
    int PanelIndex,
    bool IsOpenState)
{
    public bool IsCenter => PanelIndex == Definition.CenterPanelIndex;
    public bool IsToggling => Definition.IsPanelToggling(PanelIndex);
}

public static class Sprites
{
    /// <summary>
    ///     Full catalog of every door in retail DA. 81 definitions covering 1/2/3/4-tile doors and permanently-open
    ///     archways, across N/S and E/W orientations. Order within the array has no semantic meaning — looked up
    ///     by sprite ID via <see cref="SpriteInfo" />.
    /// </summary>
    public static readonly DoorDefinition[] Definitions =
    [
        //--- E/W 2-tile doors (18xxx series — Undine/newer content) ---
        new([18708, 18709], [18711, 18712], DoorAxis.EastWest, false, true),
        new([18649, 18695], [18697, 18698], DoorAxis.EastWest, false, true),
        new([18666, 18667], [18669, 18670], DoorAxis.EastWest, false, true),
        new([18589, 18590], [18594, 18595], DoorAxis.EastWest, false, true),
        new([18576, 18577], [18580, 18581], DoorAxis.EastWest, false, true),
        new([18489, 18490], [18492, 18493], DoorAxis.EastWest, false, true),
        new([18559, 18560], [18562, 18563], DoorAxis.EastWest, false, true),
        new([18533, 18534], [18536, 18537], DoorAxis.EastWest, false, true),
        new([18503, 18504], [18506, 18507], DoorAxis.EastWest, false, true),
        new([18496, 18497], [18499, 18500], DoorAxis.EastWest, false, true),
        new([18424, 18425], [18429, 18430], DoorAxis.EastWest, false, true),
        new([18411, 18412], [18415, 18416], DoorAxis.EastWest, false, true),
        new([18659, 18660], [18662, 18663], DoorAxis.EastWest, false, true),
        new([18652, 18653], [18655, 18656], DoorAxis.EastWest, false, true),

        //--- E/W 3-tile center-only doors (30xx/31xx — Piet/Undine city gates) ---
        new([3058, 3059, 3060], [3066, 3067, 3068], DoorAxis.EastWest, true, true),
        new([3118, 3119, 3120], [3126, 3127, 3128], DoorAxis.EastWest, true, true),
        new([3178, 3179, 3180], [3186, 3187, 3188], DoorAxis.EastWest, true, true),

        //--- E/W 2-tile doors (mid range) ---
        new([11944, 11945], [11941, 11942], DoorAxis.EastWest, false, true),
        new([1993, 1994], [1996, 1997], DoorAxis.EastWest, false, true),

        //--- E/W 1-tile doors (12xxx singletons) ---
        new([12183], [12179], DoorAxis.EastWest, false, true),
        new([12273], [12234], DoorAxis.EastWest, false, true),
        new([12379], [11448], DoorAxis.EastWest, false, true),
        new([12380], [11445], DoorAxis.EastWest, false, true),

        //--- E/W 3-tile center-only doors (13xxx, 2xxx range) ---
        new([13832, 13833, 13834], [13824, 13825, 13826], DoorAxis.EastWest, true, true),
        new([2435, 2436, 2437], [2431, 2432, 2433], DoorAxis.EastWest, true, true),
        new([2897, 2898, 2899], [2903, 2904, 2905], DoorAxis.EastWest, true, true),
        new([2945, 2946, 2947], [2951, 2952, 2953], DoorAxis.EastWest, true, true),
        new([2993, 2994, 2995], [2999, 3000, 3001], DoorAxis.EastWest, true, true),
        new([12688, 12689, 12690], [12692, 12693, 12694], DoorAxis.EastWest, true, true),
        new([12702, 12703, 12704], [12706, 12707, 12708], DoorAxis.EastWest, true, true),

        //--- E/W 3-tile all-change doors (2xxx range) ---
        new([2291, 2292, 2293], [2295, 2296, 2297], DoorAxis.EastWest, false, true),
        new([2227, 2228, 2229], [2231, 2232, 2233], DoorAxis.EastWest, false, true),
        new([2163, 2164, 2165], [2167, 2168, 2169], DoorAxis.EastWest, false, true),
        new([2874, 2875, 2876], [2881, 2882, 2883], DoorAxis.EastWest, false, true),

        //--- E/W 4-tile doors (14xxx/15xxx) ---
        new([14874, 14875, 14876, 14877], [14904, 14905, 14906, 14907], DoorAxis.EastWest, false, true),
        new([15334, 15335, 15336, 15337], [15364, 15365, 15366, 15367], DoorAxis.EastWest, false, true),

        //--- E/W 2-tile doors (27xx range) ---
        new([2761, 2762], [2768, 2769], DoorAxis.EastWest, false, true),
        new([2688, 2689], [2695, 2696], DoorAxis.EastWest, false, true),
        new([2727, 2728], [2734, 2735], DoorAxis.EastWest, false, true),

        //--- E/W archways (no closed version) — permanently-open decorative doorways ---
        new([], [13952, 13953, 13954], DoorAxis.EastWest, false, false),
        new([], [4519, 4520, 4521], DoorAxis.EastWest, false, false),
        new([], [4523, 4524, 4525], DoorAxis.EastWest, false, false),
        new([], [4527, 4528, 4529], DoorAxis.EastWest, false, false),

        //--- N/S 2-tile doors (18xxx series) ---
        new([18714, 18715], [18718, 18719], DoorAxis.NorthSouth, false, true),
        new([18700, 18701], [18704, 18705], DoorAxis.NorthSouth, false, true),
        new([18686, 18687], [18690, 18691], DoorAxis.NorthSouth, false, true),
        new([18679, 18680], [18683, 18684], DoorAxis.NorthSouth, false, true),
        new([18672, 18673], [18676, 18677], DoorAxis.NorthSouth, false, true),
        new([18631, 18632], [18635, 18636], DoorAxis.NorthSouth, false, true),
        new([18610, 18611], [18612, 18613], DoorAxis.NorthSouth, false, true),
        new([18565, 18566], [18570, 18571], DoorAxis.NorthSouth, false, true),
        new([18539, 18540], [18543, 18544], DoorAxis.NorthSouth, false, true),
        new([18524, 18525], [18529, 18530], DoorAxis.NorthSouth, false, true),
        new([18516, 18517], [18521, 18522], DoorAxis.NorthSouth, false, true),
        new([18509, 18510], [18513, 18514], DoorAxis.NorthSouth, false, true),
        new([18466, 18467], [18470, 18471], DoorAxis.NorthSouth, false, true),
        new([18445, 18446], [18447, 18448], DoorAxis.NorthSouth, false, true),

        //--- N/S 2-tile doors (mid range) ---
        new([2776, 2777], [2783, 2784], DoorAxis.NorthSouth, false, true),
        new([2714, 2715], [2721, 2722], DoorAxis.NorthSouth, false, true),
        new([2673, 2674], [2680, 2681], DoorAxis.NorthSouth, false, true),
        new([2000, 2001], [2003, 2004], DoorAxis.NorthSouth, false, true),
        new([11916, 11917], [11919, 11920], DoorAxis.NorthSouth, false, true),

        //--- N/S 3-tile all-change doors ---
        new([2850, 2851, 2852], [2857, 2858, 2859], DoorAxis.NorthSouth, false, true),
        new([2328, 2329, 2330], [2324, 2325, 2326], DoorAxis.NorthSouth, false, true),
        new([2260, 2261, 2262], [2264, 2265, 2266], DoorAxis.NorthSouth, false, true),
        new([2196, 2197, 2198], [2192, 2193, 2194], DoorAxis.NorthSouth, false, true),

        //--- N/S 3-tile center-only doors ---
        new([3018, 3019, 3020], [3024, 3025, 3026], DoorAxis.NorthSouth, true, true),
        new([2970, 2971, 2972], [2976, 2977, 2978], DoorAxis.NorthSouth, true, true),
        new([2928, 2929, 2930], [2922, 2923, 2924], DoorAxis.NorthSouth, true, true),
        new([2460, 2461, 2462], [2464, 2465, 2466], DoorAxis.NorthSouth, true, true),
        new([3209, 3210, 3211], [3217, 3218, 3219], DoorAxis.NorthSouth, true, true),
        new([3149, 3150, 3151], [3157, 3158, 3159], DoorAxis.NorthSouth, true, true),
        new([3089, 3090, 3091], [3097, 3098, 3099], DoorAxis.NorthSouth, true, true),

        //--- N/S 4-tile doors ---
        new([15338, 15339, 15340, 15341], [15368, 15369, 15370, 15371], DoorAxis.NorthSouth, false, true),
        new([14878, 14879, 14880, 14881], [14908, 14909, 14910, 14911], DoorAxis.NorthSouth, false, true),

        //--- N/S 1-tile doors ---
        new([12484], [12485], DoorAxis.NorthSouth, false, true),
        new([12266], [12271], DoorAxis.NorthSouth, false, true),
        new([8262], [8263], DoorAxis.NorthSouth, false, true),

        //--- N/S archways (no closed version) ---
        new([], [4540, 4541, 4542], DoorAxis.NorthSouth, false, false),
        new([], [4536, 4537, 4538], DoorAxis.NorthSouth, false, false),
        new([], [4532, 4533, 4534], DoorAxis.NorthSouth, false, false)
    ];

    /// <summary>
    ///     Lookup from any recognized door sprite (closed or open, archway or toggling) to the
    ///     <see cref="DoorDefinition" /> it belongs to, its panel index within that door, and whether it is the
    ///     closed-state or open-state sprite at that panel position. Populated at class init from
    ///     <see cref="Definitions" />.
    /// </summary>
    public static readonly Dictionary<ushort, DoorSpriteInfo> SpriteInfo;

    static Sprites()
    {
        var spriteInfo = new Dictionary<ushort, DoorSpriteInfo>();

        foreach (var def in Definitions)
        {
            if (def.HasClosedVersion)
                for (var i = 0; i < def.ClosedSprites.Length; i++)
                    spriteInfo[def.ClosedSprites[i]] = new DoorSpriteInfo(def, i, IsOpenState: false);

            for (var i = 0; i < def.OpenSprites.Length; i++)
                spriteInfo[def.OpenSprites[i]] = new DoorSpriteInfo(def, i, IsOpenState: true);
        }

        SpriteInfo = spriteInfo;
    }
}
