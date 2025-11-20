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

public static class Sprites
{
    public static readonly Dictionary<ushort, ushort> ClosedDoorSprites = new()
    {
        { 1994, 1997 }, { 2000, 2003 }, { 2163, 2164 }, { 2165, 2196 }, { 2197, 2198 }, { 2227, 2228 },
        { 2229, 2260 }, { 2261, 2262 }, { 2291, 2292 }, { 2293, 2328 }, { 2329, 2330 }, { 2432, 2436 },
        { 2461, 2465 }, { 2673, 2674 }, { 2675, 2680 }, { 2681, 2682 }, { 2687, 2688 }, { 2689, 2694 },
        { 2695, 2696 }, { 2714, 2715 }, { 2721, 2722 }, { 2727, 2728 }, { 2734, 2735 }, { 2761, 2762 },
        { 2768, 2769 }, { 2776, 2777 }, { 2783, 2784 }, { 2850, 2851 }, { 2852, 2857 }, { 2858, 2859 },
        { 2874, 2875 }, { 2876, 2881 }, { 2882, 2883 }, { 2897, 2898 }, { 2903, 2904 }, { 2923, 2924 },
        { 2929, 2930 }, { 2945, 2946 }, { 2951, 2952 }, { 2971, 2972 }, { 2977, 2978 }, { 2993, 2994 },
        { 2999, 3000 }, { 3019, 3020 }, { 3025, 3026 }, { 3058, 3059 }, { 3066, 3067 }, { 3090, 3091 },
        { 3098, 3099 }, { 3118, 3119 }, { 3126, 3127 }, { 3150, 3151 }, { 3158, 3159 }, { 3178, 3179 },
        { 3186, 3187 }, { 3210, 3211 }, { 3218, 3219 }, { 4519, 4520 }, { 4521, 4523 }, { 4524, 4525 },
        { 4527, 4528 }, { 4529, 4532 }, { 4533, 4534 }, { 4536, 4537 }, { 4538, 4540 }, { 4541, 4542 }
    };

    public static readonly Dictionary<ushort, ushort> OpenDoorSprites = new()
    {
        { 1997, 1994 }, { 2003, 2000 }, { 2164, 2163 }, { 2196, 2165 }, { 2198, 2197 }, { 2228, 2227 },
        { 2260, 2229 }, { 2262, 2261 }, { 2292, 2291 }, { 2328, 2293 }, { 2330, 2329 }, { 2436, 2432 },
        { 2465, 2461 }, { 2674, 2673 }, { 2680, 2675 }, { 2682, 2681 }, { 2688, 2687 }, { 2694, 2689 },
        { 2696, 2695 }, { 2715, 2714 }, { 2722, 2721 }, { 2728, 2727 }, { 2735, 2734 }, { 2762, 2761 },
        { 2769, 2768 }, { 2777, 2776 }, { 2784, 2783 }, { 2851, 2850 }, { 2857, 2852 }, { 2859, 2858 },
        { 2875, 2874 }, { 2881, 2876 }, { 2883, 2882 }, { 2898, 2897 }, { 2904, 2903 }, { 2924, 2923 },
        { 2930, 2929 }, { 2946, 2945 }, { 2952, 2951 }, { 2972, 2971 }, { 2978, 2977 }, { 2994, 2993 },
        { 3000, 2999 }, { 3020, 3019 }, { 3026, 3025 }, { 3059, 3058 }, { 3067, 3066 }, { 3091, 3090 },
        { 3099, 3098 }, { 3119, 3118 }, { 3127, 3126 }, { 3151, 3150 }, { 3159, 3158 }, { 3179, 3178 },
        { 3187, 3186 }, { 3211, 3210 }, { 3219, 3218 }, { 4520, 4519 }, { 4523, 4521 }, { 4525, 4524 },
        { 4528, 4527 }, { 4532, 4529 }, { 4534, 4533 }, { 4537, 4536 }, { 4540, 4538 }, { 4542, 4541 }
    };

    public static readonly Dictionary<ushort, bool> DoorSprites = new()
    {
        { 1994, true }, { 1997, true }, { 2000, true }, { 2003, true }, { 2163, true },
        { 2164, true }, { 2165, true }, { 2196, true }, { 2197, true }, { 2198, true },
        { 2227, true }, { 2228, true }, { 2229, true }, { 2260, true }, { 2261, true },
        { 2262, true }, { 2291, true }, { 2292, true }, { 2293, true }, { 2328, true },
        { 2329, true }, { 2330, true }, { 2432, true }, { 2436, true }, { 2461, true },
        { 2465, true }, { 2673, true }, { 2674, true }, { 2675, true }, { 2680, true },
        { 2681, true }, { 2682, true }, { 2687, true }, { 2688, true }, { 2689, true },
        { 2694, true }, { 2695, true }, { 2696, true }, { 2714, true }, { 2715, true },
        { 2721, true }, { 2722, true }, { 2727, true }, { 2728, true }, { 2734, true },
        { 2735, true }, { 2761, true }, { 2762, true }, { 2768, true }, { 2769, true },
        { 2776, true }, { 2777, true }, { 2783, true }, { 2784, true }, { 2850, true },
        { 2851, true }, { 2852, true }, { 2857, true }, { 2858, true }, { 2859, true },
        { 2874, true }, { 2875, true }, { 2876, true }, { 2881, true }, { 2882, true },
        { 2883, true }, { 2897, true }, { 2898, true }, { 2903, true }, { 2904, true },
        { 2923, true }, { 2924, true }, { 2929, true }, { 2930, true }, { 2945, true },
        { 2946, true }, { 2951, true }, { 2952, true }, { 2971, true }, { 2972, true },
        { 2977, true }, { 2978, true }, { 2993, true }, { 2994, true }, { 2999, true },
        { 3000, true }, { 3019, true }, { 3020, true }, { 3025, true }, { 3026, true },
        { 3058, true }, { 3059, true }, { 3066, true }, { 3067, true }, { 3090, true },
        { 3091, true }, { 3098, true }, { 3099, true }, { 3118, true }, { 3119, true },
        { 3126, true }, { 3127, true }, { 3150, true }, { 3151, true }, { 3158, true },
        { 3159, true }, { 3178, true }, { 3179, true }, { 3186, true }, { 3187, true },
        { 3210, true }, { 3211, true }, { 3218, true }, { 3219, true }, { 4519, true },
        { 4520, true }, { 4521, true }, { 4523, true }, { 4524, true }, { 4525, true },
        { 4527, true }, { 4528, true }, { 4529, true }, { 4532, true }, { 4533, true },
        { 4534, true }, { 4536, true }, { 4537, true }, { 4538, true }, { 4540, true },
        { 4541, true }, { 4542, true }
    };
}