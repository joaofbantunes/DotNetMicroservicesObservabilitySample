using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Shared;

namespace Api;

public static class EventPublisherActivitySource
{
    private const string Name = "event publish";
    private const ActivityKind Kind = ActivityKind.Producer;
    private const string EventTopicTag = "event.topic";
    private const string EventIdTag = "event.id";
    private const string EventTypeTag = "event.type";

    private static readonly ActivitySource ActivitySource
        = new(nameof(EventPublisher));
    private static readonly TextMapPropagator Propagator
        = Propagators.DefaultTextMapPropagator;

    public static Activity? StartActivity(string topic, IEvent @event)
    {
        if (!ActivitySource.HasListeners())
        {
            return null;
        }

        // ReSharper disable once ExplicitCallerInfoArgument
        return ActivitySource.StartActivity(
            name: Name,
            kind: Kind,
            tags: new KeyValuePair<string, object?>[]
            {
                new (EventTopicTag, topic),
                new (EventIdTag, @event.Id),
                new (EventTypeTag, @event.GetType().Name),
            });
    }

    public static Headers EnrichHeadersWithTracingContext(Activity? activity, Headers headers)
    {
        if (activity is null)
        {
            return headers;
        }

        // on the receiving side,
        // the service will extract this information
        // to maintain the overall tracing context

        var contextToInject = activity?.Context
            ?? Activity.Current?.Context
            ?? default;
        
        Propagator.Inject(
            new PropagationContext(contextToInject, Baggage.Current),
            headers,
            InjectTraceContext);

        return headers;

        static void InjectTraceContext(Headers headers, string key, string value)
            => headers.Add(key, Encoding.UTF8.GetBytes(value));
    }
}