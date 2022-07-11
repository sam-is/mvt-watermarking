using System.Net.Sockets;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Filters;
using Serilog.Formatting.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Linq;
using Microsoft.Data.Sqlite;
using System;

//namespace mvt_Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors((options) =>
{
    options.AddPolicy("all", (policy) =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Host.UseSerilog((_, _, configuration) =>
{
    configuration.WriteTo.Logger(lc =>
            lc.Filter.ByExcluding(Matching.FromSource("mvt"))
                .WriteTo.File(new JsonFormatter(),
                    @"D:\mvt-watermarking\AspNetServer\mvt_Server\mvt_Server\log.json")) // прописать в настройках
        .WriteTo.Logger(lc =>
            lc.Filter.ByIncludingOnly(Matching.FromSource("mvt"))
                .WriteTo.File(@"D:\mvt-watermarking\AspNetServer\mvt_Server\mvt_Server\mvt.json"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();

app.UseCors("all");

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                       ForwardedHeaders.XForwardedProto
});

app.MapGet("/", (HttpContext httpContext, ILoggerFactory loggerFactory) =>
{
    Console.WriteLine("Begin of method");
    var remoteAddress = httpContext.Connection.RemoteIpAddress;
    if (remoteAddress != null)
    {
        if (remoteAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            remoteAddress = System.Net.Dns.GetHostEntry(remoteAddress).AddressList
                .First(x => x.AddressFamily == AddressFamily.InterNetwork);
        }
    }

    var log = loggerFactory.CreateLogger("mvt");
    log.LogCritical("IP:" + remoteAddress);
    Console.WriteLine("End of method");
    return Results.Ok("!!!");
});

app.MapGet("api/tiles/{z}/{x}/{y}/", (int z, int x, int y, HttpContext httpContext, ILoggerFactory loggerFactory) =>
{
    var connectionStringBuilder = new SqliteConnectionStringBuilder() { DataSource = "D:\\maximum_mbtiles.mbtiles" };
    using var sqliteConnection = new SqliteConnection(connectionStringBuilder.ConnectionString);
    sqliteConnection.Open();

    using var sqlCommandGet = new SqliteCommand(@"SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", sqliteConnection);
    sqlCommandGet.Parameters.AddWithValue("$z", z);
    sqlCommandGet.Parameters.AddWithValue("$x", x);
    sqlCommandGet.Parameters.AddWithValue("$y", (1 << z) - y - 1);
    var tile = (byte[])sqlCommandGet.ExecuteScalar();
    if (tile == null) return Results.NoContent();
    httpContext.Response.Headers.Add("Content-Encoding", "gzip");
    return Results.File(tile, "application/vnd.mapbox-vector-tile");
});

app.Run();
