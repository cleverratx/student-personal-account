using System.Text.Json.Serialization;
using Dekanat.Api.Infrastructure.Health;
using Dekanat.Api.Infrastructure.OpenApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder( args );

builder.Services.AddDekanatOpenApi();
builder.Services.AddHealthChecks();
builder.Services.ConfigureHttpJsonOptions( options =>
    options.SerializerOptions.Converters.Add( new JsonStringEnumConverter() ) );

WebApplication app = builder.Build();

RouteGroupBuilder api = app.MapGroup( "/api" );
api.MapHealth();

app.Run();
