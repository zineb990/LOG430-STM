using Application.Interfaces;
using Application.Interfaces.Policies;
using Application.Usecases;
using Configuration.Policies;
using Controllers.Controllers;
using Infrastructure.Clients;
using MassTransit;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.OpenApi.Models;
using MqContracts;
using RabbitMQ.Client;
using ServiceMeshHelper;
using ServiceMeshHelper.Controllers;
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

namespace Configuration
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            ConfigureServices(builder.Services);
            builder.Logging.AddOpenTelemetry(options =>
            {
                options
                    .SetResourceBuilder(
                        ResourceBuilder.CreateDefault()
                            .AddService("TripComparator"));

            });
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(otelBuilder => otelBuilder
                    .AddService(serviceName: "tripcomparator"))
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

            app.UseHttpsRedirection();

            app.UseCors(
                options =>
                {
                    options.AllowAnyOrigin();
                    options.AllowAnyHeader();
                    options.AllowAnyMethod();
                }
            );

            app.UseAuthorization();

            app.MapControllers();

            await app.RunAsync();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            ConfigureMassTransit(services);

            services.AddControllers().PartManager.ApplicationParts.Add(new AssemblyPart(typeof(CompareTripController).Assembly));

            services.AddEndpointsApiExplorer();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "TripComparator", Version = "v1" });
                c.EnableAnnotations();
            });

            services.AddSingleton<IHostInfo, HostInfo>();

            services.AddScoped(typeof(IInfiniteRetryPolicy<>), typeof(InfiniteRetryPolicy<>));

            services.AddScoped(typeof(IBackOffRetryPolicy<>), typeof(BackOffRetryPolicy<>));

            services.AddScoped<CompareTimes>();

            services.AddScoped<IRouteTimeProvider, RouteTimeProviderClient>();

            services.AddScoped<IDataStreamWriteModel, MassTransitRabbitMqClient>();

            services.AddScoped<IBusInfoProvider, StmClient>();
        }

        private static void ConfigureMassTransit(IServiceCollection services)
        {
            var hostInfo = new HostInfo();
            
            var routingData = RestController.GetAddress(hostInfo.GetMQServiceName(), LoadBalancingMode.RoundRobin).Result.First();

            var uniqueQueueName = $"time_comparison.node_controller-to-any.query.{Guid.NewGuid()}";

            services.AddMassTransit(x =>
            {
                x.AddConsumer<TripComparatorMqController>();

                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host($"rabbitmq://{ routingData.Host }:{routingData.Port}", c =>
                    {
                        c.RequestedConnectionTimeout(100);
                        c.Heartbeat(TimeSpan.FromMilliseconds(50));
                        c.PublisherConfirmation = true;
                    });

                    cfg.Message<BusPositionUpdated>(topologyConfigurator => topologyConfigurator.SetEntityName("bus_position_updated"));
                    cfg.Message<CoordinateMessage>(topologyConfigurator => topologyConfigurator.SetEntityName("coordinate_message"));

                    cfg.ReceiveEndpoint(uniqueQueueName, endpoint =>
                    {
                        endpoint.ConfigureConsumeTopology = false;

                        endpoint.Bind<CoordinateMessage>(binding =>
                        {
                            binding.ExchangeType = ExchangeType.Topic;
                            binding.RoutingKey = "trip_comparison.query";
                        });

                        endpoint.ConfigureConsumer<TripComparatorMqController>(context);
                    });

                    cfg.Publish<BusPositionUpdated>(p => p.ExchangeType = ExchangeType.Topic);
                });
            });
        }
    }
}