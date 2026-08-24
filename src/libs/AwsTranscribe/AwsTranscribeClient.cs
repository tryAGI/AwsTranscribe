#nullable enable
#pragma warning disable MEAI001

using System.Runtime.CompilerServices;
using Amazon;
using Amazon.Runtime.EventStreams;
using Amazon.TranscribeService;
using Amazon.TranscribeStreaming;
using Amazon.TranscribeStreaming.Model;
using Microsoft.Extensions.AI;

namespace AwsTranscribe;

/// <summary>
/// Microsoft.Extensions.AI speech-to-text adapter over the official AWS Transcribe Streaming and batch clients.
/// </summary>
public sealed class AwsTranscribeClient : ISpeechToTextClient
{
    public const string DefaultLanguage = "en-US";
    public const string DefaultModelId = "standard";
    public const int DefaultSampleRateHertz = 16_000;

    private readonly bool _ownsClients;
    private readonly Uri _providerUri;
    private SpeechToTextClientMetadata? _metadata;

    /// <summary>Creates official AWS clients using the default credential chain.</summary>
    public AwsTranscribeClient(string regionSystemName)
        : this(
            new AmazonTranscribeStreamingClient(GetRegionEndpoint(regionSystemName)),
            new AmazonTranscribeServiceClient(GetRegionEndpoint(regionSystemName)),
            new Uri($"https://transcribestreaming.{regionSystemName}.amazonaws.com"),
            ownsClients: true)
    {
    }

    /// <summary>Creates an adapter over injected official AWS clients.</summary>
    public AwsTranscribeClient(
        IAmazonTranscribeStreaming streamingClient,
        IAmazonTranscribeService? batchClient = null,
        Uri? providerUri = null)
        : this(
            streamingClient,
            batchClient,
            providerUri ?? new Uri("https://transcribe.amazonaws.com"),
            ownsClients: false)
    {
    }

    private AwsTranscribeClient(
        IAmazonTranscribeStreaming streamingClient,
        IAmazonTranscribeService? batchClient,
        Uri providerUri,
        bool ownsClients)
    {
        StreamingClient = streamingClient ?? throw new ArgumentNullException(nameof(streamingClient));
        BatchClient = batchClient;
        _providerUri = providerUri ?? throw new ArgumentNullException(nameof(providerUri));
        _ownsClients = ownsClients;
    }

    /// <summary>The official low-latency AWS Transcribe Streaming client.</summary>
    public IAmazonTranscribeStreaming StreamingClient { get; }

    /// <summary>The official batch client for S3-backed transcription jobs, when supplied.</summary>
    public IAmazonTranscribeService? BatchClient { get; }

    public void Dispose()
    {
        if (!_ownsClients)
        {
            return;
        }

        StreamingClient.Dispose();
        BatchClient?.Dispose();
    }

    public object? GetService(System.Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return serviceKey is not null ? null :
            serviceType == typeof(SpeechToTextClientMetadata)
                ? (_metadata ??= new("aws-transcribe", _providerUri, DefaultModelId)) :
            serviceType.IsInstanceOfType(this) ? this :
            serviceType.IsInstanceOfType(StreamingClient) ? StreamingClient :
            BatchClient is not null && serviceType.IsInstanceOfType(BatchClient) ? BatchClient :
            null;
    }

    public async Task<SpeechToTextResponse> GetTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        var finalUpdates = new List<SpeechToTextResponseUpdate>();
        await foreach (var update in GetStreamingTextAsync(audioSpeechStream, options, cancellationToken)
            .ConfigureAwait(false))
        {
            if (update.Kind == SpeechToTextResponseUpdateKind.TextUpdated)
            {
                finalUpdates.Add(update);
            }
        }

