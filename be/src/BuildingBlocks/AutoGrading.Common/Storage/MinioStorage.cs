using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace AutoGrading.Common.Storage;

public interface IObjectStorage
{
    Task<string> UploadAsync(string objectName, Stream data, string contentType, CancellationToken cancellationToken = default);

    Task<Stream> DownloadAsync(string objectName, CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectName, CancellationToken cancellationToken = default);

    Task<string> GetPresignedUrlAsync(string objectName, int expirySeconds = 3600, CancellationToken cancellationToken = default);
}

/// <summary>S3-compatible object storage wrapper backed by MinIO, used for submission files (.docx/.drawio) and rubrics.</summary>
public sealed class MinioStorage : IObjectStorage
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;
    private bool _bucketEnsured;

    public MinioStorage(IOptions<MinioOptions> options)
    {
        _options = options.Value;

        var builder = new MinioClient()
            .WithEndpoint(_options.Endpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey);

        if (_options.UseSsl)
        {
            builder = builder.WithSSL();
        }

        _client = builder.Build();
    }

    public async Task<string> UploadAsync(string objectName, Stream data, string contentType, CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);

        var putArgs = new PutObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectName)
            .WithStreamData(data)
            .WithObjectSize(data.Length)
            .WithContentType(contentType);

        await _client.PutObjectAsync(putArgs, cancellationToken);

        return objectName;
    }

    public async Task<Stream> DownloadAsync(string objectName, CancellationToken cancellationToken = default)
    {
        var memoryStream = new MemoryStream();

        var getArgs = new GetObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectName)
            .WithCallbackStream(async (stream, ct) => await stream.CopyToAsync(memoryStream, ct));

        await _client.GetObjectAsync(getArgs, cancellationToken);
        memoryStream.Position = 0;

        return memoryStream;
    }

    public async Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
    {
        var removeArgs = new RemoveObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectName);

        await _client.RemoveObjectAsync(removeArgs, cancellationToken);
    }

    public async Task<string> GetPresignedUrlAsync(string objectName, int expirySeconds = 3600, CancellationToken cancellationToken = default)
    {
        var presignedArgs = new PresignedGetObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectName)
            .WithExpiry(expirySeconds);

        return await _client.PresignedGetObjectAsync(presignedArgs);
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketEnsured)
        {
            return;
        }

        var existsArgs = new BucketExistsArgs().WithBucket(_options.BucketName);
        var exists = await _client.BucketExistsAsync(existsArgs, cancellationToken);
        if (!exists)
        {
            var makeArgs = new MakeBucketArgs().WithBucket(_options.BucketName);
            await _client.MakeBucketAsync(makeArgs, cancellationToken);
        }

        _bucketEnsured = true;
    }
}
