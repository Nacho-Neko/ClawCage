using ClawCage.WinUI.Model.ScheduledTasks;
using ClawCage.WinUI.Services.Cron;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ClawCage.WinUI.Components.ScheduledTasks
{
    internal static class ScheduledTaskActions
    {
        internal static async Task<bool> ToggleEnabledAsync(CronConfigService cronService, List<CronJobConfig> jobs, string jobId, bool isOn)
        {
            var job = jobs.FirstOrDefault(j => j.Id == jobId);
            if (job is null || job.Enabled == isOn) return false;

            job.Enabled = isOn;
            job.UpdatedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return await cronService.SaveJobsAsync(jobs);
        }

        internal static async Task<bool> EditJobAsync(XamlRoot xamlRoot, CronConfigService cronService, List<CronJobConfig> jobs, string jobId)
        {
            var job = jobs.FirstOrDefault(j => j.Id == jobId);
            if (job is null) return false;

            var edited = await AddScheduledTaskDialog.ShowEditAsync(xamlRoot, job);
            if (edited is null) return false;

            var idx = jobs.FindIndex(j => j.Id == jobId);
            if (idx >= 0) jobs[idx] = edited;

            return await cronService.SaveJobsAsync(jobs);
        }

        internal static async Task<bool> DeleteJobAsync(XamlRoot xamlRoot, CronConfigService cronService, List<CronJobConfig> jobs, string jobId)
        {
            var dialog = new ContentDialog
            {
                Title = "确认删除",
                Content = "删除后无法恢复，是否继续？",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = xamlRoot
            };

            var tcs = new TaskCompletionSource<ContentDialogResult>();
            var op = dialog.ShowAsync();
            op.Completed = (o, s) =>
            {
                tcs.TrySetResult(s == AsyncStatus.Completed ? o.GetResults() : ContentDialogResult.None);
            };
            if (await tcs.Task != ContentDialogResult.Primary) return false;

            jobs.RemoveAll(j => j.Id == jobId);
            return await cronService.SaveJobsAsync(jobs);
        }
    }
}
