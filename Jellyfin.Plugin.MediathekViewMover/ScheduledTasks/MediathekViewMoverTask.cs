using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MediathekViewMover.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediathekViewMover.ScheduledTasks
{
    /// <summary>
    /// Ein ScheduledTask zur Verarbeitung von MediathekView-Dateien.
    /// </summary>
    public class MediathekViewMoverTask : IScheduledTask
    {
        private readonly ILogger<MediathekViewMoverTask> _logger;
        private readonly TaskProcessorService _taskProcessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediathekViewMoverTask"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{MediathekViewMoverTask}"/> interface.</param>
        /// <param name="taskProcessor">Service für die Verarbeitung von Tasks.</param>
        public MediathekViewMoverTask(
            ILogger<MediathekViewMoverTask> logger,
            TaskProcessorService taskProcessor)
        {
            _logger = logger;
            _taskProcessor = taskProcessor;
        }

        /// <inheritdoc/>
        public string Name => "MediathekView Mover";

        /// <inheritdoc/>
        public string Key => "MediathekViewMoverTask";

        /// <inheritdoc/>
        public string Description => "Verschiebt und organisiert Dateien aus MediathekView in die Jellyfin-Bibliothek";

        /// <inheritdoc/>
        public string Category => "MediathekView";

        /// <inheritdoc/>
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("MediathekView Mover Task - Start");
                var tasks = Plugin.Instance!.Configuration.MoverTasks;
                if (tasks.Count == 0)
                {
                    _logger.LogWarning("Keine MoverTasks in der Konfiguration gefunden");
                    return;
                }

                var totalTasks = tasks.Count;
                var progressPerTask = 100.0 / totalTasks;
                var currentTaskIndex = 0;

                foreach (var task in tasks)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    // Erstelle einen Progress Reporter für diese Task
                    var taskProgress = new Progress<double>(p =>
                    {
                        var baseProgress = currentTaskIndex * progressPerTask;
                        var taskContribution = (p / 100.0) * progressPerTask;
                        progress.Report(baseProgress + taskContribution);
                    });

                    await _taskProcessor.ProcessTaskAsync(task, cancellationToken, taskProgress).ConfigureAwait(false);
                    currentTaskIndex++;
                }

                _logger.LogInformation("MediathekView Mover Task - Abgeschlossen");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler während der Ausführung des MediathekView Mover Tasks");
                throw;
            }
        }

        /// <inheritdoc/>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return
            [
                new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerWeekly, DayOfWeek = DayOfWeek.Monday, TimeOfDayTicks = TimeSpan.FromHours(3).Ticks }
            ];
        }
    }
}
