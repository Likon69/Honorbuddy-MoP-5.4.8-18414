// #define REACT_TO_HOTKEYS_IN_PULSE
#define HONORBUDDY_SUPPORTS_HOTKEYS_WITHOUT_REQUIRING_A_MODIFIER

using Styx;
using Styx.CommonBot;
using Styx.WoWInternals;
using System;
using System.Linq;
using Singular.Settings;
using System.Drawing;
using Styx.Common.Helpers;
using System.Collections.Generic;
using Singular.Helpers;
using Styx.Common;
using Styx.TreeSharp;
using System.Windows.Forms;
using Styx.Pathing;
using System.Runtime.InteropServices;

namespace Singular.Managers
{
    internal static class HotkeyDirector
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern IntPtr GetActiveWindow();
        

        private static HotkeySettings HotkeySettings { get { return SingularSettings.Instance.Hotkeys(); } }

        /// <summary>
        /// True: if AOE spells are allowed, False: Single target only
        /// </summary>
        public static bool IsAoeEnabled { get { return _AoeEnabled; } }

        /// <summary>
        /// True: if PullMore spells are allowed, False: Single target only
        /// </summary>
        public static bool IsPullMoreEnabled { get { return _PullMoreEnabled; } }

        /// <summary>
        /// True: allow normal combat, False: CombatBuff and Combat behaviors are suppressed
        /// </summary>
        public static bool IsCombatEnabled { get { return _CombatEnabled; } }

        /// <summary>
        /// True: allow normal Bot movement, False: prevent any movement by Bot, Combat Routine, or Plugins
        /// </summary>
        public static bool IsMovementEnabled { get { return _MovementEnabled && !IsMovementTemporarilySuspended; } }


        private static bool IsMovementTemporarilySuspended
        {
            get 
            { 
                // check if not suspended
                if (_MovementTemporarySuspendEndtime == DateTime.MinValue )
                    return false;

                // check if still suspended
                if ( _MovementTemporarySuspendEndtime > DateTime.Now )
                    return true;

                // suspend has timed out, so refresh suspend timer if key is still down
                // -- currently only check last key pressed rather than every suspend key configured
                // if ( HotkeySettings.SuspendMovementKeys.Any( k => IsKeyDown( k )))
                if ( IsKeyDown( _lastMovementTemporarySuspendKey ))
                {
                    _MovementTemporarySuspendEndtime = DateTime.Now + TimeSpan.FromSeconds(HotkeySettings.SuspendDuration);
                    return true;
                }

                _MovementTemporarySuspendEndtime = DateTime.MinValue;
                return false;
            }

            set
            {
                if (value)
                    _MovementTemporarySuspendEndtime = DateTime.Now + TimeSpan.FromSeconds(HotkeySettings.SuspendDuration);
                else
                    _MovementTemporarySuspendEndtime = DateTime.MinValue;
            }
        }

        /// <summary>
        /// sets initial values for all key states. registers a local botevent handler so 
        /// we know when we are running and when we arent to enable/disable hotkeys
        /// </summary>
        internal static void Init()
        {
            InitKeyStates();

            SingularRoutine.OnBotEvent += (src,arg) =>
            {
                if (arg.Event == SingularBotEvent.BotStart)
                    HotkeyDirector.Start(true);
                else if (arg.Event == SingularBotEvent.BotStop)
                    HotkeyDirector.Stop();
            };
        }

        internal static void Start(bool needReset = false)
        {
            if (needReset)
                InitKeyStates();

            _HotkeysRegistered = true;

            // Hook the  hotkeys for the appropriate WOW Window...
            HotkeysManager.Initialize( StyxWoW.Memory.Process.MainWindowHandle);

            // define hotkeys for behaviors when using them as toggles (press once to disable, press again to enable)
            // .. otherwise, keys are polled for in Pulse()
            if (HotkeySettings.KeysToggleBehavior)
            {
                // register hotkey for commands with 1:1 key assignment
                if (HotkeySettings.AoeToggle != Keys.None)
                    RegisterHotkeyAssignment("AOE", HotkeySettings.AoeToggle, (hk) => { AoeToggle(); });

                if (HotkeySettings.CombatToggle != Keys.None)
                    RegisterHotkeyAssignment("Combat", HotkeySettings.CombatToggle, (hk) => { CombatToggle(); });

                // register hotkey for commands with 1:1 key assignment
                if (HotkeySettings.PullMoreToggle != Keys.None)
                    RegisterHotkeyAssignment("PullMore", HotkeySettings.PullMoreToggle, (hk) => { PullMoreToggle(); });

                // note: important to not check MovementManager if movement disabled here, since MovementManager calls us
                // .. and the potential for side-effects exists.  check SingularSettings directly for this only
                if (!SingularSettings.Instance.DisableAllMovement && HotkeySettings.MovementToggle != Keys.None)
                    RegisterHotkeyAssignment("Movement", HotkeySettings.MovementToggle, (hk) => { MovementToggle(); });
            }
        }

