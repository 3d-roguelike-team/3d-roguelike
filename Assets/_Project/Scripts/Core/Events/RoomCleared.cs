namespace Roguelike.Core.Events
{
    public readonly struct RoomCleared
    {
        public readonly int RoomIndex;

        public RoomCleared(int roomIndex)
        {
            RoomIndex = roomIndex;
        }
    }
}