        return CreateResponse(finalUpdates, GetModelId(options));
    }

    /// <summary>Aggregates final streaming updates into one Microsoft.Extensions.AI response.</summary>
    public static SpeechToTextResponse CreateResponse(
        IReadOnlyList<SpeechToTextResponseUpdate> finalUpdates,
        string? modelId = null)
    {
        ArgumentNullException.ThrowIfNull(finalUpdates);

        var deduplicatedUpdates = DeduplicateFinalUpdates(finalUpdates);
        var results = deduplicatedUpdates
            .Select(static update => update.RawRepresentation)
            .OfType<Result>()
            .ToArray();
        var alternatives = deduplicatedUpdates
            .SelectMany(static update => GetAdditionalPropertyValues<AwsTranscribeAlternative>(
                update,
                AwsTranscribePropertyNames.Alternatives))
            .ToArray();
        var items = deduplicatedUpdates
            .SelectMany(static update => GetAdditionalPropertyValues<AwsTranscribeItem>(
                update,
                AwsTranscribePropertyNames.Items))
            .ToArray();
        var startTimes = deduplicatedUpdates
            .Where(static update => update.StartTime.HasValue)
            .Select(static update => update.StartTime!.Value)
            .ToArray();
        var endTimes = deduplicatedUpdates
            .Where(static update => update.EndTime.HasValue)
            .Select(static update => update.EndTime!.Value)
            .ToArray();
        var properties = new AdditionalPropertiesDictionary
        {
            [AwsTranscribePropertyNames.Results] = results,
            [AwsTranscribePropertyNames.Alternatives] = alternatives,
            [AwsTranscribePropertyNames.Items] = items,
        };

        return new SpeechToTextResponse(string.Join(
            ' ',
            deduplicatedUpdates.Select(static update => update.Text).Where(static text => !string.IsNullOrWhiteSpace(text))))
        {
            ResponseId = deduplicatedUpdates.Select(static update => update.ResponseId).FirstOrDefault(static id => id is not null),
            ModelId = modelId,
            StartTime = startTimes.Length > 0 ? startTimes.Min() : null,
            EndTime = endTimes.Length > 0 ? endTimes.Max() : null,
            RawRepresentation = results,
            AdditionalProperties = properties,
        };
    }

    private static List<SpeechToTextResponseUpdate> DeduplicateFinalUpdates(
        IReadOnlyList<SpeechToTextResponseUpdate> finalUpdates)
    {
        var deduplicated = new List<SpeechToTextResponseUpdate>(finalUpdates.Count);
        var resultIndexes = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var update in finalUpdates)
        {
            var resultId = GetAdditionalPropertyString(update, AwsTranscribePropertyNames.ResultId);
            if (resultId is null)
            {
                // Without a provider result id there is no safe way to distinguish a duplicate
                // delivery from a speaker genuinely repeating the same phrase.
                deduplicated.Add(update);
                continue;
            }

            if (resultIndexes.TryGetValue(resultId, out var existingIndex))
            {
                // AWS can redeliver the same finalized result. Keep its original position in the
                // transcript, but use the latest representation in case metadata was completed.
                deduplicated[existingIndex] = update;
                continue;
            }

            resultIndexes[resultId] = deduplicated.Count;
            deduplicated.Add(update);
        }

        return deduplicated;
    }

    private static IReadOnlyList<T> GetAdditionalPropertyValues<T>(
        SpeechToTextResponseUpdate update,
        string key) =>
        update.AdditionalProperties?.TryGetValue(key, out var value) == true && value is IReadOnlyList<T> values
            ? values
            : [];

    private static string? GetAdditionalPropertyString(
        SpeechToTextResponseUpdate update,
        string key) =>
        update.AdditionalProperties?.TryGetValue(key, out var value) == true && value is string text
            ? NullIfEmpty(text)
            : null;

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        var request = CreateRequest(audioSpeechStream, options, cancellationToken);
        using var response = await StreamingClient
            .StartStreamTranscriptionAsync(request, cancellationToken)
            .ConfigureAwait(false);
        var responseId = NullIfEmpty(response.SessionId) ?? NullIfEmpty(response.RequestId) ?? Guid.NewGuid().ToString("N");
        var modelId = GetModelId(options);

        yield return new SpeechToTextResponseUpdate
        {
            Kind = SpeechToTextResponseUpdateKind.SessionOpen,
            ResponseId = responseId,
            ModelId = modelId,
            RawRepresentation = response,
            AdditionalProperties = CreateSessionProperties(response),
        };

        await foreach (var streamEvent in response.TranscriptResultStream
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            if (streamEvent is not TranscriptEvent transcriptEvent)
            {
                continue;
            }

            foreach (var result in transcriptEvent.Transcript?.Results ?? [])
            {
                if (CreateUpdate(result, responseId, modelId) is { } update)
                {
                    yield return update;
                }
            }
        }

        yield return new SpeechToTextResponseUpdate
        {
            Kind = SpeechToTextResponseUpdateKind.SessionClose,
            ResponseId = responseId,
            ModelId = modelId,
        };
    }

    /// <summary>Maps one official AWS streaming result to Microsoft.Extensions.AI.</summary>
    public static SpeechToTextResponseUpdate? CreateUpdate(
        Result result,
        string responseId,
        string? modelId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseId);

        var alternatives = result.Alternatives.Select(static alternative => new AwsTranscribeAlternative(
            alternative.Transcript ?? string.Empty,
            alternative.Items.Select(CreateItem).ToArray())).ToArray();
        if (alternatives.Length == 0)
        {
            return null;
        }

        var primary = alternatives[0];
        var startTimes = primary.Items
            .Where(static item => item.StartTime.HasValue)
            .Select(static item => item.StartTime!.Value)
            .ToArray();
        var endTimes = primary.Items
            .Where(static item => item.EndTime.HasValue)
            .Select(static item => item.EndTime!.Value)
            .ToArray();
        var properties = new AdditionalPropertiesDictionary
        {
            [AwsTranscribePropertyNames.Alternatives] = alternatives,
            [AwsTranscribePropertyNames.Items] = primary.Items,
        };
        AddIfNotEmpty(properties, AwsTranscribePropertyNames.ResultId, result.ResultId);
        AddIfNotEmpty(properties, AwsTranscribePropertyNames.ChannelId, result.ChannelId);
        AddIfNotEmpty(properties, AwsTranscribePropertyNames.LanguageCode, result.LanguageCode?.Value);

        var updateRange = NormalizeRange(
            result.StartTime.HasValue
                ? ToTimeSpan(result.StartTime)
                : startTimes.Length > 0 ? startTimes.Min() : null,
            result.EndTime.HasValue
                ? ToTimeSpan(result.EndTime)
                : endTimes.Length > 0 ? endTimes.Max() : null);

        return new SpeechToTextResponseUpdate(primary.Text)
        {
            Kind = result.IsPartial == true
                ? SpeechToTextResponseUpdateKind.TextUpdating
                : SpeechToTextResponseUpdateKind.TextUpdated,
            ResponseId = responseId,
            ModelId = modelId,
            StartTime = updateRange.Start,
            EndTime = updateRange.End,
            RawRepresentation = result,
            AdditionalProperties = properties,
        };
    }

    private static AwsTranscribeItem CreateItem(Item item)
    {
        var range = NormalizeRange(ToTimeSpan(item.StartTime), ToTimeSpan(item.EndTime));
        return new AwsTranscribeItem(
            item.Content ?? string.Empty,
            range.Start,
            range.End,
            item.Confidence,
            NullIfEmpty(item.Speaker),
            item.Stable,
            item.Type?.Value);
    }

    private static AdditionalPropertiesDictionary CreateSessionProperties(StartStreamTranscriptionResponse response)
    {
        var properties = new AdditionalPropertiesDictionary();
        AddIfNotEmpty(properties, AwsTranscribePropertyNames.SessionId, response.SessionId);
        AddIfNotEmpty(properties, AwsTranscribePropertyNames.RequestId, response.RequestId);
        AddIfNotEmpty(properties, AwsTranscribePropertyNames.LanguageCode, response.LanguageCode?.Value);
        return properties;
    }

    private static StartStreamTranscriptionRequest CreateRequest(
        Stream audioSpeechStream,
        SpeechToTextOptions? options,
        CancellationToken cancellationToken)
    {
        var request = new StartStreamTranscriptionRequest
        {
            LanguageCode = GetString(options, AwsTranscribePropertyNames.LanguageCode) ??
                options?.SpeechLanguage ?? DefaultLanguage,
            MediaEncoding = GetString(options, AwsTranscribePropertyNames.MediaEncoding) ?? MediaEncoding.Pcm.Value,
            MediaSampleRateHertz = GetInt32(
                options,
                AwsTranscribePropertyNames.SampleRateHertz,
                DefaultSampleRateHertz),
            EnablePartialResultsStabilization = true,
            PartialResultsStability = Amazon.TranscribeStreaming.PartialResultsStability.Medium,
        };
        if (!string.IsNullOrWhiteSpace(options?.ModelId) &&
            !string.Equals(options.ModelId, DefaultModelId, StringComparison.Ordinal))
        {
            request.LanguageModelName = options.ModelId;
        }

        if (options?.AdditionalProperties?.TryGetValue(
            AwsTranscribePropertyNames.ConfigureRequest,
            out var configureValue) == true && configureValue is Action<StartStreamTranscriptionRequest> configure)
        {
            configure(request);
        }
        if (request.IdentifyLanguage == true || request.IdentifyMultipleLanguages == true)
        {
            request.LanguageCode = null;
        }

        var chunkSize = GetInt32(options, AwsTranscribePropertyNames.ChunkSize, 8 * 1024);
        if (chunkSize is <= 0 or > 32 * 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "AWS Transcribe streaming chunks must be between 1 and 32768 bytes.");
        }

        var endOfStreamSent = false;
        request.AudioStreamPublisher = async () =>
        {
            if (endOfStreamSent)
            {
                return null!;
            }

            var buffer = new byte[chunkSize];
            var read = await audioSpeechStream
                .ReadAsync(buffer.AsMemory(0, chunkSize), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                // Transcribe requires one empty audio event to close the HTTP/2 input stream cleanly.
                // Returning null immediately leaves the session open until the service's 15-second idle timeout.
                endOfStreamSent = true;
                return new AudioEvent
                {
                    AudioChunk = new MemoryStream(Array.Empty<byte>(), writable: false),
                };
            }

            return new AudioEvent
            {
                AudioChunk = new MemoryStream(buffer, 0, read, writable: false, publiclyVisible: true),
            };
        };

        return request;
    }

    private static string GetModelId(SpeechToTextOptions? options) =>
        !string.IsNullOrWhiteSpace(options?.ModelId) ? options.ModelId : DefaultModelId;

    private static int GetInt32(SpeechToTextOptions? options, string key, int defaultValue)
    {
        if (options?.AdditionalProperties?.TryGetValue(key, out var value) != true)
        {
            return defaultValue;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue when longValue is >= int.MinValue and <= int.MaxValue => (int)longValue,
            _ => defaultValue,
        };
    }

    private static string? GetString(SpeechToTextOptions? options, string key) =>
        options?.AdditionalProperties?.TryGetValue(key, out var value) == true && value is string stringValue
            ? NullIfEmpty(stringValue)
            : null;

    private static void AddIfNotEmpty(AdditionalPropertiesDictionary properties, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            properties[key] = value;
        }
    }

    private static TimeSpan? ToTimeSpan(double? seconds) =>
        seconds is { } value && double.IsFinite(value) && value >= 0
            ? TimeSpan.FromSeconds(value)
            : null;

    private static (TimeSpan? Start, TimeSpan? End) NormalizeRange(TimeSpan? start, TimeSpan? end) =>
        start.HasValue && end.HasValue && end.Value < start.Value
            ? (start, start)
            : (start, end);

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static RegionEndpoint GetRegionEndpoint(string regionSystemName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionSystemName);
        return RegionEndpoint.GetBySystemName(regionSystemName);
    }
}
