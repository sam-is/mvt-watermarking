using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.Features;
using NetTopologySuite.IO.VectorTiles;
using MvtWatermark.NoDistortionWatermark;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles.Tiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System.IO;
using MvtWatermark.DebugClasses;
//using static NetTopologySuite.IO.VectorTiles.Mapbox.Tile;

namespace MvtWatermark
{
    internal class TestProgram
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Работает TestProgram");

            int zoom = 0; int x = 0; int y = 0;
            //WithoutWatermark(zoom, x, y);

            //var options = new NoDistortionWatermarkOptions(2, 3, 54321, 3, NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt);
            var options = new NoDistortionWatermarkOptions(2, 3, 54321, 3, NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt, true);

            //var options = new NoDistortionWatermarkOptions(2, 3, 54321, 3, NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands);

            int key = 123;
            var boolArr = new bool[] { true, false, true };
            //EmbedAndWriteToFile2(zoom, x, y, options, boolArr, key);
            var resulttree = EmbedingWatermark(zoom, x, y, options, boolArr, key);
            ExtractFromVTtree(resulttree, options, key);

            //var tile_id = NetTopologySuite.IO.VectorTiles.Tiles.Tile.CalculateTileId(10, 6, 6);
            //ReadSomething("C:\\SerializedTiles\\SerializedTrees\\10\\6\\6.mvt", tile_id);

