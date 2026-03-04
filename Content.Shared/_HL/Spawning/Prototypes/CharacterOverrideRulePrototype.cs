using Robust.Shared.Prototypes;
using Content.Shared.Polymorph;

namespace Content.Shared._HL.Spawning.Prototypes;

[Prototype("spawnCharacterOverrideRule")]
[Serializable]
public sealed partial class CharacterOverrideRulePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Match { get; private set; } = string.Empty;

    [DataField]
    public bool LoginCaseSensitive { get; private set; }

    [DataField]
    public bool CheckProfileName { get; private set; } = true;

    [DataField]
    public bool CheckEntityName { get; private set; } = true;

    [DataField]
    public ComponentRegistry Components { get; private set; } = new();

    [DataField]
    public bool ReplaceExisting { get; private set; }

    [DataField]
    public EntProtoId? Entity { get; private set; }

    [DataField]
    public bool TransferDamage { get; private set; } = true;

    [DataField]
    public bool TransferName { get; private set; }

    [DataField]
    public bool TransferHumanoidAppearance { get; private set; }

    [DataField]
    public PolymorphInventoryChange Inventory { get; private set; } = PolymorphInventoryChange.None;

    [DataField("logins")]
    public List<string> Logins { get; private set; } = new();

    [DataField("userIds")]
    public List<string> UserIds { get; private set; } = new();
}
