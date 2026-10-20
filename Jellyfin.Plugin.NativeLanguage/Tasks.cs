using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.NativeLanguage;

/// <summary>
/// Runs after every library scan.
/// </summary>
public class NativeTitlePostScanTask : ILibraryPostScanTask
{
    private readonly NativeTitleService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeTitlePostScanTask"/> class.
    /// </summary>
    /// <param name="service">Title service.</param>
    public NativeTitlePostScanTask(NativeTitleService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public Task Run(IProgress<double> progress, CancellationToken cancellationToken) =>
        _service.ProcessAllAsync(progress, cancellationToken);
}

/// <summary>
/// Manual run from Dashboard, Scheduled Tasks.
/// </summary>
public class NativeTitleScheduledTask : IScheduledTask
{
    private readonly NativeTitleService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeTitleScheduledTask"/> class.
    /// </summary>
    /// <param name="service">Title service.</param>
    public NativeTitleScheduledTask(NativeTitleService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public string Name => "Apply native-language titles";

    /// <inheritdoc />
    public string Key => "NativeLanguageApplyTitles";

    /// <inheritdoc />
    public string Description => "Gives movies and series in the chosen original languages their original title.";

    /// <inheritdoc />
    public string Category => "Native Language";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken) =>
        _service.ProcessAllAsync(progress, cancellationToken);

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();
}
