namespace Gma.Modules.Auth.Domain.ValueObjects;

public readonly record struct MemberSessionId
{
    public MemberSessionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Member session id is required.", nameof(value));
        }

        this.Value = value;
    }

    public Guid Value { get; }
}
