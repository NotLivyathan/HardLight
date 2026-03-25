using System.Numerics;
using Content.Shared._HL.Traits.Physical;

namespace Content.Shared.Floof.Silicons.Borgs;

/// <summary>
/// Configurable sprite offsets for quadborg chassis by size trait.
/// </summary>
[RegisterComponent]
public sealed partial class QuadborgSpriteOffsetComponent : Component
{
	/// <summary>
	/// Fallback offset when no explicit size trait applies.
	/// </summary>
	[DataField]
	public System.Numerics.Vector2 DefaultOffset = System.Numerics.Vector2.Zero;

	/// <summary>
	/// Offset used when the entity has <see cref="BigWeaponHandlingComponent"/>.
	/// </summary>
	[DataField]
	public System.Numerics.Vector2 BigOffset = new(0f, 0.3f);

	/// <summary>
	/// Offset used when the entity has <see cref="SmallWeaponHandlingComponent"/>.
	/// </summary>
	[DataField]
	public System.Numerics.Vector2 SmallOffset = System.Numerics.Vector2.Zero;

	/// <summary>
	/// Offset used when the entity has <see cref="TinyWeaponHandlingComponent"/>.
	/// </summary>
	[DataField]
	public System.Numerics.Vector2 TinyOffset = System.Numerics.Vector2.Zero;
}
