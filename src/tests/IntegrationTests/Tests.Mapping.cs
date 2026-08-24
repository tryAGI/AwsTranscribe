using Amazon;
using Amazon.Runtime;
using Amazon.TranscribeService;
using Amazon.TranscribeStreaming;
using Amazon.TranscribeStreaming.Model;
using Microsoft.Extensions.AI;

namespace AwsTranscribe.IntegrationTests;

public partial class Tests
{
    [TestMethod]
    public void Result_MapsTextTimingsAndProviderMetadata()
    {
        var result = new Result
        {
            ResultId = "result-id",
            IsPartial = false,
            ChannelId = "channel-0",
            LanguageCode = Amazon.TranscribeStreaming.LanguageCode.EnUS,
            StartTime = 0.1,
            EndTime = 0.9,
            Alternatives =
            [
                new Alternative
                {
                    Transcript = "hello world",
                    Items =
                    [
                        new Item
                        {
                            Content = "hello",
                            StartTime = 0.1,
                            EndTime = 0.45,
                            Confidence = 0.94,
                            Stable = true,
                            Type = ItemType.Pronunciation,
                        },
                        new Item
                        {
                            Content = "world",
                            StartTime = 0.5,
                            EndTime = 0.9,
                            Confidence = 0.98,
                            Speaker = "spk_0",
                            Type = ItemType.Pronunciation,
                        },
                    ],
                },
            ],
        };

        var update = AwsTranscribeClient.CreateUpdate(result, "session-id", "standard");

        update.Should().NotBeNull();
        update!.Kind.Should().Be(SpeechToTextResponseUpdateKind.TextUpdated);
        update.Text.Should().Be("hello world");
        update.StartTime.Should().Be(TimeSpan.FromMilliseconds(100));
        update.EndTime.Should().Be(TimeSpan.FromMilliseconds(900));
        update.RawRepresentation.Should().BeSameAs(result);
        update.AdditionalProperties![AwsTranscribePropertyNames.ResultId].Should().Be("result-id");
        update.AdditionalProperties[AwsTranscribePropertyNames.ChannelId].Should().Be("channel-0");

        var items = update.AdditionalProperties[AwsTranscribePropertyNames.Items]
            .Should().BeAssignableTo<IReadOnlyList<AwsTranscribeItem>>().Subject;
        items.Should().HaveCount(2);
        items[1].Speaker.Should().Be("spk_0");

        var response = AwsTranscribeClient.CreateResponse([update], "standard");
        response.Text.Should().Be("hello world");
        response.ModelId.Should().Be("standard");
        response.AdditionalProperties![AwsTranscribePropertyNames.Items]
            .Should().BeAssignableTo<IReadOnlyList<AwsTranscribeItem>>()
            .Subject.Should().HaveCount(2);

        result.IsPartial = true;
        AwsTranscribeClient.CreateUpdate(result, "session-id")!.Kind
            .Should().Be(SpeechToTextResponseUpdateKind.TextUpdating);
    }

    [TestMethod]
    public void CreateResponse_DeduplicatesRedeliveredFinalResultIdUsingLatestRepresentation()
    {
        var first = CreateFinalUpdate("result-1", "первая версия", 0.0, 1.0);
        var corrected = CreateFinalUpdate("result-1", "исправленная версия", 0.0, 1.1);

        var response = AwsTranscribeClient.CreateResponse([first, corrected], "standard");

        response.Text.Should().Be("исправленная версия");
        response.EndTime.Should().Be(TimeSpan.FromSeconds(1.1));
        response.AdditionalProperties![AwsTranscribePropertyNames.Results]
            .Should().BeAssignableTo<IReadOnlyList<Result>>()
            .Subject.Should().ContainSingle();
        response.AdditionalProperties[AwsTranscribePropertyNames.Items]
            .Should().BeAssignableTo<IReadOnlyList<AwsTranscribeItem>>()
            .Subject.Should().ContainSingle().Which.Text.Should().Be("исправленная версия");
    }

    [TestMethod]
    public void CreateResponse_PreservesRepeatedTextFromDistinctResultIds()
    {
        var first = CreateFinalUpdate("result-1", "да", 0.0, 0.2);
        var second = CreateFinalUpdate("result-2", "да", 0.3, 0.5);

        var response = AwsTranscribeClient.CreateResponse([first, second]);

        response.Text.Should().Be("да да");
        response.AdditionalProperties![AwsTranscribePropertyNames.Results]
            .Should().BeAssignableTo<IReadOnlyList<Result>>()
            .Subject.Should().HaveCount(2);
    }

    [TestMethod]
    public void Result_ClampsReversedProviderTimings()
    {
        var result = new Result
        {
            ResultId = "result-id",
            IsPartial = false,
            StartTime = 2.0,
            EndTime = 1.0,
            Alternatives =
            [
                new Alternative
                {
                    Transcript = "timing",
                    Items =
                    [
                        new Item
                        {
                            Content = "timing",
                            StartTime = 2.0,
                            EndTime = 1.0,
                            Type = ItemType.Pronunciation,
                        },
                    ],
                },
            ],
        };

        var update = AwsTranscribeClient.CreateUpdate(result, "session-id")!;

        update.StartTime.Should().Be(TimeSpan.FromSeconds(2));
        update.EndTime.Should().Be(update.StartTime);
        var item = update.AdditionalProperties![AwsTranscribePropertyNames.Items]
            .Should().BeAssignableTo<IReadOnlyList<AwsTranscribeItem>>()
            .Subject.Should().ContainSingle().Which;
        item.EndTime.Should().Be(item.StartTime);
    }

    private static SpeechToTextResponseUpdate CreateFinalUpdate(
        string resultId,
        string text,
        double startTime,
        double endTime)
    {
        var result = new Result
        {
            ResultId = resultId,
            IsPartial = false,
            StartTime = startTime,
            EndTime = endTime,
            Alternatives =
            [
                new Alternative
                {
                    Transcript = text,
                    Items =
                    [
                        new Item
                        {
                            Content = text,
                            StartTime = startTime,
                            EndTime = endTime,
                            Type = ItemType.Pronunciation,
                        },
                    ],
                },
            ],
        };

        return AwsTranscribeClient.CreateUpdate(result, "session-id")!;
    }

    [TestMethod]
    public void GetService_ExposesMetadataAndOfficialClients()
    {
        using var streamingClient = new AmazonTranscribeStreamingClient(
            new AnonymousAWSCredentials(),
            RegionEndpoint.USEast1);
        using var batchClient = new AmazonTranscribeServiceClient(
            new AnonymousAWSCredentials(),
            RegionEndpoint.USEast1);
        using var client = new AwsTranscribeClient(streamingClient, batchClient);

        client.GetService(typeof(SpeechToTextClientMetadata)).Should().BeOfType<SpeechToTextClientMetadata>();
        client.GetService(typeof(IAmazonTranscribeStreaming)).Should().BeSameAs(streamingClient);
        client.GetService(typeof(IAmazonTranscribeService)).Should().BeSameAs(batchClient);
        client.GetService(typeof(string)).Should().BeNull();
    }
}
