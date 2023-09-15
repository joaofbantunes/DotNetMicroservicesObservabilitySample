using Api;
using Bogus;
using Confluent.Kafka;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shared;

// need to extract this, because logging configuration and tracing/metrics configuration don't share this by themselves
Action<ResourceBuilder> configureResource = resourceBuilder =>
{
    resourceBuilder.AddService(
        serviceName: "Api",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
        serviceInstanceId: Environment.MachineName);
};

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddOpenTelemetry(options =>
{
    var resourceBuilder = ResourceBuilder.CreateDefault();
    configureResource(resourceBuilder);
    options.SetResourceBuilder(resourceBuilder);

    options.IncludeScopes = true;

    // false by default, which means the message wouldn't have the placeholders replaced
    options.IncludeFormattedMessage = true;

    // Not relevant here as we're not using it, but this allows the state value passed to the logger.Log method to be parsed,
    // in case it isn't a collection of KeyValuePair<string, object?>, which is the case when we use things like logger.LogInformation.
    // When we use these extension methods, the state value is a FormattedLogValues, which implements IReadOnlyList<KeyValuePair<string, object?>>.
    // For more info, see:
    // - https://github.com/open-telemetry/opentelemetry-dotnet/blob/5357f1744421b1c04f70b128dec1fe14b7ed500c/src/OpenTelemetry/Logs/ILogger/OpenTelemetryLogger.cs#L152
    // - https://github.com/dotnet/runtime/blob/f107b63fca1bd617a106e3cc7e86b337151bff79/src/libraries/Microsoft.Extensions.Logging.Abstractions/src/LoggerExtensions.cs#L390
    // - https://github.com/dotnet/runtime/blob/f107b63fca1bd617a106e3cc7e86b337151bff79/src/libraries/Microsoft.Extensions.Logging.Abstractions/src/ILogger.cs#L23
    // - https://github.com/dotnet/runtime/blob/f107b63fca1bd617a106e3cc7e86b337151bff79/src/libraries/Microsoft.Extensions.Logging.Abstractions/src/FormattedLogValues.cs
    options.ParseStateValues = true;

    options.AddOtlpExporter(exporterOptions =>
    {
        exporterOptions.Endpoint =
            builder
                .Configuration
                .GetSection(nameof(OpenTelemetrySettings))
                .Get<OpenTelemetrySettings>()!
                .Endpoint;
    });
});
builder.Services.AddSingleton(new Faker());
builder.Services.AddSingleton(
    builder.Configuration.GetSection(nameof(KafkaSettings)).Get<KafkaSettings>()!);
builder.Services.AddSingleton(
    builder.Configuration.GetSection(nameof(OpenTelemetrySettings)).Get<OpenTelemetrySettings>()!);
builder.Services.AddSingleton(s =>
    new ProducerBuilder<Guid, StuffHappened>(
            new ProducerConfig
            {
                BootstrapServers = s.GetRequiredService<KafkaSettings>().BootstrapServers
            })
        .SetKeySerializer(new GuidSerde())
        .SetValueSerializer(new JsonEventSerde<StuffHappened>())
        .Build());

builder.Services.AddSingleton<EventPublisherMetrics>();
builder.Services.AddSingleton<EventPublisher>();


builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(configureResource)
    .WithTracing(providerBuilder =>
    {
        providerBuilder
            .AddAspNetCoreInstrumentation()
            .AddSource(nameof(EventPublisher))
            .AddOtlpExporter(options =>
            {
                options.Endpoint =
                    builder
                        .Configuration
                        .GetSection(nameof(OpenTelemetrySettings))
                        .Get<OpenTelemetrySettings>()!
                        .Endpoint;

                options.Protocol = OtlpExportProtocol.Grpc;
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddAspNetCoreInstrumentation()
            // .AddHttpClientInstrumentation() // we're not using, but important to know it exists
            
            // be careful, metric names are still being adjusted, as we're using a preview version of .NET 8
            .AddView("http.server.request.duration", new ExplicitBucketHistogramConfiguration
            {
                Boundaries = new double[] { 0, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 }
            })
            .AddMeter(
                "System.Runtime",
                "Microsoft.AspNetCore.Hosting",
                "Microsoft.AspNetCore.Server.Kestrel",
                EventPublisherMetrics.MeterName)
            
            // .AddPrometheusExporter()
            
            .AddOtlpExporter(options =>
            {
                options.Endpoint =
                    builder
                        .Configuration
                        .GetSection(nameof(OpenTelemetrySettings))
                        .Get<OpenTelemetrySettings>()!
                        .Endpoint;

                options.Protocol = OtlpExportProtocol.Grpc;
            });
    });


var app = builder.Build();

app.MapPost(
    "/do-stuff",
    async (EventPublisher eventPublisher, Faker faker)
        =>
    {
        await eventPublisher.PublishAsync(new(Guid.NewGuid(), faker.Hacker.Verb()));
        return TypedResults.NoContent();
    });

// from OpenTelemetry.Exporter.Prometheus.AspNetCore
//app.MapPrometheusScrapingEndpoint();

app.Run();