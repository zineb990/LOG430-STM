using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using System.Threading.RateLimiting;
using RouteTimeProvider.RestClients;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Exporter;
using OpenTelemetry.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using Application.Interfaces;
using Application.Usecases;
using Microsoft.AspNetCore.RateLimiting;
using OpenTelemetry;
using System.Reflection.PortableExecutable;



namespace RouteTimeProvider
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Validate();

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddRateLimiter(_ => _
                .AddFixedWindowLimiter(policyName: "fixed", options =>
                {
                    options.PermitLimit = 2;
                    options.Window = TimeSpan.FromSeconds(10);
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 0;
                }));

            builder.Services.AddSingleton<IRouteTimeProvider, TomTomClient>();
            builder.Services.AddScoped<CarTravel>();

            builder.Services.AddControllers();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            

            builder.Logging.AddOpenTelemetry(options =>
{
    options
        .SetResourceBuilder(
            ResourceBuilder.CreateDefault()
                .AddService("RouteTimeProvider"));

});
            builder.Services.AddOpenTelemetry()
                    .ConfigureResource(otelBuilder => otelBuilder
                        .AddService(serviceName: "routetimeprovider"))
                    .WithTracing(otelBuilder => otelBuilder
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddConsoleExporter()
                        .AddZipkinExporter(options =>
                        {
                            options.Endpoint = new Uri("http://host.docker.internal:9411/api/v2/spans");
                        })
                    .ConfigureServices(services =>
                    {
                        //services.Configure<ZipkinExporterOptions>(builder.Configuration.GetSection("OpenTelemetrySettings:ZipkinSettings"));
                        services.Configure<AspNetCoreTraceInstrumentationOptions>(builder.Configuration.GetSection("AspNetCoreInstrumentation"));
                        services.Configure<ZipkinExporterOptions>(builder.Configuration.GetSection("Zipkin"));
                    }))
                   .WithMetrics(metrics => metrics
                        .AddAspNetCoreInstrumentation()
                        .AddConsoleExporter());

            builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(builder.Configuration.GetSection("AspNetCoreInstrumentation"));
            builder.Services.Configure<ZipkinExporterOptions>(builder.Configuration.GetSection("Zipkin"));


            var app = builder.Build();

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseCors(options =>
            {
                options.AllowAnyOrigin();
                options.AllowAnyHeader();
                options.AllowAnyMethod();
            });

            app.MapControllers();

            app.Run();
        }

        private static void Validate()
        {
            var apiKey = Environment.GetEnvironmentVariable("API_KEY") ?? throw new Exception("API_KEY environment variable not found");

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentNullException("API_KEY", "The API key was not defined in the environment variables; this is critical.");
            }
        }
    }
}
