namespace Glance.Network;

public sealed record NetworkAdapterInfo(string Id,
    string Name,
    string ConnectionName,
    string IpAddress,
    string LinkSpeed)
{
    public string DetailText => string.Join(" · ", new[] { ConnectionName, IpAddress, LinkSpeed }
        .Where(value => !string.IsNullOrWhiteSpace(value)));
}
