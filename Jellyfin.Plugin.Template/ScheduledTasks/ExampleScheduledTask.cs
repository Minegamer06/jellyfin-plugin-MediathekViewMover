using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Template.LibaryExamples;
using Jellyfin.Plugin.Template.UserExamples;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Template.ScheduledTasks
{
    /// <inheritdoc/>
    public class ExampleScheduledTask : IScheduledTask
    {
        private readonly ILogger<ExampleScheduledTask> _logger;
        private readonly LibaryInfo _libaryInfo;
        private readonly UserInfo _userInfo;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExampleScheduledTask"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{ExampleScheduledTask}"/> interface.</param>
        /// <param name="libaryInfo">Instance of the <see cref="LibaryInfo"/> interface.</param>
        /// <param name="userInfo">Instance of the <see cref="UserInfo"/> interface.</param>
        public ExampleScheduledTask(ILogger<ExampleScheduledTask> logger, LibaryInfo libaryInfo, UserInfo userInfo)
        {
            _logger = logger;
            _libaryInfo = libaryInfo;
            _userInfo = userInfo;
        }

        /// <inheritdoc/>
        public string Name => "ExampleTask";

        /// <inheritdoc/>
        public string Key => "TemplateExampleTask";

        /// <inheritdoc/>
        public string Description => "A Description what the Tasks is doing";

        /// <inheritdoc/>
        public string Category => "Template Plugin";

        /// <inheritdoc/>
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            // Forc Execute using:
            //  _taskManager.Execute<ExampleScheduledTask>();
            _logger.LogInformation("Task - Start: {Name}", Name);
            await _libaryInfo.Run().ConfigureAwait(false);
            await _userInfo.Run().ConfigureAwait(false);
            _logger.LogInformation("Task - Complete: {Name}", Name);
        }

        /// <inheritdoc/>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return [new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerStartup
                },
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(6).Ticks
                }
                ];
        }
    }
}
