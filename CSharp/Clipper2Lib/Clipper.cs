﻿/*******************************************************************************
* Author    :  Angus Johnson                                                   *
* Version   :  10.0 (beta) - also known as Clipper2                            *
* Date      :  5 June 2022                                                     *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2022                                         *
* Purpose   :  This module contains simple functions that will likely cover    *
*              most polygon boolean and offsetting needs, while also avoiding  *
*              the inherent complexities of the other modules.                 *
* Thanks    :  Special thanks to Thong Nguyen, Guus Kuiper, Phil Stopford,     *
*           :  and Daniel Gosnell for their invaluable assistance with C#.     *
* License   :  http://www.boost.org/LICENSE_1_0.txt                            *
*******************************************************************************/

#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Clipper2Lib
{

  //PRE-COMPILER CONDITIONAL ...
  //USINGZ: For user defined Z-coordinates. See Clipper.SetZ

  //using Path64 = List<Point64>;
  //using Paths64 = List<List<Point64>>;
  //using PathD = List<PointD>;
  //using PathsD = List<List<PointD>>;

  public static class ClipperFunc
  {

    public static Rect64 MaxInvalidRect64 = new Rect64(
      long.MaxValue, long.MaxValue, long.MinValue, long.MinValue);

   public static RectD MaxInvalidRectD = new RectD(
     double.MaxValue, -double.MaxValue, -double.MaxValue, -double.MaxValue);

    public static Paths64 Intersect(Paths64 subject, Paths64 clip, FillRule fillRule)
    {
      return BooleanOp(ClipType.Intersection, fillRule, subject, clip);
    }

    public static PathsD Intersect(PathsD subject, PathsD clip, FillRule fillRule)
    {
      return BooleanOp(ClipType.Intersection, fillRule, subject, clip);
    }

    public static Paths64 Union(Paths64 subject, FillRule fillRule)
    {
      return BooleanOp(ClipType.Union, fillRule, subject, null);
    }

    public static Paths64 Union(Paths64 subject, Paths64 clip, FillRule fillRule)
    {
      return BooleanOp(ClipType.Union, fillRule, subject, clip);
    }

    public static PathsD Union(PathsD subject, FillRule fillRule)
    {
      return BooleanOp(ClipType.Union, fillRule, subject, null);
    }

    public static PathsD Union(PathsD subject, PathsD clip, FillRule fillRule)
    {
      return BooleanOp(ClipType.Union, fillRule, subject, clip);
    }

    public static Paths64 Difference(Paths64 subject, Paths64 clip, FillRule fillRule)
    {
      return BooleanOp(ClipType.Difference, fillRule, subject, clip);
    }

    public static PathsD Difference(PathsD subject, PathsD clip, FillRule fillRule)
    {
      return BooleanOp(ClipType.Difference, fillRule, subject, clip);
    }

    public static Paths64 Xor(Paths64 subject, Paths64 clip, FillRule fillRule)
    {
      return BooleanOp(ClipType.Xor, fillRule, subject, clip);
    }

    public static PathsD Xor(PathsD subject, PathsD clip, FillRule fillRule)
    {
      return BooleanOp(ClipType.Xor, fillRule, subject, clip);
    }

    public static Paths64 BooleanOp(ClipType clipType, FillRule fillRule, 
      Paths64? subject, Paths64? clip)
    {
      Paths64 solution = new Paths64();
      if (subject == null) return solution;
      Clipper c = new Clipper();
      c.AddPaths(subject, PathType.Subject);
      if (clip != null)
        c.AddPaths(clip, PathType.Clip);
      c.Execute(clipType, fillRule, solution);
      return solution;
    }

    public static PathsD BooleanOp(ClipType clipType, FillRule fillRule,
        PathsD subject, PathsD? clip, int roundingDecimalPrecision = 3)
    {
      PathsD solution = new PathsD();
      ClipperD c = new ClipperD(roundingDecimalPrecision);
      c.AddSubject(subject);
      if (clip != null)
        c.AddClip(clip);
      c.Execute(clipType, fillRule, solution);
      return solution;
    }

    public static Paths64 InflatePaths(Paths64 paths, double delta, JoinType joinType, 
      EndType endType, double miterLimit = 2.0)
    {
      ClipperOffset co = new ClipperOffset(miterLimit);
      co.AddPaths(paths, joinType, endType);
      return co.Execute(delta);
    }

    public static PathsD InflatePaths(PathsD paths, double delta, JoinType joinType, 
      EndType endType, double miterLimit = 2.0, int roundingDecimalPrecision = 3)
    {
      if (roundingDecimalPrecision < -8 || roundingDecimalPrecision > 8)
        throw new ClipperLibException("Error - RoundingDecimalPrecision exceeds the allowed range.");
      double _scale = Math.Pow(10, roundingDecimalPrecision);
      double  _invScale = 1 / _scale;

      ClipperOffset co = new ClipperOffset(miterLimit);
      co.AddPaths(ScalePaths64(paths, _scale), joinType, endType);
      Paths64 tmp = co.Execute(delta * _scale);
      return ScalePathsD(tmp, _invScale);
    }

    public static double Area(Path64 path)
    {
      double a = 0.0;
      int cnt = path.Count;
      for (int i = 0, j = cnt - 1; i < cnt; j = i++)
        a += (double) (path[j].Y + path[i].Y) * (path[j].X - path[i].X);
      return Math.Abs(a * 0.5);
    }

    public static double Area(Paths64 paths)
    {
      double a = 0.0;
      int cnt = paths.Count;
      for (int i = 0; i < cnt; i++)
        a += paths[i].isPoly ? Area(paths[i]) : -Area(paths[i]);
      return a;
    }

    public static double Area(PathD path)
    {
      double a = 0.0;
      int cnt = path.Count;
      for (int i = 0, j = cnt - 1; i < cnt; j = i++)
        a += (path[j].y + path[i].y) * (path[j].x - path[i].x);
      return Math.Abs(a * 0.5);
    }

    public static double Area(PathsD paths)
    {
      if (paths == null)
      {
        return 0;
      }
      double a = 0.0;
      foreach (PathD path in paths)
        a += path.isPoly ? Area(path) : -Area(path);
      return a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsClockwise(Path64 poly)
    {
      return Area(poly) >= 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsClockwise(PathD poly)
    {
      return Area(poly) >= 0;
    }

    public static Path64 OffsetPath(Path64 path, long dx, long dy)
    {
      Path64 result = new Path64(path.Count, path.isPoly);
      foreach (Point64 pt in path)
        result.Add(new Point64(pt.X + dx, pt.Y + dy));
      return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PointD ScalePoint(Point64 pt, double scale) 
    {
      PointD result = new PointD()
      {
        x = pt.X * scale,
        y = pt.Y * scale,
      };
      return result;
    }

    public static Path64 ScalePath(Path64 path, double scale)
    {
      if (scale == 1) return path;
      Path64 result = new Path64(path.Count, path.isPoly);
      foreach (Point64 pt in path)
        result.Add(new Point64(pt.X * scale, pt.Y * scale));
      return result;
    }

    public static Paths64 ScalePaths(Paths64 paths, double scale)
    {
      if (scale == 1) return paths;
      Paths64 result = new Paths64(paths.Count);
      foreach (Path64 path in paths)
        result.Add(ScalePath(path, scale));
      return result;
    }

    public static PathD ScalePath(PathD path, double scale)
    {
      if (scale == 1) return path;
      PathD result = new PathD(path.Count, path.isPoly);
      foreach (PointD pt in path)
        result.Add(new PointD(pt, scale));
      return result;
    }

    public static PathsD ScalePaths(PathsD paths, double scale)
    {
      if (scale == 1) return paths;
      PathsD result = new PathsD(paths.Count);
      foreach (PathD path in paths)
        result.Add(ScalePath(path, scale));
      return result;
    }

    //Unlike ScalePath, both ScalePath64 & ScalePathD also involve type conversion
    public static Path64 ScalePath64(PathD path, double scale)
    {
      int cnt = path.Count;
      Path64 res = new Path64(cnt, path.isPoly);
      foreach (PointD pt in path)
        res.Add(new Point64(pt, scale));
      return res;
    }

    public static Paths64 ScalePaths64(PathsD paths, double scale)
    {
      int cnt = paths.Count;
      Paths64 res = new Paths64(cnt);
      foreach (PathD path in paths)
        res.Add(ScalePath64(path, scale));
      return res;
    }

    public static PathD ScalePathD(Path64 path, double scale)
    {
      int cnt = path.Count;
      PathD res = new PathD(cnt, path.isPoly);
      foreach (Point64 pt in path)
        res.Add(new PointD(pt, scale));
      return res;
    }

    public static PathsD ScalePathsD(Paths64 paths, double scale)
    {
      int cnt = paths.Count;
      PathsD res = new PathsD(cnt);
      foreach (Path64 path in paths)
        res.Add(ScalePathD(path, scale));
      return res;
    }

    //The static functions Path64 and PathD convert path types without scaling
    public static Path64 Path64(PathD path)
    {
      Path64 result = new Path64(path.Count, path.isPoly);
      foreach (PointD pt in path)
        result.Add(new Point64(pt));
      return result;
    }

    public static Paths64 Paths64(PathsD paths)
    {
      Paths64 result = new Paths64(paths.Count);
      foreach (PathD path in paths)
        result.Add(Path64(path));
      return result;
    }

    public static PathsD PathsD(Paths64 paths)
    {
      PathsD result = new PathsD(paths.Count);
      foreach (Path64 path in paths)
        result.Add(PathD(path));
      return result;
    }

    public static PathD PathD(Path64 path)
    {
      PathD result = new PathD(path.Count, path.isPoly);
      foreach (Point64 pt in path)
        result.Add(new PointD(pt));
      return result;
    }

    public static Paths64 OffsetPaths(Paths64 paths, long dx, long dy)
    {
      Paths64 result = new Paths64(paths.Count);
      foreach (Path64 path in paths)
        result.Add(OffsetPath(path, dx, dy));
      return result;
    }

    public static PathD OffsetPath(PathD path, long dx, long dy)
    {
      PathD result = new PathD(path.Count, path.isPoly);
      foreach (PointD pt in path)
        result.Add(new PointD(pt.x + dx, pt.y + dy));
      return result;
    }

    public static PathsD OffsetPaths(PathsD paths, long dx, long dy)
    {
      PathsD result = new PathsD(paths.Count);
      foreach (PathD path in paths)
        result.Add(OffsetPath(path, dx, dy));
      return result;
    }

    public static Path64 ReversePath(Path64 path)
    {
      Path64 result = new Path64(path);
      result.Reverse();
      return result;
    }

    public static PathD ReversePath(PathD path)
    {
      PathD result = new PathD(path);
      result.Reverse();
      return result;
    }

    public static Paths64 ReversePaths(Paths64 paths)
    {
      Paths64 result = new Paths64(paths.Count);
      foreach (Path64 t in paths)
        result.Add(ReversePath(t));

      return result;
    }

    public static PathsD ReversePaths(PathsD paths)
    {
      PathsD result = new PathsD(paths.Count);
      foreach (PathD path in paths)
        result.Add(ReversePath(path));
      return result;
    }

    public static Rect64 GetBounds(Paths64 paths)
    {
      Rect64 result = MaxInvalidRect64;
      foreach (Path64 path in paths)
        foreach (Point64 pt in path)
        {
          if (pt.X < result.left) result.left = pt.X;
          if (pt.X > result.right) result.right = pt.X;
          if (pt.Y < result.top) result.top = pt.Y;
          if (pt.Y > result.bottom) result.bottom = pt.Y;
        }
      return result.IsEmpty() ? new Rect64() : result;
    }

    public static RectD GetBounds(PathsD paths)
    {
      RectD result = MaxInvalidRectD;
      foreach (PathD path in paths)
        foreach (PointD pt in path)
        {
          if (pt.x < result.left) result.left = pt.x;
          if (pt.x > result.right) result.right = pt.x;
          if (pt.y < result.top) result.top = pt.y;
          if (pt.y > result.bottom) result.bottom = pt.y;
        }
      return result.IsEmpty() ? new RectD() : result;
    }

    public static Path64 MakePath(int[] arr)
    {
      int len = arr.Length / 2;
      Path64 p = new Path64(len, true);
      for (int i = 0; i < len; i++)
        p.Add(new Point64(arr[i * 2], arr[i * 2 + 1]));
      return p;
    }

    public static Path64 MakePath(long[] arr)
    {
      int len = arr.Length / 2;
      Path64 p = new Path64(len, true);
      for (int i = 0; i < len; i++)
        p.Add(new Point64(arr[i * 2], arr[i * 2 + 1]));
      return p;
    }

    public static PathD MakePath(double[] arr)
    {
      int len = arr.Length / 2;
      PathD p = new PathD(len, true);
      for (int i = 0; i < len; i++)
        p.Add(new PointD(arr[i * 2], arr[i * 2 + 1]));
      return p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Sqr(double value)
    {
      return value * value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool PointsNearEqual(PointD pt1, PointD pt2, double distanceSqrd)
    {
      return Sqr(pt1.x - pt2.x) + Sqr(pt1.y - pt2.y) < distanceSqrd;
    }

    public static PathD StripNearDuplicates(PathD path,
        double minEdgeLenSqrd, bool isClosedPath)
    {
      int cnt = path.Count;
      PathD result = new PathD(cnt, path.isPoly);
      if (cnt == 0) return result;
      PointD lastPt = path[0];
      result.Add(lastPt);
      for (int i = 1; i < cnt; i++)
        if (!PointsNearEqual(lastPt, path[i], minEdgeLenSqrd))
        {
          lastPt = path[i];
          result.Add(lastPt);
        }

      if (isClosedPath && PointsNearEqual(lastPt, result[0], minEdgeLenSqrd))
      {
        result.RemoveAt(result.Count - 1);
      }

      return result;
    }

    public static Path64 StripDuplicates(Path64 path, bool isClosedPath)
    {
      int cnt = path.Count;
      Path64 result = new Path64(cnt, path.isPoly);
      if (cnt == 0) return result;
      Point64 lastPt = path[0];
      result.Add(lastPt);
      for (int i = 1; i < cnt; i++)
        if (lastPt != path[i])
        {
          lastPt = path[i];
          result.Add(lastPt);
        }
      if (isClosedPath && lastPt == result[0])
        result.RemoveAt(result.Count - 1);
      return result;
    }

    private static void AddPolyNodeToPaths(PolyPath polyPath, Paths64 paths)
    {
      if (polyPath.Polygon!.Count > 0)
        paths.Add(polyPath.Polygon);
      for (int i = 0; i < polyPath.ChildCount; i++)
        AddPolyNodeToPaths((PolyPath) polyPath._childs[i], paths);
    }

    public static Paths64 PolyTreeToPaths(PolyTree polyTree)
    {
      Paths64 result = new Paths64();
      for (int i = 0; i < polyTree.ChildCount; i++)
        AddPolyNodeToPaths((PolyPath) polyTree._childs[i], result);
      return result;
    }

    public static void AddPolyNodeToPathsD(PolyPathD polyPath, PathsD paths)
    {
      if (polyPath.Polygon!.Count > 0)
        paths.Add(polyPath.Polygon);
      for (int i = 0; i < polyPath.ChildCount; i++)
        AddPolyNodeToPathsD((PolyPathD) polyPath._childs[i], paths);
    }

    public static PathsD PolyTreeToPathsD(PolyTreeD polyTree)
    {
      PathsD result = new PathsD();
      for (int i = 0; i < polyTree.ChildCount; i++)
        AddPolyNodeToPathsD((PolyPathD) polyTree._childs[i], result);
      return result;
    }

    public static double PerpendicDistFromLineSqrd(PointD pt, PointD line1, PointD line2)
    {
      double a = pt.x - line1.x;
      double b = pt.y - line1.y;
      double c = line2.x - line1.x;
      double d = line2.y - line1.y;
      if (c == 0 && d == 0) return 0;
      return Sqr(a * d - c * b) / (c * c + d * d);
    }

    public static double PerpendicDistFromLineSqrd(Point64 pt, Point64 line1, Point64 line2)
    {
      double a = (double) pt.X - line1.X;
      double b = (double) pt.Y - line1.Y;
      double c = (double) line2.X - line1.X;
      double d = (double) line2.Y - line1.Y;
      if (c == 0 && d == 0) return 0;
      return Sqr(a * d - c * b) / (c * c + d * d);
    }

    public static void RDP(Path64 path, int begin, int end, double epsSqrd, List<int> flags)
    {
      int idx = 0;
      double max_d = 0;
      while (end > begin && path[begin] == path[end]) flags[end--] = 0;
      for (int i = begin + 1; i < end; ++i)
      {
        //PerpendicDistFromLineSqrd - avoids expensive Sqrt()
        double d = PerpendicDistFromLineSqrd(path[i], path[begin], path[end]);
        if (d <= max_d) continue;
        max_d = d;
        idx = i;
      }
      if (max_d <= epsSqrd) return;
      flags[idx] = 1;
      if (idx > begin + 1) RDP(path, begin, idx, epsSqrd, flags);
      if (idx < end - 1) RDP(path, idx, end, epsSqrd, flags);
    }

    public static Path64 RamerDouglasPeucker(Path64 path, double epsilon)
    {
      int len = path.Count;
      if (len < 5) return path;
      List<int> flags = new List<int>(new int[len]);
      flags[0] = 1;
      flags[len - 1] = 1;
      RDP(path, 0, len - 1, Sqr(epsilon), flags);
      Path64 result = new Path64(len, path.isPoly);
      for (int i = 0; i < len; ++i)
        if (flags[i] == 1)
          result.Add(path[i]);
      return result;
    }

    public static Paths64 RamerDouglasPeucker(Paths64 paths, double epsilon)
    {
      Paths64 result = new Paths64(paths.Count);
      foreach (Path64 path in paths)
        result.Add(RamerDouglasPeucker(path, epsilon));
      return result;
    }

    public static void RDP(PathD path, int begin, int end, double epsSqrd, List<int> flags)
    {
      int idx = 0;
      double max_d = 0;
      while (end > begin && path[begin] == path[end]) flags[end--] = 0;
      for (int i = begin + 1; i < end; ++i)
      {
        //PerpendicDistFromLineSqrd - avoids expensive Sqrt()
        double d = PerpendicDistFromLineSqrd(path[i], path[begin], path[end]);
        if (d <= max_d) continue;
        max_d = d;
        idx = i;
      }
      if (max_d <= epsSqrd) return;
      flags[idx] = 1;
      if (idx > begin + 1) RDP(path, begin, idx, epsSqrd, flags);
      if (idx < end - 1) RDP(path, idx, end, epsSqrd, flags);
    }

    public static PathD RamerDouglasPeucker(PathD path, double epsilon)
    {
      int len = path.Count;
      if (len < 5) return path;
      List<int> flags = new List<int>(new int[len]);
      flags[0] = 1;
      flags[len - 1] = 1;
      RDP(path, 0, len - 1, Sqr(epsilon), flags);
      PathD result = new PathD(len, path.isPoly);
      for (int i = 0; i < len; ++i)
        if (flags[i] == 1)
          result.Add(path[i]);
      return result;
    }

    public static PathsD RamerDouglasPeucker(PathsD paths, double epsilon)
    {
      PathsD result = new PathsD(paths.Count);
      foreach (PathD path in paths)
        result.Add(RamerDouglasPeucker(path, epsilon));
      return result;
    }
  }
} //namespace