namespace SmartCommander.Services
{
    // Backend-agnostic listing options; each provider translates them to whatever
    // its backend understands.
    public sealed record DirectoryListingFilter(bool IncludeHidden = false, bool Recursive = false);
}
