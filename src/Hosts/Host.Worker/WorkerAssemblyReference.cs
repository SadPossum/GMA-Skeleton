namespace Host.Worker;

using System.Reflection;

public static class WorkerAssemblyReference
{
    public static Assembly Assembly => typeof(WorkerAssemblyReference).Assembly;
}
