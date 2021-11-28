// Copyright (c) Martin Costello, 2021. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Configure the JSON serializer to indent
// the JSON to make it more human readable.
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

// Create a JSON serializer context that uses the same
// serialization options as the app is configured to use.
builder.Services.AddSingleton(services =>
{
    var options = services.GetRequiredService<IOptions<JsonOptions>>().Value;
    return new StellarJsonSerializerContext(options.SerializerOptions);
});

// Register our derived JsonSerializerContext instance as an instance
// of JsonSerializerContext itself, in addition to StellarJsonSerializerContext.
builder.Services.AddSingleton<JsonSerializerContext>(
    services => services.GetRequiredService<StellarJsonSerializerContext>());

var app = builder.Build();

// Create the stars and planets that can be queried from the HTTP API
var planets = new[]
{
    new Planet("Mercury", 57.91 * 1_000_000),
    new Planet("Venus", 108.2 * 1_000_000),
    new Planet("Earth", 149.6 * 1_000_000),
    new Planet("Mars", 227.9 * 1_000_000),
    new Planet("Jupiter", 778.5 * 1_000_000),
    new Planet("Saturn", 1.434 * 1_000_000_000),
    new Planet("Uranus", 2.871 * 1_000_000_000),
    new Planet("Neptune", 4.495 * 1_000_000_000)
};

var stars = new[]
{
    new Star("Sun", 1),
    new Star("Proxima Centauri", 0.122),
    new Star("Rigil Kentaurus", 1),
    new Star("Toliman", 0.77),
    new Star("Barnard's Star", 0.13),
    new Star("Polaris", 6.5),
};

// Map the HTTP endpoints for stars. These endpoints rely on
// an instance of JsonSerializerContext being registered with
// the service collection so that the JSON serialization is
// left as an implementation detail and is not explicitly dealt
// with by the code for the endpoints themselves.
//
// We use the Results.Extensions.Json() method instead of
// Results.Json() so we can use the extensibility hook to use
// the JSON source generator to write our objects to the HTTP
// response instead of the default which does not have a way
// for use to use our source-generated serialization code.
app.MapGet("/stars", () => Results.Extensions.Json(stars));
app.MapGet("/stars/{name}", (string name) =>
{
    var star = stars
        .Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
        .FirstOrDefault();

    if (star is null)
    {
        return Results.Problem("Star not found.", statusCode: StatusCodes.Status404NotFound);
    }

    return Results.Extensions.Json(star);
});

// Map the HTTP endpoints for the planets. These endpoints specify the source-generated
// JsonTypeInfo<T> properties on our custom StellarJsonSerializerContext class so that
// they use the serializer implementation that was generated, which gives better performance.
var context = app.Services.GetRequiredService<StellarJsonSerializerContext>();

app.MapGet("/planets", () => Results.Extensions.Json(planets, context.PlanetArray));
app.MapGet("/planets/{name}", (string name) =>
{
    var planet = planets
        .Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
        .FirstOrDefault();

    if (planet is null)
    {
        return Results.Problem("Planet not found.", statusCode: StatusCodes.Status404NotFound);
    }

    return Results.Extensions.Json(planet, context.Planet);
});

// Run the API
app.Run();

// Define our planet and star classes to return data from the API

public class Planet
{
    public Planet(string name, double distanceFromStar)
    {
        Name = name;
        DistanceFromStar = distanceFromStar;
    }

    public string Name { get; set; }
    public double DistanceFromStar { get; set; }
}

public class Star
{
    public Star(string name, double solarMasses)
    {
        Name = name;
        SolarMasses = solarMasses;
    }

    public string Name { get; set; }
    public double SolarMasses { get; set; }
}

// Define a custom JsonSerializerContext implementation so that we can use
// the JSON source generator. Each root-level type that is (de)serialized
// is registered using a [JsonSerializable] attribute so that the appropriate
// code to handle the serialization for our planets and stars is generated.
[JsonSerializable(typeof(Planet))]
[JsonSerializable(typeof(Planet[]))]
[JsonSerializable(typeof(Star))]
[JsonSerializable(typeof(Star[]))]
public partial class StellarJsonSerializerContext : JsonSerializerContext
{
}
