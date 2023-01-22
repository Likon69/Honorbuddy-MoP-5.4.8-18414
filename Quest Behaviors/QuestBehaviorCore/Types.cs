// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings
using System;

using Honorbuddy.QuestBehaviorCore.XmlElements;

using Styx;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public static class Delay
	{

		//----------
		// Commonly used 'Constants'
		//
		public static TimeSpan AfterWeaponFire
		{
			get
			{
				return (_random.Next(1, 100) < 70)
						? TimeSpan.FromMilliseconds(_random.Next(100, 350))
						: TimeSpan.FromMilliseconds(_random.Next(250, 1200));
			}
		}
		public static TimeSpan AfterItemUse { get { return TimeSpan.FromMilliseconds(_random.Next(400, 900)); } }
		public static TimeSpan AfterInteraction { get { return TimeSpan.FromMilliseconds(_random.Next(600, 1700)); } }
		public static TimeSpan BeforeButtonClick { get { return TimeSpan.FromMilliseconds(_random.Next(400, 900)); } }
		public static readonly TimeSpan LagDuration = TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150);

		private static readonly Random _random = new Random((int)DateTime.Now.Ticks +1);
	}

		
	public static class Throttle
	{
		public static readonly TimeSpan WoWClientMovement = TimeSpan.FromMilliseconds(100);
		public static readonly TimeSpan UserUpdate = TimeSpan.FromMilliseconds(1000);
	}


	//----------
	// Types
	//

	public enum AbilityTargetingType
	{
		/// <summary>Uses the caster's currently selected target.</summary>
		CurrentTarget,

		/// <summary>Uses a targeting system used mostly by vehicles that shoot projectiles</summary>
		Vehicle,

		/// <summary>
		///     The ability is an area of effect that is centered on the caster and radiates out in every direction (360 degrees) 
		///		e.g. Thunder Clap
		/// </summary>
		PointBlankAreaOfEffect,

		/// <summary>
		///     The ability's effect is centered at a ground location that is selected by the caster e.g. 'Party
		///     G.R.E.N.A.D.E.' http://www.wowhead.com/item=38577
		/// </summary>
		Ground,

		/// <summary>
		///     The ability is an area of effect that affects targets in a cone-shaped area in front of the caster
		///		e.g. Shockwave
		/// </summary>
		Cone
	}

	public enum Deprecated_MobType
	{
		Npc,
		GameObject,
	}


	public enum MobStateType
	{
		// NPC states
		Alive,
		AliveNotInCombat,
		BelowHp,
		Dead,

		DontCare,
	}


	public enum MountStrategyType
	{
		CancelShapeshift,
		Dismount,
		DismountOrCancelShapeshift,
		Mount,
		None,
	}


	public enum MovementByType
	{
		ClickToMoveOnly,
		FlightorPreferred,
		NavigatorOnly,
		NavigatorPreferred,
		None
	}


	public enum ProactiveCombatStrategyType
	{
		NoClear,
		ClearMobsTargetingUs,
		ClearMobsThatWillAggro,
		ClearAll
	}

	public enum SpellCastResult
	{
		/// <summary>The cast result is indeterminate</summary>
		Indeterminate,

		/// <summary>The spell cast failed but cause is not known</summary>
		UnknownFail,

		/// <summary>The spell cast succeeded</summary>
		Succeeded,

		/// <summary>The spell target is not valid</summary>
		InvalidTarget,

		/// <summary>The spell missed its target</summary>
		Missed,

		/// <summary>The spell target is too close to caster</summary>
		TooClose,

		/// <summary>The spell target is too far away from caster</summary>
		OutOfRange,

		/// <summary>The spell target is not in line of sight of the caster</summary>
		LineOfSight,

		/// <summary>The caster was interrupted</summary>
		Interrupted,

		/// <summary>The caster does not know spell</summary>
		SpellNotKnown,

		/// <summary>The spell is on cooldown</summary>
		NotReady,
	}

	// Delegates
	public delegate bool ProvideBoolDelegate(object context);
	public delegate int ProvideIntDelegate(object context);
	public delegate double ProvideDoubleDelegate(object context);
	public delegate string ProvideStringDelegate(object context);
	public delegate HuntingGroundsType ProvideHuntingGroundsDelegate(object context);
	public delegate MovementByType ProvideMovementByDelegate(object context);
	public delegate NavType ProvideNavTypeDelegate(object context);
	public delegate WoWItem ProvideWoWItemDelegate(object context);
	public delegate WoWObject ProvideWoWObjectDelegate(object context);
	public delegate WoWPoint ProvideWoWPointDelegate(object context);
	public delegate WoWSpell ProvideWoWSpellDelegate(object context);
	public delegate WoWUnit ProvideWoWUnitDelegate(object context);


	public class ContractException : SystemException
	{
		public ContractException(string message = null)
			: base(message ?? string.Empty)
		{
			// empty   
		}
	}
}