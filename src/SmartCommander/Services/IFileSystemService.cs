namespace SmartCommander.Services
{
    // The single service ViewModels depend on. Same contract as IFileSystemProvider; the
    // implementing composite (FileSystemService) routes each call to the provider that
    // owns the path, so ViewModels stay unaware of which backend a path belongs to.
    public interface IFileSystemService : IFileSystemProvider
    {
    }
}
