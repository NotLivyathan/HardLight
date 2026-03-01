namespace Content.Shared.Body.Systems;

public sealed partial class SharedBodySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        InitializePartAppearances();
        InitializeIntegrityQueue();
    }
}
