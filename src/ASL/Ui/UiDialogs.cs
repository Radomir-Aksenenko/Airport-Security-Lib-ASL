using System;
using ASL.Api;

namespace ASL
{
    /// <summary>
    /// Modal Confirm / Prompt / Alert dialogs, built from the same toolkit windows (dialog layer, dim
    /// backdrop that blocks clicks, no close button). Enter confirms, Escape cancels; exactly one of the
    /// callbacks runs, and the window is destroyed afterwards.
    /// </summary>
    internal static class UiDialogs
    {
        private static int _counter;

        private static UiWindow Make(UiOwner owner, UiTheme theme, string title, float width)
        {
            var opt = new UiWindowOptions
            {
                Width = width, Anchor = UiAnchor.Center, Modal = true, Draggable = false, Closable = false,
                CaptureCursor = true, BlockGameInput = true, ShowTitleBar = true, Scrollable = false,
                RememberPosition = false, CloseOnEscape = true,
            };
            string id = "__dialog" + (++_counter);
            var w = UiWindow.Create(owner, owner.ModId + "/" + id, id, title, opt, theme, false, true);
            owner.Dialogs.Add(w);   // tracked so ModUi.ReleaseAll can destroy it (without callbacks) on mod unload / reload
            return w;
        }

        // Long messages go into a scroll view (the buttons below stay on screen); short ones stay a plain label.
        private static void AddMessage(UiWindow w, string message)
        {
            message = message ?? string.Empty;
            int lines = 1;
            for (int i = 0; i < message.Length; i++) if (message[i] == '\n') lines++;
            if (message.Length > 360 || lines > 8) w.AddScroll(320f).AddLabel(message);
            else w.AddLabel(message);
        }

        private static void Finish(UiWindow w, UiOwner owner, ref bool done, Action cb)
        {
            if (done) return;
            done = true;
            UiRuntime.DestroyWindow(w);
            owner.Fire(cb, "dialog callback");
        }

        public static void Confirm(UiOwner owner, UiTheme theme, string title, string message, Action onYes, Action onNo, string yesText, string noText)
        {
            var w = Make(owner, theme, title, 520f);
            bool done = false;
            AddMessage(w, message);
            w.AddSpacer(6f);
            var row = w.AddRow(12f);
            row.AddSpacer(0f).SetFlexible(1f);
            var no = row.AddButton(noText ?? "No", null, UiButtonStyle.Secondary);
            var yes = row.AddButton(yesText ?? "Yes", null, UiButtonStyle.Primary);
            no.Clicked += () => Finish(w, owner, ref done, onNo);
            yes.Clicked += () => Finish(w, owner, ref done, onYes);
            w.EscapeAction = () => Finish(w, owner, ref done, onNo);
            w.EnterAction = () => Finish(w, owner, ref done, onYes);
            w.Open();
        }

        public static void Alert(UiOwner owner, UiTheme theme, string title, string message, Action onClose)
        {
            var w = Make(owner, theme, title, 520f);
            bool done = false;
            AddMessage(w, message);
            w.AddSpacer(6f);
            var row = w.AddRow(12f);
            row.AddSpacer(0f).SetFlexible(1f);
            var ok = row.AddButton("OK", null, UiButtonStyle.Primary);
            ok.Clicked += () => Finish(w, owner, ref done, onClose);
            w.EscapeAction = () => Finish(w, owner, ref done, onClose);
            w.EnterAction = () => Finish(w, owner, ref done, onClose);
            w.Open();
        }

        public static void Prompt(UiOwner owner, UiTheme theme, string title, string message, string initial, Action<string> onSubmit, Action onCancel)
        {
            var w = Make(owner, theme, title, 560f);
            bool done = false;
            if (!string.IsNullOrEmpty(message)) AddMessage(w, message);
            var input = w.AddInput(string.Empty, initial ?? string.Empty);
            w.AddSpacer(6f);
            var row = w.AddRow(12f);
            row.AddSpacer(0f).SetFlexible(1f);
            var cancel = row.AddButton("Cancel", null, UiButtonStyle.Secondary);
            var ok = row.AddButton("OK", null, UiButtonStyle.Primary);

            void Submit()
            {
                if (done) return;
                done = true;
                string text = input.Text;
                UiRuntime.DestroyWindow(w);
                owner.Fire(onSubmit, text, "prompt submit");
            }
            void Cancel() => Finish(w, owner, ref done, onCancel);

            ok.Clicked += Submit;
            cancel.Clicked += Cancel;
            input.Submitted += _ => Submit();
            w.EscapeAction = Cancel;
            w.Open();
            input.Focus();
        }
    }
}
