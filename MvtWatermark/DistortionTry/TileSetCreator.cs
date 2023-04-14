using Microsoft.Data.Sqlite;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System.IO.Compression;

namespace DistortionTry;
public class TileSetCreator
{
    public static VectorTileTree GetVectorTileTree(IEnumerable<CoordinateSet> parameterSetsStp, IEnumerable<CoordinateSet> parameterSetsTegola)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
        var connectionString = $"Data Source = {path}";
        using var sqliteConnection = new SqliteConnection(connectionString);
        sqliteConnection.Open();

        Console.WriteLine($"Connection string = {connectionString}");

        var vectorTileTree = new VectorTileTree();
        var areAnyCorrectTilesHere = false;

        foreach (var parameterSet in parameterSetsStp)
        {
            var vt = GetSingleVectorTileFromDBStp(sqliteConnection, parameterSet.Zoom, parameterSet.X, parameterSet.Y);
            if (vt != null)
            {
                areAnyCorrectTilesHere = true;
                vectorTileTree[vt.TileId] = vt;
            }
        }

        foreach (var parameterSet in parameterSetsTegola)
        {
            var vt = GetsingleVectorTileFromTegola(parameterSet.Zoom, parameterSet.X, parameterSet.Y);
            if (vt != null)
            {
                areAnyCorrectTilesHere = true;
                vectorTileTree[vt.TileId] = vt;
            }
        }

        if (!areAnyCorrectTilesHere)
            throw new ArgumentException("No correct tiles have been found");

