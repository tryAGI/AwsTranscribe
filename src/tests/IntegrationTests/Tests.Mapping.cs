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
