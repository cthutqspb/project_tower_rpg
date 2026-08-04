namespace ProjectTowerRpg.Core.UI
{
    public static class UIEvents
    {
        public static event System.Action ToggleCharacterWindow;
        public static event System.Action CloseAllWindows;

        public static void TriggerToggleCharacterWindow()
        {
            ToggleCharacterWindow?.Invoke();
        }

        public static void TriggerCloseAllWindows()
        {
            CloseAllWindows?.Invoke();
        }
    }
}
