namespace Host.Api;

using System.Reflection;

public sealed class ApiAssemblyReference
{
    public static Assembly Assembly => typeof(ApiAssemblyReference).Assembly;
}
