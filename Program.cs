using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TodoistRecurrent
{
    class Program
    {
        private static ScheduledTask[] _dailyTasks = new[] {
            new ScheduledTask("Check Todoist before wrapping up", "today at 5pm", new TimeSpan(19, 0, 0), ImmutableArray.Create(DayOfWeek.Monday,
                DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday), 2217036134),
            new ScheduledTask("Rellenar botellas de agua", "today at 11pm", new TimeSpan(23, 0, 0)),
            new ScheduledTask("Ropa para lavar / colgar", "today at 11pm", new TimeSpan(23, 0, 0)),
            new ScheduledTask("Pasar rumba", "today at 11pm", new TimeSpan(23, 0, 0), ImmutableArray.Create(DayOfWeek.Tuesday, DayOfWeek.Sunday)),
            new ScheduledTask("Sacar jabón de la bañera", "today at 7pm", new TimeSpan(21, 0, 0), ImmutableArray.Create(DayOfWeek.Thursday)),
            new ScheduledTask("Dar vuelta la ensalada", "today at 8pm", new TimeSpan(22, 0, 0), ImmutableArray.Create(DayOfWeek.Thursday)),
            new ScheduledTask("Agua aires acondicionados ", "today at 8pm", new TimeSpan(22, 0, 0), ImmutableArray.Create(DayOfWeek.Thursday)),
        };

        static async Task Main(string[] args)
        {
            var utcNow = DateTime.UtcNow;
            var tasksToRun = _dailyTasks.Where(t =>
            {
                if (!t.Days.Contains(utcNow.DayOfWeek))
                {
                    return false;
                }

                // it's not an issue if we create it multiple times. Multiple requests will be ignored
                var diff = utcNow.TimeOfDay.Subtract(t.ScheduleAtUTC).TotalHours;
                if (diff < 0)
                {
                    diff += 24;
                }
                return diff <= 1;
            });

            var commands = tasksToRun.Select(t => new TodoistCommand<TodoistTask>()
            {
                Type = "item_add",
                UUID = t.GetId(utcNow),
                TempId = t.GetId(utcNow),
                Args = new TodoistTask()
                {
                    Content = t.Content,
                    ProjectId = t.ProjectId,
                    ResponsibleUid = 22636116,
                    Due = new TodoistDue()
                    {
                        String = t.Due
                    }
                }
            } as BaseCommand).Concat(tasksToRun.Select(t => new TodoistCommand<TodoistReminder>
            {
                Type = "reminder_add",
                UUID = t.GetId(utcNow) + "r",
                TempId = t.GetId(utcNow) + "r",
                Args = new TodoistReminder()
                {
                    ItemId = t.GetId(utcNow),
                    Type = "relative",
                    Service = "push",
                    MinuteOffset = 30
                }
            })).ToArray();

            if (commands.Length == 0)
            {
                return;
            }

            var dict = new Dictionary<string, string>()
            {
                ["token"] = Environment.GetEnvironmentVariable("TODOIST_TOKEN"),
                ["commands"] = Jil.JSON.SerializeDynamic(commands, Jil.Options.ExcludeNullsIncludeInheritedCamelCase)
            };

            var content = new FormUrlEncodedContent(dict);

            var client = new HttpClient();
            var response = await client.PostAsync("https://api.todoist.com/sync/v8/sync", content);
            var responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseBody);
            response.EnsureSuccessStatusCode();
        }
    }

    public class ScheduledTask
    {
        public string Content { get; private set; }
        public string Due { get; private set; }
        public ImmutableArray<DayOfWeek> Days { get; private set; }
        public TimeSpan ScheduleAtUTC { get; private set; }
        public long ProjectId { get; set; }

        private static readonly ImmutableArray<DayOfWeek> _everyDay = ImmutableArray.Create(DayOfWeek.Sunday, DayOfWeek.Monday,
            DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday);

        public ScheduledTask(string content, string due, TimeSpan scheduleAtUTC) : this(content, due, scheduleAtUTC, _everyDay) { }

        public ScheduledTask(string content, string due, TimeSpan scheduleAtUTC, ImmutableArray<DayOfWeek> days, long? projectId = null)
        {
            Content = content;
            Due = due;
            ScheduleAtUTC = scheduleAtUTC;
            Days = days;
            ProjectId = projectId ?? 2210411112;
        }

        public string GetId(DateTime utcNow)
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(Content));
                var sb = new StringBuilder(hash.Length * 2 + 8);

                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }

                sb.Append(utcNow.ToString("yyyyMMdd"));
                return sb.ToString();
            }
        }
    }

    public class BaseCommand { }

    public class TodoistCommand<T> : BaseCommand
    {
        public string Type { get; set; }
        public string UUID { get; set; }
        [DataMember(Name = "temp_id")]
        public string TempId { get; set; }
        public T Args { get; set; }
    }

    public class TodoistTask
    {
        public string Content { get; set; }
        [DataMember(Name = "project_id")]
        public long? ProjectId { get; set; }
        public TodoistDue Due { get; set; }
        [DataMember(Name = "responsible_uid")]
        public long? ResponsibleUid { get; set; }
    }

    public class TodoistReminder
    {
        [DataMember(Name = "item_id")]
        public string ItemId { get; set; }
        public string Type { get; set; }
        public string Service { get; set; }
        [DataMember(Name = "minute_offset")]
        public int MinuteOffset { get; set; }
    }

    public class TodoistDue
    {
        public string String { get; set; }
    }
}
