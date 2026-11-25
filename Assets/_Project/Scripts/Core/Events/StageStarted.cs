namespace Roguelike.Core.Events
{
    public readonly struct StageStarted
    {
        public readonly int StageIndex;

        public StageStarted(int stageIndex)
        {
            StageIndex = stageIndex;
        }
    }
}
