using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeaOfUncertainty.Prototype
{
    [Serializable] public sealed class CoastlinePointData { public double longitude; public double latitude; }
    [Serializable] public sealed class CoastlinePolygonData { public List<CoastlinePointData> points = new List<CoastlinePointData>(); }
    [Serializable] public sealed class CoastlineMetadataData { public string title; public string source; public string sourceFile; public string license; }
    [Serializable]
    public sealed class CoastlineData
    {
        public CoastlineMetadataData metadata;
        public double west;
        public double east;
        public double south;
        public double north;
        public double projectionWest;
        public double projectionEast;
        public double projectionSouth;
        public double projectionNorth;
        public List<CoastlinePolygonData> polygons = new List<CoastlinePolygonData>();
    }

    public static class CoastlinePolygonMesh
    {
        public static Mesh Create(CoastlineData data, Rect worldBounds, float height, out List<Vector3[]> shorelines)
        {
            shorelines = new List<Vector3[]>();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            if (data?.polygons == null) return null;
            double projectionWest = data.projectionEast > data.projectionWest ? data.projectionWest : data.west;
            double projectionEast = data.projectionEast > data.projectionWest ? data.projectionEast : data.east;
            double projectionSouth = data.projectionNorth > data.projectionSouth ? data.projectionSouth : data.south;
            double projectionNorth = data.projectionNorth > data.projectionSouth ? data.projectionNorth : data.north;

            foreach (CoastlinePolygonData polygon in data.polygons)
            {
                var points = new List<Vector2>();
                foreach (CoastlinePointData point in polygon.points)
                {
                    float normalizedX = (float)((point.longitude - projectionWest) / (projectionEast - projectionWest));
                    float normalizedZ = (float)((point.latitude - projectionSouth) / (projectionNorth - projectionSouth));
                    float x = Mathf.LerpUnclamped(worldBounds.xMin, worldBounds.xMax, normalizedX);
                    float z = Mathf.LerpUnclamped(worldBounds.yMin, worldBounds.yMax, normalizedZ);
                    Vector2 projected = new Vector2(x, z);
                    if (points.Count == 0 || Vector2.SqrMagnitude(points[points.Count - 1] - projected) > .000001f) points.Add(projected);
                }
                if (points.Count > 2 && Vector2.SqrMagnitude(points[0] - points[points.Count - 1]) < .000001f) points.RemoveAt(points.Count - 1);
                if (points.Count < 3) continue;
                if (SignedArea(points) < 0f) points.Reverse();

                List<int> localTriangles = Triangulate(points);
                if (localTriangles.Count < 3) continue;
                int offset = vertices.Count;
                foreach (Vector2 point in points)
                {
                    vertices.Add(new Vector3(point.x, height, point.y));
                    uvs.Add(new Vector2((point.x - worldBounds.xMin) / worldBounds.width, (point.y - worldBounds.yMin) / worldBounds.height));
                }
                foreach (int index in localTriangles) triangles.Add(offset + index);
                var shoreline = new Vector3[points.Count];
                for (int i = 0; i < points.Count; i++) shoreline[i] = new Vector3(points[i].x, height + .018f, points[i].y);
                shorelines.Add(shoreline);
            }

            if (vertices.Count < 3) return null;
            var mesh = new Mesh { name = "Natural Earth Coastline Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<int> Triangulate(List<Vector2> points)
        {
            var result = new List<int>();
            var remaining = new List<int>();
            for (int i = 0; i < points.Count; i++) remaining.Add(i);
            int guard = points.Count * points.Count;
            while (remaining.Count > 2 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int previous = remaining[(i + remaining.Count - 1) % remaining.Count];
                    int current = remaining[i];
                    int next = remaining[(i + 1) % remaining.Count];
                    if (Cross(points[previous], points[current], points[next]) <= .000001f) continue;
                    bool contains = false;
                    for (int j = 0; j < remaining.Count; j++)
                    {
                        int candidate = remaining[j];
                        if (candidate == previous || candidate == current || candidate == next) continue;
                        if (InsideTriangle(points[candidate], points[previous], points[current], points[next])) { contains = true; break; }
                    }
                    if (contains) continue;
                    // Clockwise in Unity's X/Z plane gives an upward-facing normal.
                    result.Add(previous); result.Add(next); result.Add(current);
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            return result;
        }

        private static float SignedArea(List<Vector2> points)
        {
            float area = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % points.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return area * .5f;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        private static bool InsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float ab = Cross(a, b, point), bc = Cross(b, c, point), ca = Cross(c, a, point);
            return ab >= -.000001f && bc >= -.000001f && ca >= -.000001f;
        }
    }
}
