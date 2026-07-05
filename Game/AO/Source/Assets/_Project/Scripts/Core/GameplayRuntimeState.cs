namespace AO.Core
{
    public static class GameplayRuntimeState
    {
        public static bool IsInputBlocked { get; private set; }

        public static void SetInputBlocked(bool blocked)
        {
            IsInputBlocked = blocked;
        }
    }
}
