using Microsoft.Extensions.Options;
using SmartTask.DAL.Storage;

namespace SmartTask.UnitTests;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "SmartTaskTests",
        Guid.NewGuid().ToString("N"));

    private LocalFileStorageService CreateService()
    {
        return new LocalFileStorageService(
            Options.Create(new FileStorageOptions
            {
                RootPath = _rootPath
            }));
    }

    [Fact]
    public async Task SaveAndOpenReadAsync_ShouldStoreAndReturnFileContents()
    {
        var service = CreateService();
        var storedFileName = $"{Guid.NewGuid():N}.pdf";
        var expected = "%PDF-test"u8.ToArray();

        await using var input = new MemoryStream(expected);

        await service.SaveAsync(input, storedFileName);

        await using var result = await service.OpenReadAsync(storedFileName);

        Assert.NotNull(result);

        using var output = new MemoryStream();
        await result.CopyToAsync(output);

        Assert.Equal(expected, output.ToArray());
    }

    [Fact]
    public async Task OpenReadAsync_ShouldReturnNullWhenFileDoesNotExist()
    {
        var service = CreateService();

        var result = await service.OpenReadAsync(
            $"{Guid.NewGuid():N}.pdf");

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveStoredFile()
    {
        var service = CreateService();
        var storedFileName = $"{Guid.NewGuid():N}.txt";

        await using var input = new MemoryStream("test"u8.ToArray());
        await service.SaveAsync(input, storedFileName);

        await service.DeleteAsync(storedFileName);

        Assert.Null(await service.OpenReadAsync(storedFileName));
    }

    [Theory]
    [InlineData("../outside.pdf")]
    [InlineData("../../outside.pdf")]
    [InlineData("..\\\\outside.pdf")]
    [InlineData("/outside.pdf")]
    [InlineData("random-file.pdf")]
    [InlineData("00000000000000000000000000000000.exe")]
    [InlineData("00000000000000000000000000000000.jpeg/../outside.pdf")]
    public async Task SaveAsync_ShouldRejectInvalidStoredFileName(
        string storedFileName)
    {
        var service = CreateService();

        await using var input = new MemoryStream("test"u8.ToArray());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SaveAsync(input, storedFileName));
    }

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".txt")]
    public async Task SaveAsync_ShouldAcceptSupportedExtensions(string extension)
    {
        var service = CreateService();
        var storedFileName = $"{Guid.NewGuid():N}{extension}";

        await using var input = new MemoryStream("test"u8.ToArray());

        await service.SaveAsync(input, storedFileName);

        Assert.True(File.Exists(Path.Combine(_rootPath, storedFileName)));
    }

    [Fact]
    public void Constructor_ShouldRejectMissingRootPath()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new LocalFileStorageService(
                Options.Create(new FileStorageOptions
                {
                    RootPath = string.Empty
                })));
    }

[Fact]
public async Task SaveAsync_WhenWriteIsCancelled_ShouldRemovePartialFile()
{
    var service = CreateService();
    var storedFileName = $"{Guid.NewGuid():N}.pdf";
    var filePath = Path.Combine(_rootPath, storedFileName);

    await using var input = new MemoryStream(
        "%PDF-test-content"u8.ToArray());

    using var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(
        () => service.SaveAsync(
            input,
            storedFileName,
            cancellationTokenSource.Token));

    Assert.False(File.Exists(filePath));
}

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
