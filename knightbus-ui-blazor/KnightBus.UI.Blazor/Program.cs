using KnightBus.UI.Blazor;
using KnightBus.UI.Blazor.Components;
using KnightBus.UI.Blazor.Data;
using KnightBus.UI.Blazor.Providers.ServiceBus;
using KnightBus.UI.Blazor.Providers.StorageBus;
using MudBlazor.Services;
using Environment = KnightBus.UI.Blazor.Environment;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddMudServices()
    .AddServiceBus()
    .UseBlobStorage()
    .AddSingleton<EnvironmentService>()
    .AddScoped<QueueService>()
    .AddScoped<TopicsService>();

builder.Services.AddKeyedSingleton(Environment.Dev, builder.Configuration.GetEnvironmentConfig(Environment.Dev));
builder.Services.AddKeyedSingleton(Environment.Test, builder.Configuration.GetEnvironmentConfig(Environment.Test));
builder.Services.AddKeyedSingleton(Environment.Prod, builder.Configuration.GetEnvironmentConfig(Environment.Prod));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
