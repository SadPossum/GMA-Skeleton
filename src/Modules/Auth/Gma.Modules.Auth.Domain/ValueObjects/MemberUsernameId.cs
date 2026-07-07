namespace Gma.Modules.Auth.Domain.ValueObjects;

public readonly record struct MemberUsernameId
{
    public MemberUsernameId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Member username id is required.", nameof(value));
        }

        this.Value = value;
    }

    public Guid Value { get; }
}
