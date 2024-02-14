# mvt-watermarking
Implementation of algorithms for embedding digital watermarks in spatial data made in Mapbox Vector Tiles format


# Library
TODO added Nuget Package

## QimMvtWatermark
Algorithm works with NetTopologySuite VectorTileTree.

Dependencies:
- NetTopologySuite
- NetTopologySuite.Features
- NetTopologySuite.IO.VectorTiles
- NetTopologySuite.IO.VectorTiles.Mapbox
- protobuf-net

## Quick Start
### Embeding watermark
```csharp
var key = 0
var message = new BitArray(new[] { true, true, false, false, true, true, false, false });
var options = new QimMvtWatermarkOptions();
var mvtWatermark = new QimMvtWatermark(options);
var tileWatermarked = mvtWatermark.Embed(tileTree, key, message);
```

### Extracting watermark

```csharp
var key = 0
var options = new QimMvtWatermarkOptions();
var mvtWatermark = new QimMvtWatermark(options);
var message = mvtWatermark.Extract(tileTree, key);
```
### Options

#### Main options
- `k`
  
Coefficient for t2 when embedded. The larger this parameter, the more accurate whne extracting. Valid values: [0, 1). Default value: 0.9.
- `t2`
  
The relative number of how many coordinates of the points of the objects will need to be changed. Valid value: \[0, 1\]. Default value: 0.2.
- `nb`

How many bits are embedded in one tile. If the tile has few objects, then the extraction accuracy will decrease with increasing. Also, as the value increases, the processing time of a single tile increases. Linked to r and m by the following formula: nb*r = m^2. Default value: 8
- `r`
  
How many times will the same bit be embedded in the same tile. If the tile has few objects, then the extraction accuracy will decrease with increasing. Also, as the value increases, the processing time of a single tile increases. Linked to nb and m by the following formula: nb*r = m^2. Default value: 8
- `mode`

There are 3 modes of the algorithm: `Repeat`, `WithCheck`, `WithTilesMajorityVote`. Default value: `WithTilesMajorityVote`

  1. `Repeat`

  If all message is embedded, but there are still tiles left, then the message will be cyclically embedded further.

  This is the best option, if you need to embed a watermark into several tiles with a large number of objects, you can select the necessary options, such as `nb` and `r` to embed all message into tile tree.
  
  2. `WithCheck`

  If part of message can't be embed in tile, it embed in next tile. This mode is slower than other, but it checks whether it was possible to embed part of the message.

  If all message is embedded, but there are still tiles left, then the message will be cyclically embedded further 
  
  3. `WithTilesMajorityVote`

  Embeds part of message by id tile module. Message is divided into parts, each tile is connected to one part by a ratio: `part = parts[tileId % partsCount]`.

  You can embed with this mode incrementally, because parts of the message are deterministically calculated from the options, and the tile id does not change. The link between tile id and a part of the message is always constant.

  This is the best option, if you need to embed a watermark into a lot of tiles with a small number of objects.

  If the embedding was carried out by this mode, then when extracting, you need to add the `messageLength` parameter in the options, so that the link between the part of the message and the tile id can be calculated


#### Additional options
- `t1`

How many objects should the square (see m options) in tile have at least for it to be taken when extracting. Default value: 5
- `extent`

The size of the grid according to which the coordinates will be quantized. It is recommended to take 2048 or 4096. At 4096, the extraction accuracy may be less than at 2048. Default value: 2048.
- `distance`

The minimum distance at which points with different boolean values should be located. Default value: 2.
- `m`

m^2 - the number of squares into which the tile is divided, 1 bit is embedded in each of the squares. Linked to nb and r by the following formula: nb*r = m^2. If nb and r selected m automatically compute. Default value: 8.
- `countMaps`

The number of quantization matrices to be calculated. The more, the longer the algorithm will work, but at the same time the level of randomness will increase. Default value: 10.
- `isGeneralExtractionMethod`

Additional extraction method. It works worse, so it is not recommended to choose it. Default value: false.

- `messageLength`

The length of the embedded message in bits. If the second `WithTilesMajorityVote` is selected, then it is a required parameter, without which extraction will not work.

