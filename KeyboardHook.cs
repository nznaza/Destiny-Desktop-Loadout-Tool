using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace DestinyLoadoutTool
{
    public partial class GlobalHotKey : IDisposable
    {
        /// <summary>
        /// Registers a global hotkey
        /// </summary>
        /// <param name="aKeyGesture">e.g. Alt + Shift + Control + Win + S</param>
        /// <param name="aAction">Action to be called when hotkey is pressed</param>
        /// <returns>true, if registration succeeded, otherwise false</returns>
        public static bool RegisterHotKey(string aKeyGestureString, Action<int> aAction, int id)
        {
            KeyGestureConverter c = new KeyGestureConverter();
            KeyGesture aKeyGesture = (KeyGesture)c.ConvertFrom(aKeyGestureString)!;
            return RegisterHotKey(aKeyGesture.Modifiers, aKeyGesture.Key, aAction, id);
        }

        public static bool RegisterHotKey(ModifierKeys aModifier, Key aKey, Action<int> aAction, int id)
        {
            if (aModifier == ModifierKeys.None && aKey >= Key.D0 && aKey <= Key.Z)
            {
                throw new ArgumentException("Modifier must not be ModifierKeys.None for non function Keys");
            }
            ArgumentNullException.ThrowIfNull(aAction);

            System.Windows.Forms.Keys aVirtualKeyCode = (System.Windows.Forms.Keys)KeyInterop.VirtualKeyFromKey(aKey);

            if (!registeredHotKeys.Contains(new HotKey(aModifier, aKey)))
            {
                bool aRegistered = RegisterHotKey(window.Handle, currentID + 1, (uint)aModifier | MOD_NOREPEAT, (uint)aVirtualKeyCode);

                if (aRegistered)
                {
                    registeredHotKeys.Add(new HotKeyWithAction(aModifier, aKey, aAction, id));
                    currentID++;
                }
                return aRegistered;
            }
            return false;
        }

        public void Dispose()
        {
            // unregister all the registered hot keys.
            UnregisterAllHotKeys();

            // dispose the inner native window.
            window.Dispose();
            GC.SuppressFinalize(this);
        }

        public static void UnregisterAllHotKeys()
        {
            for (int i = currentID; i > 0; i--)
            {
                UnregisterHotKey(window.Handle, i);
            }
            currentID = 0;
        }

        static GlobalHotKey()
        {
            window.KeyPressed += (s, e) =>
            {
                registeredHotKeys.TryGetValue(new HotKey(e.Modifier, e.Key), out HotKey? value);
                if (value is HotKeyWithAction v)
                    v.Action?.Invoke(v.Id);
            };
        }

        private static readonly InvisibleWindowForMessages window = new InvisibleWindowForMessages();
        private static int currentID;
        private static readonly uint MOD_NOREPEAT = 0x4000;
        private static readonly HashSet<HotKey> registeredHotKeys = [];


        private class HotKey(ModifierKeys modifier, Key key)
        {
            public ModifierKeys Modifier { get; } = modifier;
            public Key Key { get; } = key;

            public override int GetHashCode()
            {
                return Modifier.GetHashCode() + Key.GetHashCode();
            }

            public override bool Equals(object? obj)
            {
                if (obj is HotKey hka)
                    return hka.Key == Key && hka.Modifier == Modifier;
                return false;
            }
        }
        private class HotKeyWithAction(ModifierKeys modifier, Key key, Action<int> action, int id) : HotKey(modifier, key)
        {
            public int Id { get; } = id;
            public Action<int> Action { get; } = action;

        }

        // Registers a hot key with Windows.
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);
        
        // Unregisters the hot key with Windows.
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool UnregisterHotKey(nint hWnd, int id);

        private class InvisibleWindowForMessages : System.Windows.Forms.NativeWindow, IDisposable
        {
            public InvisibleWindowForMessages()
            {
                CreateHandle(new System.Windows.Forms.CreateParams());
            }

            private static readonly int WM_HOTKEY = 0x0312;
            protected override void WndProc(ref System.Windows.Forms.Message m)
            {
                base.WndProc(ref m);

                if (m.Msg == WM_HOTKEY)
                {
                    Key aWPFKey = KeyInterop.KeyFromVirtualKey((int)m.LParam >> 16 & 0xFFFF);
                    ModifierKeys modifier = (ModifierKeys)((int)m.LParam & 0xFFFF);
                    KeyPressed?.Invoke(this, new HotKeyPressedEventArgs(modifier, aWPFKey));
                }
            }

            public class HotKeyPressedEventArgs : EventArgs
            {
                private readonly ModifierKeys _modifier;
                private readonly Key _key;

                internal HotKeyPressedEventArgs(ModifierKeys modifier, Key key)
                {
                    _modifier = modifier;
                    _key = key;
                }

                public ModifierKeys Modifier
                {
                    get { return _modifier; }
                }

                public Key Key
                {
                    get { return _key; }
                }
            }


            public event EventHandler<HotKeyPressedEventArgs>? KeyPressed;

            #region IDisposable Members

            public void Dispose()
            {
                DestroyHandle();
            }

            #endregion
        }
    }
}