        private static void RegisterHotkeyAssignment(string name, Keys key, Action<Hotkey> callback)
        {
            Keys keyCode = key & Keys.KeyCode;
            ModifierKeys mods = ModifierKeys.NoRepeat;

            if ((key & Keys.Shift) != 0)
                mods |= ModifierKeys.Shift;
            if ((key & Keys.Alt) != 0)
                mods |= ModifierKeys.Alt;
            if ((key & Keys.Control) != 0)
                mods |= ModifierKeys.Control;

            Logger.Write(Color.White, "Hotkey: To disable {0}, press: [{1}]", name, key.ToFormattedString());
            HotkeysManager.Register(name, keyCode, mods, callback);
        }

        private static string ToFormattedString(this Keys key)
        {
            string txt = "";

            if ((key & Keys.Shift) != 0)
                txt += "Shift+";
            if ((key & Keys.Alt) != 0)
                txt += "Alt+";
            if ((key & Keys.Control) != 0)
                txt += "Ctrl+";
            txt += (key & Keys.KeyCode).ToString();
            return txt;
        }

        internal static void Stop()
        {
            if (!_HotkeysRegistered)
                return;

            _HotkeysRegistered = false;

            // remove hotkeys for commands with 1:1 key assignment          
            HotkeysManager.Unregister("AOE");
            HotkeysManager.Unregister("Combat");
            HotkeysManager.Unregister("Movement");

/* Suspend Movement keys have to be polled for now instead of using HotKey interface
 * since defining a HotKey won't allow the key to pass through to game client window
 * 
            // remove hotkeys for commands with 1:M key assignment
            if (_registeredMovementSuspendKeys != null)
            {
                foreach (var key in _registeredMovementSuspendKeys)
                {
                    HotkeysManager.Unregister("Movement Suspend(" + key.ToString() + ")");
                }
            }
*/
        }

        internal static void Update()
        {
            if (_HotkeysRegistered)
            {
                Stop();
                Start();
            }
        }

        /// <summary>
        /// checks whether the state of any of the ability toggles we control via hotkey
        /// has changed.  if so, update the user with a message
        /// </summary>
        internal static void Pulse()
        {
            // since we are polling system keybd, make sure our game window is active
            if (GetActiveWindow() != StyxWoW.Memory.Process.MainWindowHandle)
                return;

            // handle release of key here if not using toggle behavior
            if (!HotkeySettings.KeysToggleBehavior)
            {
                if (HotkeySettings.AoeToggle != Keys.None)
                {
                    _AoeEnabled = !IsKeyDown(HotkeySettings.AoeToggle);
                    AoeKeyHandler();
                }
                if (HotkeySettings.CombatToggle != Keys.None)
                {
                    _CombatEnabled = !IsKeyDown(HotkeySettings.CombatToggle);
                    CombatKeyHandler();
                }
                if (HotkeySettings.MovementToggle != Keys.None)
                {
                    _MovementEnabled = !IsKeyDown(HotkeySettings.MovementToggle);
                    MovementKeyHandler();
                }
            }

            TemporaryMovementKeyHandler();
        }

        internal static void AoeKeyHandler()
        {
            if (_AoeEnabled != last_IsAoeEnabled)
            {
                last_IsAoeEnabled = _AoeEnabled;
                if (last_IsAoeEnabled)
                    TellUser("AoE now active!");
                else
                    TellUser("AoE disabled... press {0} to enable", HotkeySettings.AoeToggle.ToFormattedString());
            }
        }

        internal static void PullMoreKeyHandler()
        {
            if (_PullMoreEnabled != last_IsPullMoreEnabled)
            {
                last_IsPullMoreEnabled = _PullMoreEnabled;
                if (last_IsPullMoreEnabled)
                    TellUser("PullMore now allowed!");
                else
                    TellUser("PullMore disabled... press {0} to enable", HotkeySettings.PullMoreToggle.ToFormattedString());
            }
        }

        internal static void CombatKeyHandler()
        {
            if (_CombatEnabled != last_IsCombatEnabled)
            {
                last_IsCombatEnabled = _CombatEnabled;
                if (last_IsCombatEnabled)
                    TellUser("Combat now enabled!");
                else
                    TellUser("Combat disabled... press {0} to enable", HotkeySettings.CombatToggle.ToFormattedString());
            }
        }

        internal static void MovementKeyHandler()
        {
            if (_MovementEnabled != last_IsMovementEnabled)
            {
                last_IsMovementEnabled = _MovementEnabled;
                if (last_IsMovementEnabled)
                    TellUser("Movement now enabled!");
                else
                    TellUser("Movement disabled... press {0} to enable", HotkeySettings.MovementToggle.ToFormattedString() );

                MovementManager.Update();
            }
        }

