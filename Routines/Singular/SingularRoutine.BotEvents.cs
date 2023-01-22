using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Routines;
using Styx.WoWInternals.DBC;
using System.Drawing;
using Singular.Helpers;

namespace Singular
{
    public enum SingularBotEvent
    {
        BotChanged,
        BotStart,
        BotStarted,
        BotStop,
        BotStopped
    }

    public class SingularBotEventArg : EventArgs
    {
        public readonly SingularBotEvent Event;

        public SingularBotEventArg(SingularBotEvent ev)
        {
            Event = ev;
        }
    }

    partial class SingularRoutine
    {
        /// <summary>
        /// Added to encapsulate any context validation (like are we current routine) 
        /// .. at pont of raising event rather than by every subscriber
        /// </summary>
        public static event EventHandler<SingularBotEventArg> OnBotEvent;

        private static bool _botEventSubscribed;

        private void SingularBotEventInitialize()
        {
            if (!_botEventSubscribed)
            {
                _botEventSubscribed = true;
                BotEvents.OnBotChanged += e => SingularRaiseBotEvent(SingularBotEvent.BotChanged);
                BotEvents.OnBotStart += e => SingularRaiseBotEvent(SingularBotEvent.BotStart);
                BotEvents.OnBotStarted += e => SingularRaiseBotEvent(SingularBotEvent.BotStarted);
                BotEvents.OnBotStop += e => SingularRaiseBotEvent(SingularBotEvent.BotStop);
                BotEvents.OnBotStopped += e => SingularRaiseBotEvent(SingularBotEvent.BotStopped);
            }
        }

        public void SingularRaiseBotEvent( SingularBotEvent ev)
        {
            try 
            {
                if (_botEventSubscribed && RoutineManager.Current.Name == Name)
                    OnBotEvent(this, new SingularBotEventArg( ev));
            }
            catch
            {
            }
        }
    }
}
