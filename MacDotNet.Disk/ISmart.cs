namespace MacDotNet.Disk;

public interface ISmart
{
    bool LastUpdate { get; }

    bool Update();
}
