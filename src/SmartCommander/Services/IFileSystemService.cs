using System.Threading;
using System.Threading.Tasks;

namespace SmartCommander.Services
{
    // The single service ViewModels depend on. Extends IFileSystemProvider with FTP connection
    // management, a composite-level concern since no per-backend provider owns "which connection
    // is active". The implementing composite (FileSystemService) routes each call to the
    // provider that owns the path, so ViewModels stay unaware of which backend a path belongs to.
    public interface IFileSystemService : IFileSystemProvider
    {
        bool IsFtpConnected { get; }
        string? FtpHost { get; }
        Task ConnectFtpAsync(string host, int port, string username, string password, bool anonymous, CancellationToken ct);
        Task DisconnectFtpAsync();
    }
}
