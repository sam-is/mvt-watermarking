
using System;
using System.Runtime.CompilerServices;
using NetTopologySuite.Geometries;
using NetTopologySuite.GeometriesGraph;
using NetTopologySuite.IO.VectorTiles.Tiles.WebMercator;

[assembly: InternalsVisibleTo("NetTopologySuite.IO.VectorTiles.Tests")]
namespace NetTopologySuite.IO.VectorTiles.Mapbox
{
    /// <summary>
    /// A transformation utility from WGS84 coordinates to a local tile coordinate system in pixel
    /// </summary>
    public struct TileGeometryTransform // ИЗМЕНИЛ ДОСТУП
    {
        private Tiles.Tile _tile;
        private uint _extent;
        private long _top;
        private long _left;
        
        /// <summary>
        /// Initializes this transformation utility
        /// </summary>
        /// <param name="tile">The tile's bounds</param>
        /// <param name="extent">The tile's extent in pixel. Tiles are always square.</param>
        public TileGeometryTransform(Tiles.Tile tile, uint extent) : this()
        {
            _tile = tile;
            _extent = extent;
            
            var meters = WebMercatorHandler.LatLonToMeters(_tile.Top, _tile.Left);
            var pixels = WebMercatorHandler.MetersToPixels(meters, tile.Zoom, (int) extent);
            _top = (long)pixels.y;
            _left = (long)pixels.x;
        }

        /// <summary>
        /// Transforms the coordinate at <paramref name="index"/> of <paramref name="sequence"/> to the tile coordinate system.
        /// The return value is the position relative to the local point at (<paramref name="currentX"/>, <paramref name="currentY"/>).
        /// </summary>
        /// <param name="sequence">The input sequence</param>
        /// <param name="index">The index of the coordinate to transform</param>
        /// <param name="currentX">The current horizontal component of the cursor location. This value is updated.</param>
        /// <param name="currentY">The current vertical component of the cursor location. This value is updated.</param>
        /// <returns>The position relative to the local point at (<paramref name="currentX"/>, <paramref name="currentY"/>).</returns>
        public (int x, int y) Transform(CoordinateSequence sequence, int index, ref int currentX, ref int currentY)
        {
            var lon = sequence.GetOrdinate(index, Ordinate.X);
            var lat = sequence.GetOrdinate(index, Ordinate.Y);

            var meters = WebMercatorHandler.LatLonToMeters(lat, lon);
            var pixels = WebMercatorHandler.MetersToPixels(meters, _tile.Zoom, (int) _extent);

            int localX = (int) (pixels.x - _left);
            int localY = (int) (_top - pixels.y);

            int dx = localX - currentX;
            int dy = localY - currentY;


            /*
            Console.BackgroundColor = ConsoleColor.Gray; // отладка
            Console.ForegroundColor = ConsoleColor.Blue; // отладка
            Console.WriteLine($"currentX = {currentX} , currentY = {currentY}"); // отладка
            Console.BackgroundColor = ConsoleColor.Black; // отладка
            Console.ForegroundColor = ConsoleColor.White; // отладка

            Console.WriteLine($"lon (X) = {lon} , lat (Y) = {lat}"); // отладка
            Console.WriteLine($"meters = {meters}"); // отладка
            Console.WriteLine($"pixels = {pixels}"); // отладка

            if (!Double.IsNaN(pixels.y))
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen; // отладка
            }
            else
                Console.ForegroundColor = ConsoleColor.DarkRed; // отладка
            Console.WriteLine($"pixels.x = {pixels.x}, left = {_left}"); // отладка
            Console.WriteLine($"top = {_top}, pixels.y = {pixels.y}"); // отладка
            Console.ForegroundColor = ConsoleColor.White; // отладка
            Console.WriteLine($"localX = {localX} , localY = {localY}"); // отладка
            Console.WriteLine($"dx = {dx} , dy = {dy}"); // отладка
            */

            
            currentX = localX;
            currentY = localY;

            return (dx, dy);
        }

        public (double longitude, double latitude) TransformInverse(int x, int y)
        {
            var globalX = _left + x;
            var globalY = _top - y;

            var meters = WebMercatorHandler.PixelsToMeters((globalX, globalY), _tile.Zoom, (int) _extent);
            var coordinates = WebMercatorHandler.MetersToLatLon(meters);
            return coordinates;
        }
    }
}
