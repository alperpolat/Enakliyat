using Enakliyat.Domain;
using Enakliyat.Infrastructure;
using Enakliyat.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("EnakliyatDb");

builder.Services.AddDbContext<EnakliyatDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/api/move-requests", async (MoveRequest request, IUnitOfWork uow, CancellationToken ct) =>
{
    await uow.MoveRequests.AddAsync(request, ct);
    await uow.CommitAsync(ct);
    return Results.Created($"/api/move-requests/{request.Id}", request);
});

app.MapGet("/api/move-requests", async (IUnitOfWork uow, CancellationToken ct) =>
{
    var items = await uow.MoveRequests.GetAllAsync(ct);
    return Results.Ok(items);
});

app.MapGet("/api/move-requests/{id:int}", async (int id, IUnitOfWork uow, CancellationToken ct) =>
{
    var item = await uow.MoveRequests.GetByIdAsync(id, ct);
    return item is not null ? Results.Ok(item) : Results.NotFound();
});

app.Run();
