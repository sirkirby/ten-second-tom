using System.Threading;
using System.Threading.Tasks;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Shared.Abstractions.Templates;

/// <summary>
/// Abstraction for installing bundled prompt templates without introducing
/// cross-layer dependencies. Implemented by the Templates feature.
/// </summary>
public interface ITemplateInstaller
{
    /// <summary>
    /// Installs the default bundled templates into the provided directory.
    /// </summary>
    /// <param name="targetDirectory">Absolute path to the templates folder.</param>
    /// <param name="overwriteExisting">Whether any existing files should be overwritten.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result describing installation statistics.</returns>
    Task<Result<TemplateInstallationResult>> InstallDefaultsAsync(
        string targetDirectory,
        bool overwriteExisting,
        CancellationToken cancellationToken);
}
