using ClawCage.WinUI.Model.ScheduledTasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Services.Cron
{
    public class CronConfigService
    {
        private static readonly string CronDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw", "cron");
        private static readonly string JobsFile = Path.Combine(CronDir, "jobs.json");
        private static readonly string RunsDir = Path.Combine(CronDir, "runs");

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<(JsonObject Root, List<CronJobConfig> Jobs)?> LoadJobsAsync()
        {
            if (!File.Exists(JobsFile))
                return null;

            var json = await File.ReadAllTextAsync(JobsFile, Encoding.UTF8);
            var root = JsonNode.Parse(json) as JsonObject;
            if (root is null)
                return null;

            var jobs = new List<CronJobConfig>();
            if (root["jobs"] is JsonArray jobsArray)
            {
                foreach (var node in jobsArray)
                {
                    if (node is null) continue;
                    var job = node.Deserialize<CronJobConfig>(ReadOptions);
                    if (job is not null)
                        jobs.Add(job);
                }
            }

            return (root, jobs);
        }

        public async Task<bool> SaveJobsAsync(List<CronJobConfig> jobs)
        {
            Directory.CreateDirectory(CronDir);
            var root = new JsonObject
            {
                ["version"] = 1,
                ["jobs"] = JsonSerializer.SerializeToNode(jobs, WriteOptions)
            };
            var json = root.ToJsonString(WriteOptions);
            await File.WriteAllTextAsync(JobsFile, json, Encoding.UTF8);
            return true;
        }

        public async Task<List<CronJobRunRecord>> LoadRunRecordsAsync(string jobId)
        {
            var file = Path.Combine(RunsDir, jobId + ".jsonl");
            var records = new List<CronJobRunRecord>();
            if (!File.Exists(file))
                return records;
            using var stream = File.OpenRead(file);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var rec = JsonSerializer.Deserialize<CronJobRunRecord>(line, ReadOptions);
                    if (rec != null)
                        records.Add(rec);
                }
                catch { }
            }
            return records;
        }
    }
}