### How get VectorTileTree with NetTopologySuite.IO.VectorTiles.Mapbox
#### From .mbtiles
```csharp
public static VectorTileTree GetVectorTileTreeFromDb(string path, int z)
{
    using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
    sqliteConnection.Open();
    var reader = new MapboxTileReader();
    var tileTree = new VectorTileTree();

    using var command = new SqliteCommand(@"SELECT tile_column, tile_row, tile_data FROM tiles WHERE zoom_level = $z", sqliteConnection);
    command.Parameters.AddWithValue("$z", z);
    using var dbReader = command.ExecuteReader();

    while (dbReader.Read())
    {
          var x = dbReader.GetInt32(0);
          var y = (1 << z) - dbReader.GetInt32(1) - 1;

          var stream = dbReader.GetStream(2);

          using var decompressor = new GZipStream(stream, CompressionMode.Decompress, false);
          var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

          tileTree[tile.TileId] = tile;
    }

    return tileTree;
}
```
#### From directory with tile tree
```csharp
  public static VectorTileTree ReadFromFiles(string path)
  {
      var reader = new MapboxTileReader();
      var tileTree = new VectorTileTree();

      var directoryInfo = new DirectoryInfo(path);
      foreach (var z in directoryInfo.GetDirectories())
      {
          foreach (var x in z.GetDirectories())
          {
              foreach (var y in x.GetFiles())
              {
                  using var fileStream = y.Open(FileMode.Open);
                  fileStream.Seek(0, SeekOrigin.Begin);
                  using var decompressor = new GZipStream(fileStream, CompressionMode.Decompress, false);
                  var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(Convert.ToInt32(x.Name), Convert.ToInt32(y.Name), Convert.ToInt32(z.Name)));

                  if (!tile.IsEmpty)
                      tileTree[tile.TileId] = tile;
              }
          }
      }
      return tileTree;
  }
```
# Console
## Usage
Embed

    MvtWatermarkConsole.exe -s (source) -k (key) -m embed -w (watermark) -o (output) [options]
Extract

    MvtWatermarkConsole.exe -s (source) -k (key) -m extract [options]
## Examples
### Source - .mbtiles

Embed

     MvtWatermarkConsole.exe -s db.mbtiles -k 0 -m embed -w test -o output_folder --minz 12 --maxz 12
Extract

    MvtWatermarkConsole.exe -s db.mbtiles -k 0 -m extract --minz 12 --maxz 12

### Source - directory with tile tree
tiles - directory with tile tree
Embed

     MvtWatermarkConsole.exe -s tiles -k 0 -m embed -w test -o output_folder --minz 12 --maxz 12
Extract

    MvtWatermarkConsole.exe -s tiles -k 0 -m extract --minz 12 --maxz 12
## Parameters
### Source
`-s`, `--source`.

Source of tile. May be .mbtiles db or directory with tile tree struct. Required parameter.

### Key
`-k`, `--key`.

Secret key. Integer value. Required parameter.

### Mode
`-m`, `--mode`.

Mode of algorthm. Valid values: embed, extract. Required value.

### Config
`-c`, `--config`.

Path to config file in json foramt. If not selected, default options of algorithm will be use. About options see [Options](README.md#options).

Example config.json:
```json
{
  "k": 0.9,
  "t2": 0.2,
  "t1": 5,
  "extent": 2048,
  "distance": 2,
  "nb": 1,
  "r": 4,
  "m": null,
  "countMaps": 10,
  "isGeneralExtractionMethod": false,
  "mode": "WithTilesMajorityVote",
  "messageLength":  24
}
```
### Watermark
`-w`, `--watermark`. 

Embeded watermark string. Required for embed mode.

### Output
`-o`, `--output`. 

For embed mode output for watermarked tile tree. Required for embed mode. 

For extract mode output for embeded watermark. If not select with extract mode, embeded message displays in console.

### MinZ
`--minz`

The minimum zoom level that tiles will be selected from.

### MaxZ
`--maxz`

The maximum zoom level to which tiles will be selectedю


# How algorithms work
## QimMvtWatermark

# Links
Scientific publication: https://www.scitepress.org/DigitalLibrary/Link.aspx?doi=10.5220/0012044500003473
