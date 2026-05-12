var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddAuthentication();

// Register application services
builder.Services.AddScoped<ApiImagenesAppVoluntario.Services.ImagenService>();

// Add Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "API Imagenes App Voluntario",
        Description = "API para manejar imágenes de la aplicación Voluntario+",
        Version = "v1"
    });

    options.OperationFilter<ApiImagenesAppVoluntario.Controllers.ApiKeyHeaderFilter>();
    options.CustomSchemaIds(x => x.FullName);
});

var app = builder.Build();

// Enable middleware to serve generated Swagger as a JSON endpoint
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ApiImagenesAppVoluntario");
    });
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
