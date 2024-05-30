using ProxyWorld.wit.imports.wasi.io.v0_2_0;

namespace ProxyWorld.wit.exports.wasi.http.v0_2_0;

class PollTaskScheduler : TaskScheduler {
    internal static PollTaskScheduler Instance = new();
    internal static TaskFactory Factory = new(Instance);
    List<(IPoll.Pollable, TaskCompletionSource)> pollables = new();
    List<Task> tasks = new();

    internal Task Register(IPoll.Pollable pollable)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.AttachedToParent);
        pollables.Add((pollable, source));
        return source.Task;
    }

    internal void Run()
    {
        while (this.tasks.Count > 0) {
                Console.WriteLine($"task count: {this.tasks.Count}");                
            var tasks = this.tasks;
            this.tasks = new();
            foreach (var task in tasks) {
                Console.WriteLine("try execute");
                base.TryExecuteTask(task);
                Console.WriteLine("try execute complete");                
                if (!task.IsCompleted) {
                    this.tasks.Add(task);
                }
            }
            
                Console.WriteLine($"pollable count: {this.pollables.Count}");    
            if (this.pollables.Count > 0) {
                var pollables = this.pollables;
                this.pollables = new();
                var arguments = new List<IPoll.Pollable>();
                var sources = new List<TaskCompletionSource>();
                foreach ((var pollable, var source) in pollables) {
                    arguments.Add(pollable);
                    sources.Add(source);
                }
                var results = PollInterop.Poll(arguments);
                var ready = new bool[arguments.Count];
                foreach (var result in results) {
                    ready[result] = true;
                    arguments[(int) result].Dispose();
                    sources[(int) result].SetResult();
                }
                for (var i = 0; i < arguments.Count; ++i) {
                    if (!ready[i]) {
                        this.pollables.Add((arguments[i], sources[i]));
                    }
                }
            }
        }
    }

    protected override void QueueTask(Task task)
    {
        tasks.Add(task);
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        // TODO
        return false;
    }

    protected override IEnumerable<Task> GetScheduledTasks()
    {
        // TODO
        return null;
    }
}
