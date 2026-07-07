namespace Gma.Modules.TaskRuntime.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Infrastructure;

internal sealed class TaskControlMessageStateConfiguration : IEntityTypeConfiguration<TaskControlMessageState>
{
    public void Configure(EntityTypeBuilder<TaskControlMessageState> builder)
    {
        builder.ToTable("task_control_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.CommandName).HasMaxLength(TaskControlMessageState.CommandNameMaxLength).IsRequired();
        builder.Property(message => message.Payload).HasMaxLength(TaskControlMessage.PayloadMaxLength).IsRequired();
        builder.Property(message => message.RequestedBy).HasMaxLength(TaskControlMessageState.RequestedByMaxLength);
        builder.Property(message => message.Status).HasConversion<int>().IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(TaskRun.ErrorMaxLength);
        builder.HasIndex(message => new { message.RunId, message.Status, message.EnqueuedAtUtc });
    }
}
