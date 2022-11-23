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

namespace MvtWatermark
{
    internal class TestProgram
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Работает TestProgram");

            var NdWm = new NoDistortionWatermark.NoDistortionWatermark(2);

            var vtTree = new VectorTileTree();
            ulong tile_id;
            VectorTile vt = createVectorTile(out tile_id);
            vtTree[tile_id] = vt;

            var boolArr = new bool[] { true, false, true };
            var message = new System.Collections.BitArray(boolArr);
            Console.WriteLine(message.ToString);
            //NdWm.Embed(vtTree, 123, message);
            NdWm.EmbedAndWriteToFile(vtTree, 123, message);

            Console.WriteLine("Встраивание завершено"); // отладка


            //Create a MapboxTileReader.
            var reader = new MapboxTileReader();

            //Define which tile you want to read. You may be able to extract the x/y/zoom info from the file path of the tile. 
            var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tile_id);

            var filePath = "C:\\SerializedTiles\\SerializedWithWatermark\\10\\0\\0.mvt";
            //Open a vector tile file as a stream.
            using (var fs = new FileStream(filePath, FileMode.Open))
            {
                //Read the vector tile.
                vt = reader.Read(fs, tileDefinition);

                //Loop through each layer.
                foreach (var l in vt.Layers)
                {
                    //Access the features of the layer and do something with them. 
                    var features = l.Features;
                    Console.WriteLine("Фича: ");
                    Console.WriteLine(features[0].Geometry);
                    //Console.WriteLine($"Количество точек в геометрии: {features[0].Geometry.Length}");
                    Console.WriteLine(features[0].Attributes.Count);
                }
            }


            Console.WriteLine("TestProgram завершил работу"); // отладка
        }

        static VectorTile createVectorTile(out ulong tile_id)
        {
            int x, y, zoom;
            x = 0;
            y = 0;
            zoom = 10;

            tile_id = NetTopologySuite.IO.VectorTiles.Tiles.Tile.CalculateTileId(zoom, x, y);
            //Define which tile you want to create.
            var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, zoom);

            //Create a vector tile instance and pass om the tile ID from the tile definition above.
            var vt = new VectorTile { TileId = tileDefinition.Id };

            //Create one or more layers. Ideally one layer per dataset.
            var lyr = new Layer { Name = "layer1" };

            var coordinateCollection = new List<Coordinate>();
            for (var i = 1; i < 41; i++)
            {
                coordinateCollection.Add(new Coordinate(i*i, i*i));
            }
            var coordinateArray = coordinateCollection.ToArray();

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

            return vt;
        }

        static Feature createFeature(uint numOfdots, int id, bool isPolygon=false)
        {
            var rand = new Random();
            if (numOfdots == 1)
            {
                return new Feature
                {
                    Geometry = new Point(new Coordinate(rand.Next(4096), rand.Next(4096))),
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
                //coordinateCollection.Add(new Coordinate(i * i, i * i));
                coordinateCollection.Add(new Coordinate(rand.Next(4096), rand.Next(4096)));
            }
            var coordinateArray = coordinateCollection.ToArray();
            return new Feature
            {
                Geometry = new LineString(coordinateArray),
                Attributes = new AttributesTable(new Dictionary<string, object>()
                {
                    ["LN_ID"] = id,
                    ["type"] = "Linestring",
                })
            };
        }
    }
}
