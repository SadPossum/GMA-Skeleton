namespace Gma.Modules.Auth.Domain.ValueObjects;

public readonly record struct MemberId
{
    public MemberId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Member id is required.", nameof(value));
        }

        this.Value = value;
    }

    public Guid Value { get; }
}
