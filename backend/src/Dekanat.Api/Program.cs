using System.Text.Json.Serialization;
using Dekanat.Api.Infrastructure.Errors;
using Dekanat.Api.Infrastructure.Health;
using Dekanat.Api.Infrastructure.OpenApi;
using Dekanat.Api.Infrastructure.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder( args );

builder.Services.AddErrorHandling();
builder.Services.AddDekanatOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddDatabase();
builder.Services.ConfigureHttpJsonOptions( options =>
    options.SerializerOptions.Converters.Add( new JsonStringEnumConverter() ) );

WebApplication app = builder.Build();

app.UseErrorHandling();

RouteGroupBuilder api = app.MapGroup( "/api" ).WithProblemResponses();
api.MapHealth();

if ( !OpenApiSetup.IsGeneratingDocument )
{
    await app.MigrateDatabaseAsync();
}

await app.RunAsync();
