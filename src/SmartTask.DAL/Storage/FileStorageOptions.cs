namespace SmartTask.DAL.Storage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string RootPath { get; set; } = string.Empty;
}