        internal static void TemporaryMovementKeyHandler()
        {
            // bail out if temporary movement suspensio not enabled
            if ( !HotkeySettings.SuspendMovement)
                return;

            // loop through array (ugghhh) polling for keys current state
            foreach (Keys key in HotkeySettings.SuspendMovementKeys)
            {
                if ( IsKeyDown(key))
                {
                    MovementTemporary_Suspend(key);
                    break;
                }
            }

            if (IsMovementTemporarilySuspended != last_IsMovementTemporarilySuspended)
            {
                last_IsMovementTemporarilySuspended = IsMovementTemporarilySuspended;

                // keep these notifications in Log window only
                if (last_IsMovementTemporarilySuspended)
                    Logger.Write(Color.White, "Bot Movement disabled during user movement...");
                else
                    Logger.Write(Color.White, "Bot Movement restored!");

                MovementManager.Update();
            }
        }

        #region Helpers

        private static void TellUser(string template, params object[] args)
        {
            string msg = string.Format(template, args);
            Logger.Write( Color.Yellow, string.Format("Hotkey: " + msg));
            if ( HotkeySettings.ChatFrameMessage )
                Lua.DoString(string.Format("print('{0}!')", msg));
        }

        #endregion

        // track whether keys registered yet
        private static bool _HotkeysRegistered = false;

        // state of each toggle kept here
        private static bool _AoeEnabled;
        private static bool _CombatEnabled;
        private static bool _MovementEnabled;
        private static bool _PullMoreEnabled;
        private static DateTime _MovementTemporarySuspendEndtime = DateTime.MinValue;
        private static Keys _lastMovementTemporarySuspendKey;

        // save keys used at last Register
        public static Keys[] _registeredMovementSuspendKeys;

        // state prior to last puls saved here
        private static bool last_IsAoeEnabled;
        private static bool last_IsCombatEnabled;
        private static bool last_IsPullMoreEnabled;
        private static bool last_IsMovementEnabled;
        private static bool last_IsMovementTemporarilySuspended; 

        // state toggle helpers
        private static bool AoeToggle()
        {
            _AoeEnabled = _AoeEnabled ? false : true;
#if !REACT_TO_HOTKEYS_IN_PULSE
            AoeKeyHandler();
#endif
            return (_AoeEnabled);
        }

        private static bool PullMoreToggle()
        {
            _PullMoreEnabled = _PullMoreEnabled ? false : true;
#if !REACT_TO_HOTKEYS_IN_PULSE
            PullMoreKeyHandler();
#endif
            return (_PullMoreEnabled);
        }

        private static bool CombatToggle() 
        { 
            _CombatEnabled = _CombatEnabled ? false : true;
#if !REACT_TO_HOTKEYS_IN_PULSE
            CombatKeyHandler();
#endif
            return (_CombatEnabled); 
        }

        private static bool MovementToggle() 
        { 
            _MovementEnabled = _MovementEnabled ? false : true;
            if ( !_MovementEnabled )
                StopMoving.Now();

#if !REACT_TO_HOTKEYS_IN_PULSE
            MovementKeyHandler();
#endif
            return (_MovementEnabled); 
        }

        private static void MovementTemporary_Suspend( Keys key)
        {
            _lastMovementTemporarySuspendKey = key;
            if (_MovementEnabled)
            {
                if (!IsWowKeyBoardFocusInFrame())
                    IsMovementTemporarilySuspended = true;

#if !REACT_TO_HOTKEYS_IN_PULSE
                TemporaryMovementKeyHandler();
#endif
            }
        }

        /// <summary>
        /// returns true if WOW keyboard focus is in a frame/entry field
        /// </summary>
        /// <returns></returns>
        private static bool IsWowKeyBoardFocusInFrame()
        {
            List<string> ret = Lua.GetReturnValues("return GetCurrentKeyBoardFocus()");
            return ret != null;
        }

        private static void InitKeyStates()
        {
            // reset these values so we begin at same state every Start
            _AoeEnabled = true;
            _CombatEnabled = true;
            _PullMoreEnabled = true;
            _MovementEnabled = true;
            _MovementTemporarySuspendEndtime = DateTime.MinValue;

            last_IsAoeEnabled = true;
            last_IsCombatEnabled = true;
            last_IsPullMoreEnabled = true;
            last_IsMovementEnabled = true;
            last_IsMovementTemporarilySuspended = false;
        }

        private static Dictionary<string, Keys> mapWowKeyToWindows = new Dictionary<string, Keys>
        {
        };

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern short GetAsyncKeyState(int vkey);

        static bool IsKeyDown(Keys key)
        {
            return (GetAsyncKeyState((int) key) & 0x8000) != 0;
        }
    }

}