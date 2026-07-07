namespace Gma.Modules.TaskRuntime.Application;

using Gma.Framework.Results;

public static class TaskRuntimeApplicationErrors
{
    public static readonly Error RunNotFound = new("TaskRuntime.RunNotFound", "The task run was not found.");
    public static readonly Error InvalidPayloadJson = new("TaskRuntime.InvalidPayloadJson", "The task payload must be valid JSON.");
    public static readonly Error InvalidRunId = new("TaskRuntime.InvalidRunId", "The task run id must not be empty.");
    public static readonly Error InvalidStatus = new("TaskRuntime.InvalidStatus", "The task run status is not supported.");
    public static readonly Error PayloadRequired = new("TaskRuntime.PayloadRequired", "Provide either --payload-json or --payload-file.");
    public static readonly Error PayloadSourceConflict = new("TaskRuntime.PayloadSourceConflict", "Use either --payload-json or --payload-file, not both.");
    public static readonly Error PayloadFileNotFound = new("TaskRuntime.PayloadFileNotFound", "The task payload file was not found.");
    public static readonly Error RunCannotBeCanceled = new("TaskRuntime.RunCannotBeCanceled", "The task run cannot be canceled from its current status.");
    public static readonly Error RunCannotBeRetried = new("TaskRuntime.RunCannotBeRetried", "The task run cannot be retried from its current status.");
    public static readonly Error RunCannotBeControlled = new("TaskRuntime.RunCannotBeControlled", "The task run cannot receive control messages from its current status.");
    public static readonly Error InvalidControlMessage = new("TaskRuntime.InvalidControlMessage", "The task control message is invalid.");
}
