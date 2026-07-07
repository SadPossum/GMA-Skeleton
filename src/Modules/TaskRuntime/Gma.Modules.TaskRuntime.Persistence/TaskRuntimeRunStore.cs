namespace Gma.Modules.TaskRuntime.Persistence;

using Gma.Framework.Runtime.Time;
using Gma.Framework.Tasks.Infrastructure;

internal sealed class TaskRuntimeRunStore(TaskRuntimeDbContext dbContext, ISystemClock clock)
    : EfTaskRunStore<TaskRuntimeDbContext>(dbContext, clock);
