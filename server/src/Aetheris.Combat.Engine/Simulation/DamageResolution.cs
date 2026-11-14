namespace Aetheris.Combat.Engine;

/// <summary>How one salvo passes a shield, then armor, then the hull.</summary>
/// <param name="ShieldNow">The saturation left.</param>
/// <param name="Spent">How much saturation the shield spent.</param>
/// <param name="Passed">How much of the salvo passed the shield.</param>
/// <param name="Armor">The armor left.</param>
/// <param name="Hull">The hull left.</param>
/// <param name="ModuleIntegrity">The integrity of the module that was struck.</param>
/// <param name="ShieldDown">Whether the saturation crossed from above zero to zero.</param>
/// <param name="ModuleDestroyed">Whether the struck module reached zero integrity.</param>
/// <param name="ShipDestroyed">Whether the hull reached zero.</param>
public readonly record struct DamageHit(
    double ShieldNow,
    double Spent,
    double Passed,
    double Armor,
    double Hull,
    double ModuleIntegrity,
    bool ShieldDown,
    bool ModuleDestroyed,
    bool ShipDestroyed);

/// <summary>
/// The shield spends <c>min(salvo × block, saturation)</c> and the rest of the salvo passes.
/// Armor then takes its share of what passed, and whatever armor does not take reaches the hull.
/// The module is struck for the hull loss times the module share.
/// </summary>
public static class DamageResolution
{
    /// <summary>
    /// Applies one salvo.
    /// </summary>
    /// <param name="salvo">The damage of the hit.</param>
    /// <param name="block">The block of the shield for this kind. Zero lets the salvo through.</param>
    /// <param name="shieldNow">The saturation before the hit.</param>
    /// <param name="armor">The armor before the hit.</param>
    /// <param name="armorShare">The share of what passed the shield that armor tries to take.</param>
    /// <param name="hull">The hull before the hit.</param>
    /// <param name="moduleIntegrity">The integrity of the struck module.</param>
    /// <param name="moduleDamageShare">The share of the hull loss that strikes the module.</param>
    /// <returns>The layers after the hit.</returns>
    public static DamageHit Apply(
        double salvo,
        double block,
        double shieldNow,
        double armor,
        double armorShare,
        double hull,
        double moduleIntegrity,
        double moduleDamageShare)
    {
        if (salvo < 0)
        {
            salvo = 0;
        }

        if (block < 0)
        {
            block = 0;
        }

        if (shieldNow < 0)
        {
            shieldNow = 0;
        }

        if (armor < 0)
        {
            armor = 0;
        }

        if (hull < 0)
        {
            hull = 0;
        }

        double wanted = salvo * block;
        double spent = wanted < shieldNow ? wanted : shieldNow;
        double passed = salvo - spent;

        if (passed < 0)
        {
            passed = 0;
        }

        double shieldLeft = shieldNow - spent;
        bool shieldDown = shieldNow > 0 && shieldLeft <= 0;
        double asked = passed * (armorShare < 0 ? 0 : armorShare);
        double taken = asked < armor ? asked : armor;
        double hullLost = passed - taken;
        double armorLeft = armor - taken;
        double hullLeft = hull - hullLost;
        double moduleLoss = hullLost * (moduleDamageShare < 0 ? 0 : moduleDamageShare);
        double integrityLeft = moduleIntegrity - moduleLoss;

        return new DamageHit(
            shieldLeft,
            spent,
            passed,
            armorLeft,
            hullLeft,
            integrityLeft,
            shieldDown,
            moduleIntegrity > 0 && integrityLeft <= 0,
            hull > 0 && hullLeft <= 0);
    }
}