            Console.WriteLine("TestProgram завершил работу"); // отладка
        }

        static VectorTile createVectorTile(int x, int y, int zoom, out ulong tile_id)
        {
            tile_id = NetTopologySuite.IO.VectorTiles.Tiles.Changed.Tile.CalculateTileId(zoom, x, y);

            var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, zoom);

            var vt = new VectorTile { TileId = tileDefinition.Id };

            var lyr = new Layer { Name = "layer1" };

            for (int i = 1; i < 10; i++)
            {
                var feature = createFeature(i * i, i);
                lyr.Features.Add(feature);
            }

            //Add the layer to the vector tile. 
            vt.Layers.Add(lyr);

            Console.WriteLine("Возвращаем векторный тайл..."); // отладка
            return vt;
        }

        static Feature createFeature(int numOfdots, int id, bool isPolygon=false)
        {
            var rand = new Random(numOfdots);
            int X, Y;
            if (numOfdots == 1)
            {
                X = rand.Next(-179, 179);
                Y = rand.Next(-89, 89);
                var point = new Point(new Coordinate(X, Y));
                Console.WriteLine("\nПоинт: "); // отладка
                Console.WriteLine(point); // отладка
                Console.WriteLine("\n"); // отладка
                return new Feature
                {
                    Geometry = point,
                    Attributes = new AttributesTable(new Dictionary<string, object>()
                    {
                        ["LN_ID"] = id,
                        ["type"] = "Point",
                    })
                };
            }
            var coordinateCollection = new List<Coordinate>();
            for (var i = 0; i < numOfdots; i++)
            {
                X = rand.Next(-179, 179);
                Y = rand.Next(-89, 89);

                //Console.WriteLine($"X = {X} , Y = {Y}"); // отладка

                coordinateCollection.Add(new Coordinate(X, Y));
            }
            var coordinateArray = coordinateCollection.ToArray();

            //Console.WriteLine("\nCoordinate array:"); // отладка
            //ConsoleWriter.WriteArray<Coordinate>(coordinateArray); // отладка

            var geom = new LineString(coordinateArray);

            Console.WriteLine("\nЛайнстринг: "); // отладка
            Console.WriteLine(geom.ToString()); // отладка
            Console.WriteLine("\n"); // отладка

            return new Feature
            {
                Geometry = geom,
                Attributes = new AttributesTable(new Dictionary<string, object>()
                {
                    ["LN_ID"] = id,
                    ["type"] = "Linestring",
                })
            };
        }

        static VectorTile createVectorTileWithTestFeature(out ulong tile_id)
        {
            int x, y, zoom;
            x = 0;
            y = 0;
            zoom = 10;

            tile_id = NetTopologySuite.IO.VectorTiles.Tiles.Changed.Tile.CalculateTileId(zoom, x, y);

            var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, zoom);

            var vt = new VectorTile { TileId = tileDefinition.Id };

            var lyr = new Layer { Name = "layer1" };

            var coordinateCollection = new List<Coordinate>();
            for (var i = 1; i < 41; i++)
            {
                coordinateCollection.Add(new Coordinate(i * i, i * i));
            }
            var coordinateArray = coordinateCollection.ToArray();

            Console.WriteLine("Coordinate array:"); // отладка
            ConsoleWriter.WriteArray<Coordinate>(coordinateArray); // отладка

            var myFeature = new Feature
            {
                Geometry = new LineString(coordinateArray),
                Attributes = new AttributesTable(new Dictionary<string, object>()
                {
                    ["LN_ID"] = 1,
                    ["title"] = "Linestring_one",
                })
            };
            //Add your NTS feature(s) to the layer. Loop through all your features and add them to the tile.
            lyr.Features.Add(myFeature);

            //Add the layer to the vector tile. 
            vt.Layers.Add(lyr);

            Console.WriteLine("Возвращаем векторный тайл..."); // отладка
            return vt;
        }

        /// <summary>
        /// Запись MVT по файлам и папкам без вотермарки
        /// </summary>
        /// <param name="zoom"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        static void WithoutWatermark(int zoom, int x, int y)
        {

            var vtTree = new VectorTileTree();
            ulong tile_id;
            //VectorTile vt = createVectorTileWithTestFeature(out tile_id);
            VectorTile vt = createVectorTile(x, y, zoom, out tile_id);
            vtTree[tile_id] = vt;

            vtTree.Write("C:\\SerializedTiles\\SerializedTrees\\");

            Console.WriteLine("Write завершён"); // отладка

            //ReadSomething("C:\\SerializedTiles\\SerializedTrees\\10\\6\\6.mvt", tile_id);
            ReadSomething($"C:\\SerializedTiles\\SerializedTrees\\{zoom}\\{x}\\{y}.mvt", tile_id);
        }

        /// <summary>
        /// Встраивание вотермарки и запись в файл
        /// </summary>
        /// <param name="zoom"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="options"></param>
        /// <param name="boolArr"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        static VectorTileTree EmbedingWatermark(int zoom, int x, int y, NoDistortionWatermarkOptions options, bool[] boolArr, int key = 123)
        {
            var NdWm = new NoDistortionWatermark.NoDistortionWatermark(options);

            var vtTree = new VectorTileTree();
            ulong tile_id;
            //VectorTile vt = createVectorTileWithTestFeature(out tile_id);
            //VectorTile vt = createVectorTile(0, 0, 1, out tile_id);
            VectorTile vt = createVectorTile(x, y, zoom, out tile_id);
            vtTree[tile_id] = vt;

            var message = new System.Collections.BitArray(boolArr);
            Console.WriteLine(message.ToString);

            string path = "C:\\SerializedTiles\\SerializedWithWatermark";

            var resulttree = NdWm.Embed(vtTree, key, message);
            //NdWm.EmbedAndWriteToFile(vtTree, key, message, path);

            resulttree.Write(path);

            Console.WriteLine("Встраивание завершено"); // отладка

            ReadSomething($"C:\\SerializedTiles\\SerializedWithWatermark\\{zoom}\\{x}\\{y}.mvt", tile_id);

            return resulttree;
        }

        static void EmbedAndWriteToFile2(int zoom, int x, int y, NoDistortionWatermarkOptions options, bool[] boolArr, int key = 123)
        {
            var NdWm = new NoDistortionWatermark.NoDistortionWatermark(options);

            var vtTree = new VectorTileTree();
            ulong tile_id;
            //VectorTile vt = createVectorTileWithTestFeature(out tile_id);
            //VectorTile vt = createVectorTile(0, 0, 1, out tile_id);
            VectorTile vt = createVectorTile(x, y, zoom, out tile_id);
            vtTree[tile_id] = vt;

            var message = new System.Collections.BitArray(boolArr);
            Console.WriteLine(message.ToString);

            string path = "C:\\SerializedTiles\\SerializedWithWatermark";

            //var resulttree = NdWm.Embed(vtTree, key, message);
            //NdWm.EmbedAndWriteToFile(vtTree, key, message, path);

            //resulttree.Write(path);

            Console.WriteLine("Встраивание завершено"); // отладка

            ReadSomething($"C:\\SerializedTiles\\SerializedWithWatermark\\{zoom}\\{x}\\{y}.mvt", tile_id);
        }

        /// <summary>
        /// То же самое, что и EmbedingWatermark, но без записи в файл
        /// </summary>
        /// <param name="zoom"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="options"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        static VectorTileTree EmbedToVTtree(int zoom, int x, int y, NoDistortionWatermarkOptions options, bool[] boolArr, int key = 123)
        {
            var NdWm = new NoDistortionWatermark.NoDistortionWatermark(options);

            var vtTree = new VectorTileTree();
            ulong tile_id;
            VectorTile vt = createVectorTile(x, y, zoom, out tile_id);
            vtTree[tile_id] = vt;

            var message = new System.Collections.BitArray(boolArr);
            Console.WriteLine(message.ToString);

            var resulttree = NdWm.Embed(vtTree, key, message);

            Console.WriteLine("Встраивание завершено"); // отладка

            ReadSomething($"C:\\SerializedTiles\\SerializedWithWatermark\\{zoom}\\{x}\\{y}.mvt", tile_id);

            return resulttree;
        }

        static void ExtractFromVTtree(VectorTileTree tiles, NoDistortionWatermarkOptions options, int key = 123)
        {
            var NdWm = new NoDistortionWatermark.NoDistortionWatermark(options);

            var WatermarkInt = WatermarkTransform.getIntFromBitArrayNullable(NdWm.Extract(tiles, key));
            Console.WriteLine($"\n\nИзвлечённая вотермарка: {WatermarkInt}");
        }

        static void ReadSomething(string filePath, ulong tile_id)
        {
            //Create a MapboxTileReader.
            var reader = new MapboxTileReader();

            //Define which tile you want to read. You may be able to extract the x/y/zoom info from the file path of the tile. 
            var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tile_id);

            //Open a vector tile file as a stream.
            using (var fs = new FileStream(filePath, FileMode.Open))
            {
                //Read the vector tile.
                var vt = reader.Read(fs, tileDefinition);

                //Loop through each layer.
                foreach (var l in vt.Layers)
                {
                    //Access the features of the layer and do something with them. 
                    var features = l.Features;
                    Console.WriteLine("\n");

                    foreach (var f in features)
                    {
                        Console.WriteLine("Фича: ");
                        Console.WriteLine(f.Geometry);
                        Console.WriteLine($"Валидна ли геометрия: {f.Geometry.IsValid}");
                        Console.WriteLine(f.Attributes.Count);
                        Console.WriteLine("\n");
                    }
                }
            }
        }
    }
}
