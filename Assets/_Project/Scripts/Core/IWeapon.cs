namespace Roguelike.Core
{
    public interface IWeapon
    {
        int CurrentAmmo { get; }
        int MaxAmmo { get; }
        bool IsReloading { get; }

        void TryFire();
        void Reload();
    }
}
