namespace AwsTranscribe.IntegrationTests;

[TestClass]
public partial class Tests
{
    private static AwsTranscribeClient GetAuthenticatedClient()
    {
        var region =
            Environment.GetEnvironmentVariable("AWS_REGION") is { Length: > 0 } regionValue
                ? regionValue
                : Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") is { Length: > 0 } defaultRegionValue
                    ? defaultRegionValue
                    : throw new AssertInconclusiveException(
                        "AWS_REGION or AWS_DEFAULT_REGION environment variable is not found.");

        return new AwsTranscribeClient(region);
    }
}
