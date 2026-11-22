using System.Text.Json.Serialization;
using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Grades;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Features.Requests;
using Dekanat.Api.Features.Schedule;
using Dekanat.Api.Features.Staff;
using Dekanat.Api.Infrastructure.Errors;
using Dekanat.Api.Infrastructure.Health;
using Dekanat.Api.Infrastructure.OpenApi;
using Dekanat.Api.Infrastructure.Persistence;
using Dekanat.Api.Infrastructure.Seed;
using Dekanat.Api.Infrastructure.Time;

WebApplicationBuilder builder = WebApplication.CreateBuilder( args );

builder.Services.AddErrorHandling();
builder.Services.AddDekanatOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddDatabase();
builder.Services.AddDekanatIdentity();
builder.Services.AddDekanatSession();
builder.Services.AddUniversityTime();
builder.Services.ConfigureHttpJsonOptions( options =>
    options.SerializerOptions.Converters.Add( new JsonStringEnumConverter() ) );

WebApplication app = builder.Build();

app.UseForwardedHeaders();
app.UseErrorHandling();
app.UseDekanatSession();

RouteGroupBuilder api = app.MapGroup( "/api" ).WithProblemResponses();
api.MapHealth();
api.MapAuth();
api.MapProfile();
api.MapSchedule();
api.MapGrades();
api.MapRequests();
api.MapStaff();

if ( !OpenApiSetup.IsGeneratingDocument )
{
    await app.MigrateDatabaseAsync();
    await app.SeedDemoDataAsync();
}

await app.RunAsync();
