namespace Roguelike.Core.Events
{
    public readonly struct RoomEntered
    {
        public readonly int RoomIndex;

        public RoomEntered(int roomIndex)
        {
            RoomIndex = roomIndex;
        }
    }
}