        return vectorTileTree;
    }

    private static VectorTile? GetSingleVectorTileFromDBStp(SqliteConnection? sqliteConnection, int zoom, int x, int y)
    {
        using var command = new SqliteCommand(@"SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", sqliteConnection);
        command.Parameters.AddWithValue("$z", zoom);
        command.Parameters.AddWithValue("$x", x);
        command.Parameters.AddWithValue("$y", (1 << zoom) - y - 1);
        var obj = command.ExecuteScalar();

        if (obj == null)
        {
            Console.WriteLine("obj = null");
            return null;
        }
        else
        {
            Console.WriteLine("Successfully got the tile from STP");
        }

        var bytes = (byte[])obj!;

        using var memoryStream = new MemoryStream(bytes);
        var reader = new MapboxTileReader();

        memoryStream.Seek(0, SeekOrigin.Begin);
        using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
        var vt = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, zoom));

        return vt;
    }

    private static VectorTile? GetsingleVectorTileFromTegola(int zoom, int x, int y)
    {
        var reader = new MapboxTileReader();

        using var sharedClient = new HttpClient()
        {
            BaseAddress = new Uri($"https://tegola-osm-demo.go-spatial.org/v1/maps/osm/{zoom}/{x}/{y}"),
        };

        sharedClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 QGIS/32210");
        sharedClient.DefaultRequestHeaders.Add("accept-encoding", "gzip");

        var response = sharedClient.GetByteArrayAsync("").Result;
        using var memoryStream = new MemoryStream(response);

        memoryStream.Seek(0, SeekOrigin.Begin);
        using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
        var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, zoom));

        if (tile is null)
            Console.WriteLine("obj = null");
        else
            Console.WriteLine("Successfully got the tile from Tegola");

        return tile;
    }

    public static VectorTileTree CreateRandomVectorTileTreeOneLineString(IEnumerable<CoordinateSet> parameterSets, int dotsNumber)
    {
        var vectorTileTree = new VectorTileTree();
        foreach (var parameterSet in parameterSets)
        {
            ulong tileId;
            VectorTile? vt = CreateRandomVectorTileOneLineString(parameterSet.X, parameterSet.Y, parameterSet.Zoom, dotsNumber, out tileId);
            vectorTileTree[tileId] = vt!;
        }

        return vectorTileTree;
    }

    private static VectorTile? CreateRandomVectorTileOneLineString(int x, int y, int zoom, int dotsNumber, out ulong tile_id)
    {
        tile_id = MvtWatermark.NoDistortionWatermark.Auxiliary.NtsArtefacts.Tile.CalculateTileId(zoom, x, y);
        var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, zoom);
        var vt = new VectorTile { TileId = tileDefinition.Id };

        var rand = new Random(1);
        var lyr = new Layer { Name = $"layer{1}" };

        var feature = CreateRandomFeature(dotsNumber, 1,
                Convert.ToBoolean(rand.Next(0, 2)), Convert.ToBoolean(rand.Next(0, 2)));
        lyr.Features.Add(feature);

        vt.Layers.Add(lyr);

        Console.WriteLine("Возвращаем векторный тайл..."); // отладка
        return vt;
    }

    public static VectorTileTree CreateRandomVectorTileTree(IEnumerable<CoordinateSet> parameterSets)
    {
        var vectorTileTree = new VectorTileTree();
        foreach (var parameterSet in parameterSets)
        {
            ulong tileId;
            VectorTile? vt = CreateRandomVectorTile(parameterSet.X, parameterSet.Y, parameterSet.Zoom, out tileId);
            vectorTileTree[tileId] = vt!;
        }

        return vectorTileTree;
    }

    private static VectorTile? CreateRandomVectorTile(int x, int y, int zoom, out ulong tile_id)
    {
        tile_id = MvtWatermark.NoDistortionWatermark.Auxiliary.NtsArtefacts.Tile.CalculateTileId(zoom, x, y);
        var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, zoom);
        var vt = new VectorTile { TileId = tileDefinition.Id };
        Layer lyr = CreateRandomLayer(1); 

        vt.Layers.Add(lyr);

        Console.WriteLine("Возвращаем векторный тайл..."); // отладка
        return vt;
    }

    private static Layer CreateRandomLayer(int layerNumber)
    {
        var rand = new Random(layerNumber);
        var lyr = new Layer { Name = $"layer{layerNumber}" };

        for (var i = 1; i < 20; i++)
        {
            var feature = CreateRandomFeature(i * i, i, 
                Convert.ToBoolean(rand.Next(0, 2)), Convert.ToBoolean(rand.Next(0, 2)));
            lyr.Features.Add(feature);
        }

        return lyr;
    }

    private static Feature CreateRandomFeature(int numOfDots, int id, bool isPolygon = false, bool isMultiLineString = false)
    {
        var rand = new Random(numOfDots);
        string geometryType;
        Geometry geometry;

        if (numOfDots == 1)
        {
            geometry = CreateRandomPoint(rand);
            geometryType = "Point";
        }
        else if (isPolygon)
        {
            geometry = CreateRandomPolygon(rand, numOfDots);
            geometryType = "Polygon";
        }
        else if (isMultiLineString)
        {
            geometry = CreateRandomMultiLineString(rand, numOfDots, rand.Next(1, 10));
            geometryType = "MultiLineString";
        }
        else 
        {
            geometry = CreateRandomLineString(rand, numOfDots);
            geometryType = "LineString";
            //Console.WriteLine("\nЛайнстринг: "); // отладка
            //Console.WriteLine(geom.ToString()); // отладка
            //Console.WriteLine("\n"); // отладка
        }

        return new Feature
        {
            Geometry = geometry,
            Attributes = new AttributesTable(new Dictionary<string, object>()
            {
                ["LN_ID"] = id,
                ["type"] = geometryType,
            })
        };
    }

    private static Point CreateRandomPoint(Random rand)
    {
        var xCoord = rand.Next(-179, 178) + 0.5;
        var yCoord = rand.Next(-89, 88) + 0.5;
        return new Point(new Coordinate(xCoord, yCoord));
    }

    private static LineString CreateRandomLineString(Random rand, int numOfDots)
    {
        var coordinateCollection = new List<Coordinate>();
        for (var i = 0; i < numOfDots; i++)
        {
            var xCoord = rand.Next(-179, 179) + 0.5;
            var yCoord = rand.Next(-89, 89) + 0.5;

            coordinateCollection.Add(new Coordinate(xCoord, yCoord));
        }
        var coordinateArray = coordinateCollection.ToArray();

        return new LineString(coordinateArray);
    }

    private static Polygon CreateRandomPolygon(Random rand, int numOfDots)
    {
        var coordinateCollection = new List<Coordinate>();

        var startXCoord = rand.Next(-179, 179) + 0.5;
        var startYCoord = rand.Next(-89, 89) + 0.5;
        coordinateCollection.Add(new Coordinate(startXCoord, startYCoord));

        for (var i = 1; i < numOfDots; i++)
        {
            var xCoord = rand.Next(-179, 179) + 0.5;
            var yCoord = rand.Next(-89, 89) + 0.5;
            coordinateCollection.Add(new Coordinate(xCoord, yCoord));
        }
        coordinateCollection.Add(new Coordinate(startXCoord, startYCoord));

        var coordinateArray = coordinateCollection.ToArray();
        var linearRing = new LinearRing(coordinateArray);
        var emptyLinearRing = new LinearRing[0];

        return new Polygon(linearRing, emptyLinearRing);
    }

    private static MultiLineString CreateRandomMultiLineString(Random rand, 
        int numOfDotsInSingleLineString, int lineStringNum)
    {
        var lineStringArr = new LineString[lineStringNum];
        for (var i = 0; i < lineStringNum; i++)
        {
            lineStringArr[i] = CreateRandomLineString(rand, rand.Next(2, numOfDotsInSingleLineString + 1));
        }

        return new MultiLineString(lineStringArr);
    }
}
