using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Haihv.Vbdlis.Tools.Api.Playwright.Models;
using Haihv.Vbdlis.Tools.Api.Playwright.Options;
using Haihv.Vbdlis.Tools.Api.Playwright.Services;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.Configure<PlaywrightSettings>(builder.Configuration.GetSection(PlaywrightSettings.SectionName));
builder.Services.Configure<VbdlisSettings>(builder.Configuration.GetSection(VbdlisSettings.SectionName));
builder.Services.AddSingleton<IVbdlisPlaywrightSearchService, VbdlisPlaywrightSearchService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapPost("/api/v1/vbdlis/search-giay-to",
        async (VbdlisBatchSearchRequest request, IVbdlisPlaywrightSearchService service, ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            var validationContext = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

            if (!isValid)
            {
                return Results.ValidationProblem(validationResults
                    .SelectMany(x => x.MemberNames.DefaultIfEmpty(string.Empty).Select(member => (member, x.ErrorMessage ?? "Invalid value")))
                    .GroupBy(x => x.member)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Item2).ToArray()));
            }

            try
            {
                var response = await service.SearchAsync(request, cancellationToken);
                return Results.Ok(response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Search VBDLIS failed.");
                return Results.Problem("Có lỗi khi truy vấn VBDLIS.", statusCode: StatusCodes.Status500InternalServerError);
            }
        })
    .WithName("SearchVbdlisBySoGiayTo")
    .WithDescription("Đăng nhập VBDLIS bằng Playwright và tìm kiếm theo danh sách số giấy tờ.");

app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }));

app.Run();
