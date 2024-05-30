namespace ProxyWorld.wit.exports.wasi.http.v0_2_0;

/// Like .NET 9's `Task.WhenEach`, but implemented without using `ThreadPool`
class WhenEach {
    int remaining;
    TaskCompletionSource waiter;
    List<Task> completed = new();

    WhenEach(int remaining)
    {
        this.remaining = remaining;
    }

    public static IAsyncEnumerable<T> Iterate<T>(List<T> tasks) where T : Task
    {
        var whenEach = new WhenEach(tasks.Count);
        foreach (var task in tasks) {
            task.ContinueWith(task => {
                whenEach.completed.Add(task);
                if (whenEach.waiter != null) {
                    var waiter = whenEach.waiter;
                    whenEach.waiter = null;
                    waiter.SetResult();
                }
            });
        }
        return whenEach.Iterate<T>();
    }

    async IAsyncEnumerable<T> Iterate<T>() where T : Task
    {
        while (remaining > 0) {
            if (completed.Count > 0) {
                var task = completed[completed.Count - 1];
                completed.RemoveAt(completed.Count - 1);
                remaining -= 1;
                yield return (T) task;
            } else {
                var source = new TaskCompletionSource(TaskCreationOptions.AttachedToParent);
                waiter = source;
                await source.Task;
            }
        }
    }
}
