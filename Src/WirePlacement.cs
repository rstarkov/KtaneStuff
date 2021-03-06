﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using KtaneStuff.Modeling;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace KtaneStuff
{
    using static Md;

    static class WirePlacement
    {
        public static void DoModels()
        {
            File.WriteAllText(@"D:\c\KTANE\WirePlacement\Assets\Models\Base.obj", GenerateObjFile(Base(), "Base"));
            File.WriteAllText(@"D:\c\KTANE\WirePlacement\Assets\Models\WireCollider.obj", GenerateObjFile(WireCollider(), "WireCollider"));
        }

        const double _wireRadius = .0035;
        const double _firstControlHeight = .02;
        const double _interpolateHeight = .01;
        const double _firstControlHeightHighlight = .0175;
        const double _interpolateHeightHighlight = .0075;

        public static IEnumerable<VertexInfo[]> WireCollider()
        {
            var length = .0304;
            var numSegments = 3;

            var thickness = _wireRadius;
            var firstControlHeight = _firstControlHeight;
            var interpolateHeight = _interpolateHeight;

            var start = pt(0, 0, 0);
            var startControl = pt(length / 10, firstControlHeight, 0);
            var endControl = pt(length * 9 / 10, firstControlHeight, 0);
            var end = pt(length, 0, 0);

            var bézierSteps = 4;
            var tubeRevSteps = 6;

            var interpolateStart = pt(0, interpolateHeight, 0);
            var interpolateEnd = pt(length, interpolateHeight, 0);

            var intermediatePoints = Ut.NewArray(numSegments - 1, i => interpolateStart + (interpolateEnd - interpolateStart) * (i + 1) / numSegments);
            var deviations = Ut.NewArray(numSegments - 1, _ => pt(0, 0, 0));

            var points =
                new[] { new { ControlBefore = default(Pt), Point = start, ControlAfter = startControl } }
                .Concat(intermediatePoints.Select((p, i) => new { ControlBefore = p - deviations[i], Point = p, ControlAfter = p + deviations[i] }))
                .Concat(new[] { new { ControlBefore = endControl, Point = end, ControlAfter = default(Pt) } })
                .SelectConsecutivePairs(false, (one, two) => Bézier(one.Point, one.ControlAfter, two.ControlBefore, two.Point, bézierSteps))
                .SelectMany((x, i) => i == 0 ? x : x.Skip(1))
                .Select(p => pt(-p.X, p.Y, p.Z))
                .ToArray();
            return CreateMesh(false, true, tubeFromCurve(points, thickness, tubeRevSteps));
        }

        enum WireColor
        {
            Black,
            Blue,
            Red,
            White,
            Yellow
        }

        public static void CheatSheet()
        {
            var solutions = Ut.NewArray(
                new { Color = WireColor.Black, Locations = "B1,D4,A4,D2,B4" },
                new { Color = WireColor.Blue, Locations = "A2,C4,A1,C4,D4" },
                new { Color = WireColor.Blue, Locations = "C3,C2,C1,D3,B1" },
                new { Color = WireColor.Red, Locations = "A1,B3,C4,B2,B3" },
                new { Color = WireColor.Red, Locations = "C4,D3,B1,C1,C2" },
                new { Color = WireColor.White, Locations = "B2,C1,B4,A1,C1" },
                new { Color = WireColor.White, Locations = "D3,D2,D4,B3,B2" },
                new { Color = WireColor.Yellow, Locations = "A3,C3,A2,A4,A3" },
                new { Color = WireColor.Yellow, Locations = "D1,A1,B2,B4,A4" },
                new { Color = WireColor.Yellow, Locations = "D2,D1,D2,A2,D1" }
            );
            var colors = new[] { WireColor.Black, WireColor.Blue, WireColor.Red, WireColor.White, WireColor.Yellow };

            var s = "";
            foreach (var c3color in colors)
            {
                s += $@"<table class='wire-placement-embellish {c3color.ToString().ToLowerInvariant()}'><tr><th>A<th>B<th>C<th>D<td class='corner'></tr>{
                    Enumerable.Range(0, 4).Select(row => $"<tr>{Enumerable.Range(0, 4).Select(col => $"<td class='{solutions.FirstOrDefault(sol => sol.Locations.Split(',')[(int) c3color] == $"{(char) ('A' + col)}{(char) ('1' + row)}")?.Color.ToString().ToLowerInvariant() ?? "none"}'>").JoinString()}<th>{row + 1}</tr>").JoinString()
                }</table>";
            }
            var path = @"D:\c\KTANE\HTML\Wire Placement embellished (Timwi).html";
            File.WriteAllText(path, Regex.Replace(File.ReadAllText(path), @"(?<=<!--##-->).*(?=<!--###-->)", s, RegexOptions.Singleline));
        }

        private static MeshVertexInfo[][] tubeFromCurve(Pt[] pts, double radius, int revSteps)
        {
            var normals = new Pt[pts.Length];
            normals[0] = ((pts[1] - pts[0]) * pt(0, 1, 0)).Normalize() * radius;
            for (int i = 1; i < pts.Length - 1; i++)
                normals[i] = normals[i - 1].ProjectOntoPlane((pts[i + 1] - pts[i]) + (pts[i] - pts[i - 1])).Normalize() * radius;
            normals[pts.Length - 1] = normals[pts.Length - 2].ProjectOntoPlane(pts[pts.Length - 1] - pts[pts.Length - 2]).Normalize() * radius;

            var axes = pts.Select((p, i) =>
                i == 0 ? new { Start = pts[0], End = pts[1] } :
                i == pts.Length - 1 ? new { Start = pts[pts.Length - 2], End = pts[pts.Length - 1] } :
                new { Start = p, End = p + (pts[i + 1] - p) + (p - pts[i - 1]) }).ToArray();

            return Enumerable.Range(0, pts.Length)
                .Select(ix => new { Axis = axes[ix], Perp = pts[ix] + normals[ix], Point = pts[ix] })
                .Select(inf => Enumerable.Range(0, revSteps)
                    .Select(i => 360 * i / revSteps)
                    .Select(angle => inf.Perp.Rotate(inf.Axis.Start, inf.Axis.End, angle))
                    .Select(p => new MeshVertexInfo(p, p - inf.Point)).Reverse().ToArray())
                .ToArray();
        }

        public static IEnumerable<VertexInfo[]> Base()
        {
            var depth = .5;
            var width = 1;
            var dip = .2;
            var holeI = .16;
            var holeO = .2;
            var holeDepth = .5;
            var xf = width * .4;
            var bevelWidth = .1;
            var bevelF = bevelWidth * .4;
            var bézierSteps = 10;
            var roundSteps = 72;

            var curve = new[] { p(0, 0), p(holeI, holeDepth), p(holeO, holeDepth), p(holeO, depth - dip) }
                    .Concat(Bézier(p(holeO, depth - dip), p(xf, depth - dip), p(width - bevelWidth - xf, depth), p(width - bevelWidth, depth), bézierSteps).Skip(1))
                    .Concat(Bézier(p(width - bevelWidth, depth), p(width - bevelWidth + bevelF, depth), p(width, depth - bevelWidth + bevelF), p(width, depth - bevelWidth), bézierSteps).Skip(1))
                    .Concat(p(width, 0))
                    .Select(p => pt(p.X, p.Y, 0))
                    .ToArray();

            return CreateMesh(true, false, Enumerable.Range(0, roundSteps)
                .Select(i => i * 360.0 / roundSteps)
                .Select(angle => curve
                    .Select((p, i) => distort(p, angle).Apply(pr => i < 5 ? pr.WithMeshInfo(Normal.Average, Normal.Average, Normal.Mine, Normal.Mine) : pr.WithMeshInfo(Normal.Average, Normal.Average, i == curve.Length - 1 ? Normal.Mine : Normal.Average, i == curve.Length - 1 ? Normal.Mine : Normal.Average)))
                    .ToArray())
                .ToArray());
        }

        private static Pt distort(Pt p, double angle)
        {
            var x = cos(angle);
            var y = sin(angle);
            var f = Math.Pow(Math.Pow(x, 6) + Math.Pow(y, 6), 1.0 / 6);

            return pt(p.X * x / f, p.Y, p.X * y / f);
        }
    }
}
