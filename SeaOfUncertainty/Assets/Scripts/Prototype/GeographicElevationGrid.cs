using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace SeaOfUncertainty.Prototype
{
    public sealed class GeographicElevationGrid
    {
        private const string Magic = "SOUELEV1";
        private readonly short[] elevations;

        public int Width { get; }
        public int Height { get; }
        public double West { get; }
        public double East { get; }
        public double South { get; }
        public double North { get; }
        public int SampleCount => elevations.Length;

        private GeographicElevationGrid(int width, int height, double west, double east, double south, double north, short[] elevations)
        {
            Width = width;
            Height = height;
            West = west;
            East = east;
            South = south;
            North = north;
            this.elevations = elevations;
        }

        public short ElevationMetres(int column, int row)
            => elevations[Mathf.Clamp(row, 0, Height - 1) * Width + Mathf.Clamp(column, 0, Width - 1)];

        public float SampleMetres(double longitude, double latitude)
        {
            double columnPosition = (longitude - West) / (East - West) * (Width - 1);
            double rowPosition = (latitude - South) / (North - South) * (Height - 1);
            int column = Mathf.Clamp((int)Math.Floor(columnPosition), 0, Width - 2);
            int row = Mathf.Clamp((int)Math.Floor(rowPosition), 0, Height - 2);
            float columnBlend = Mathf.Clamp01((float)(columnPosition - column));
            float rowBlend = Mathf.Clamp01((float)(rowPosition - row));
            float south = Mathf.Lerp(ElevationMetres(column, row), ElevationMetres(column + 1, row), columnBlend);
            float north = Mathf.Lerp(ElevationMetres(column, row + 1), ElevationMetres(column + 1, row + 1), columnBlend);
            return Mathf.Lerp(south, north, rowBlend);
        }

        public bool Contains(double longitude, double latitude)
            => longitude >= West && longitude <= East && latitude >= South && latitude <= North;

        public double Longitude(int column)
            => Width <= 1 ? West : West + (East - West) * column / (Width - 1);

        public double Latitude(int row)
            => Height <= 1 ? South : South + (North - South) * row / (Height - 1);

        public static bool TryLoad(TextAsset asset, out GeographicElevationGrid grid, out string error)
        {
            grid = null;
            error = null;
            if (asset == null) { error = "Elevation resource is missing."; return false; }
            try
            {
                using (var stream = new MemoryStream(asset.bytes, false))
                using (var reader = new BinaryReader(stream, Encoding.ASCII, false))
                {
                    if (Encoding.ASCII.GetString(reader.ReadBytes(Magic.Length)) != Magic) { error = "Elevation resource has an invalid signature."; return false; }
                    int width = reader.ReadInt32();
                    int height = reader.ReadInt32();
                    double west = reader.ReadDouble();
                    double east = reader.ReadDouble();
                    double south = reader.ReadDouble();
                    double north = reader.ReadDouble();
                    if (width < 2 || height < 2 || width > 4096 || height > 4096 || east <= west || north <= south)
                    {
                        error = "Elevation resource header is invalid.";
                        return false;
                    }
                    int count = checked(width * height);
                    if (stream.Length - stream.Position != count * sizeof(short)) { error = "Elevation resource sample count does not match its dimensions."; return false; }
                    var samples = new short[count];
                    for (int index = 0; index < samples.Length; index++) samples[index] = reader.ReadInt16();
                    grid = new GeographicElevationGrid(width, height, west, east, south, north, samples);
                    return true;
                }
            }
            catch (Exception exception)
            {
                error = "Elevation resource could not be read: " + exception.Message;
                return false;
            }
        }
    }
}
