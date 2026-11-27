namespace VisitorCalendar
{
    internal static class CalendarShortcut
    {
        internal const string OpenAction = "Game/Calendar";
        internal const string KeyboardBackAction = "Overlay/Calendar";
        internal const string PreviousAction = "Overlay/Left";
        internal const string NextAction = "Overlay/Right";

        // GameInputManager routes this through the native day button. In cheat
        // mode that same button advances time instead of opening the calendar.
        internal static bool CanAdvertise(bool dayButtonEnabled, bool worldInteractable, bool cheats, bool leftControl) =>
            dayButtonEnabled && worldInteractable && !cheats && !leftControl;
    }
}
