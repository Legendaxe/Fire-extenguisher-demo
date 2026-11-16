namespace FireExtinguisher.Rocket
{
    /// <summary>
    /// Optional gate for <see cref="FunnyImpactEffects"/>. While <see cref="ImpactAllowed"/> is
    /// false the prop still collides normally, but it produces no impact burst, sound or camera
    /// shake. Lets a prop decide for itself when its impacts are worth reacting to.
    /// </summary>
    public interface IImpactGate
    {
        bool ImpactAllowed { get; }
    }
}
