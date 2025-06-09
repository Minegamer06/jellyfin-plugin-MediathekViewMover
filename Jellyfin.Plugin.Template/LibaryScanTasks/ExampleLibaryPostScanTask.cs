using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Jellyfin.Plugin.Template.ScheduledTasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Template.LibaryScanTasks
{
    /// <inheritdoc/>
    public class ExampleLibaryPostScanTask : ILibraryPostScanTask
    {
        private readonly ITaskManager _taskManager;
        private readonly ILogger<ExampleLibaryPostScanTask> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleLibaryPostScanTask"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{ExampleLibaryPostScanTask}"/> interface.</param>
        /// <param name="taskManager">Instance of the <see cref="ITaskManager"/> interface.</param>
        public ExampleLibaryPostScanTask(ILogger<ExampleLibaryPostScanTask> logger, ITaskManager taskManager)
        {
            _logger = logger;
            _taskManager = taskManager;
        }

        /// <inheritdoc/>
        public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Task - Start: {Name}", nameof(ExampleLibaryPostScanTask));
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            if (Plugin.Instance?.Configuration.TrueFalseSetting == true)
            {
                _taskManager.Execute<ExampleScheduledTask>();
            }

            _logger.LogInformation("Task - Complete: {Name}", nameof(ExampleLibaryPostScanTask));
        }
    }
}
