namespace Roguelike.Core.Events
{
    public readonly struct RunEnded
    {
        public readonly bool Victory;

        public RunEnded(bool victory)
        {
            Victory = victory;
        }
    }
}
