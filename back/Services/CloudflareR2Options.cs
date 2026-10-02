namespace back.Services;

public class CloudflareR2Options
{
    public string AccountId { get; set; } = "";
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";
    public string BucketName { get; set; } = "";
    public string? Endpoint { get; set; }
}
