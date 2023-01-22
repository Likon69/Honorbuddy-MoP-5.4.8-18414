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
using System.Linq;

using Styx;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public class PetControl
	{
		/// <summary>
		/// <para>Returns true if you can cast the PETACTIONNAME; otherwise, false.</para>
		/// <para>This method checks for both spell existence, and if the spell is on cooldown.</para>
		/// <para>Notes:<list type="bullet">
		/// 
		/// <item><description><para>* To return 'true', the PETACTIONNAME spell must be hot-barred!  Existence in the spellbook
		/// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
		/// </list></para>
		/// </summary>
		/// <param name="petActionName">may not be null. Examples include: "Follow", "Attack", "Passive", "Growl".</param>
		/// <returns></returns>
		// 24Feb2013-07:42UTC chinajade
		public static bool CanCastPetAction(WoWPetSpell petAction)
		{
			if (petAction == null)
				{ return false; }
			if ((petAction.SpellType == WoWPetSpell.PetSpellType.Spell) && (petAction.Spell == null))
				{ return false; }

			// NB: We can't issue pet directives while mounted, so don't try...
			if (Me.Mounted)
				{ return false; }

			return (petAction.SpellType == WoWPetSpell.PetSpellType.Spell)
				? !petAction.Spell.Cooldown
				: true;
		}


		/// <summary>
		/// <para>Casts the PETACTIONNAME on WOWUNIT.</para>
		/// <para>Notes:<list type="bullet">
		/// <item><description><para>* The PETACTIONNAME spell must be hot-barred!  Existence in the spellbook
		/// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
		/// <item><description><para>* If PETACTIONNAME doesn't exist, or is not hot-barred
		/// an error message is emitted.  To avoid this, use CanCastPetAction() as an entry condition to use of this method.</para></description></item>
		/// <item><description><para>* If PETACTIONNAME is on cooldown, no action is performed.</para></description></item>
		/// </list></para>
		/// </summary>
		/// <param name="petActionName">may not be null. Examples include: "Follow", "Attack", "Passive", "Growl".</param>
		/// <param name="wowUnit">may not be null</param>
		// 24Feb2013-07:42UTC chinajade
		public static void CastPetAction(WoWPetSpell petAction, WoWUnit wowUnit = null)
		{
			if (petAction != null)
			{
				var petActionName = GetPetActionName(petAction);

				if (wowUnit == null)
				{
					QBCLog.DeveloperInfo("Instructing pet to \"{0}\".", petActionName);
					Lua.DoString("CastPetAction({0})", petAction.ActionBarIndex +1);
				}

				else if (wowUnit.IsValid)
				{
					QBCLog.DeveloperInfo("Instructing pet \"{0}\" on {1}.", petActionName, wowUnit.SafeName);
					uint originalFocus = Me.CurrentFocus;
					StyxWoW.Me.SetFocus(wowUnit);
					Lua.DoString("CastPetAction({0}, 'focus')", petAction.ActionBarIndex + 1);
					StyxWoW.Me.SetFocus(originalFocus);
				}

				else
				{
					QBCLog.DeveloperInfo("Attempt to instruct pet to \"{0}\" failed--WoWUnit is not valid.",
						petActionName);
				}
			}
		}


		// 30Jun2013-07:42UTC chinajade
		public static WoWPetSpell FindPetActionByName(string petActionName)
		{
			var petAction = Me.PetSpells.FirstOrDefault(p => p.ToString() == petActionName);

			if (petAction == null)
			{
				QBCLog.MaintenanceError("or [USER ERROR]: PetAction('{0}') is either not known, or not hot-barred.",
					petActionName);
			}

			return petAction;
		}


		// 30Jun2013-07:42UTC chinajade
		public static string GetPetActionName(WoWPetSpell petAction)
		{
			if (petAction != null)
			{
				switch (petAction.SpellType)
				{
					case WoWPetSpell.PetSpellType.Action:
						return petAction.Action.ToString();

					case WoWPetSpell.PetSpellType.Spell:
						return petAction.Spell.Name;

					case WoWPetSpell.PetSpellType.Stance:
						return petAction.Stance.ToString();
				}
				return "WoWPetSpell(UNKNOWN)";
			}
			return "WoWPetSpell(NULL)";
		}


		/// <summary>
		/// <para>Returns true if the pet is executing the PETACTIONNAME.</para>
		/// <para>The way the WoWclient works, because a PetAction is active doesn't necessarily mean it is being immediately obeyed.</para>
		/// <para>For instance, a pet's "Passive" ability may be active, but the pet may be attacking due to an explicit "Attack" command temporarily overriding it.</para>
		/// <para>During the attack, the "Passive" ability still shows active.</para>
		/// <para>Notes:<list type="bullet">
		/// <item><description><para>* The PETACTIONNAME spell must be hot-barred!  Existence in the spellbook
		/// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
		/// <item><description><para>* If PETACTIONNAME doesn't exist, or is not hot-barred, 'false' is returned.</para></description></item>
		/// </list></para>
		/// </summary>
		/// <param name="petAction">may not be null. Examples include: "Follow", "Attack", "Passive"</param>
		/// <returns></returns>
		// 24Feb2013-07:42UTC chinajade
		public static bool IsPetActionActive(WoWPetSpell petAction)
		{
			if (petAction == null)
				{ return false; }

			return Lua.GetReturnVal<bool>(string.Format("return GetPetActionInfo({0})", petAction.ActionBarIndex + 1), 4);
		}


		// 15May2013-07:42UTC chinajade
		public static bool CanPetBeDismissed()
		{
			return Me.GotAlivePet && Lua.GetReturnVal<bool>("return PetCanBeDismissed()", 0);
		}


		// 15May2013-07:42UTC chinajade
		public static void PetDismiss()
		{
			if (CanPetBeDismissed())
				{ Lua.DoString("PetDismiss()"); } 

			// Remove totems (or DK's ghoul--which is Totem #1)...
			// Ref: http://wowprogramming.com/docs/api/DestroyTotem
			Lua.DoString("DestroyTotem(1); DestroyTotem(2); DestroyTotem(3); DestroyTotem(4);");

			// TODO: Need to eliminate minions here, also
		}


		/// <summary>
		/// <para>Sends the user's pet to attack the target identified by WOWUNITDELEGATE.</para>
		/// <para>Notes:<list type="bullet">
		/// <item><description><para>* This behavior performs all appropriate checks: pet exists and is alive, target is viable and not friendly, etc.</para></description></item>
		/// <item><description><para>* The 'attack' command will continue to be issued until the pet obeys (by targeting the mob).</para></description></item>
		/// <item><description><para>* The pet's "Attack" command must be hot-barred!  Existence in the spellbook
		/// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
		/// <item><description><para>* The returned Composite is suitable for use in a behavior tree (Priority)Selector container
		/// (i.e., placing it in a Sequence container will not yield the desired results).</para></description></item>
		/// </list></para>
		/// </summary>
		/// <param name="wowUnit">may not be null</param>
		/// <returns>a behavior tree Composite suitable for use in a (Priority)Selector container</returns>
		public static void Attack(WoWUnit wowUnit)
		{
			WoWPetSpell attackAction = _petActionAttack ?? (_petActionAttack = FindPetActionByName("Attack"));

			if (Me.GotAlivePet
				&& (Me.Pet.CurrentTarget != wowUnit)
				&& Query.IsViableForFighting(wowUnit)
				&& CanCastPetAction(attackAction))
			{
				CastPetAction(attackAction, wowUnit);
			}
		}
		private static WoWPetSpell _petActionAttack;


		/// <summary>
		/// <para>Instructs the user's pet to follow its owner.</para>
		/// <para>Notes:<list type="bullet">
		/// <item><description><para>* This behavior performs all appropriate checks: pet exists and is alive, etc.</para></description></item>
		/// <item><description><para>* If the pet is attacking a mob, the 'follow' command will continue to be issued until the pet obeys.</para></description></item>
		/// <item><description><para>* The pet's "Follow" command must be hot-barred!  Existence in the spellbook
		/// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
		/// <item><description><para>* The returned Composite is suitable for use in a behavior tree (Priority)Selector container
		/// (i.e., placing it in a Sequence container will not yield the desired results).</para></description></item>
		/// </list></para>
		/// </summary>
		/// <returns>a behavior tree Composite suitable for use in a (Priority)Selector container</returns>
		public static void Follow()
		{
			WoWPetSpell followAction = _petActionFollow ?? (_petActionFollow = FindPetActionByName("Follow"));

			if (Me.GotAlivePet
				&& (!IsPetActionActive(followAction) || Me.Pet.GotTarget)
				&& CanCastPetAction(followAction))
			{
				CastPetAction(followAction);
			}
		}
		private static WoWPetSpell _petActionFollow;
		

		/// <summary>
		/// <para>Instructs the user's pet to assume one of the following stances: "Assist", "Defensive", "Passive"</para>
		/// <para>Notes:<list type="bullet">
		/// <item><description><para>* This behavior performs all appropriate checks: pet exists and is alive, etc.</para></description></item>
		/// <item><description><para>* The pet's "Assist", "Defensive", and "Passive" commands must be hot-barred!  Existence in the spellbook
		/// is insufficient.  This is a limitation of the WoWclient and HB APIs.</para></description></item>
		/// <item><description><para>* The returned Composite is suitable for use in a behavior tree (Priority)Selector container
		/// (i.e., placing it in a Sequence container will not yield the desired results).</para></description></item>
		/// </list></para>
		/// </summary>
		/// <returns></returns>
		public static void SetStance_Assist()
		{
			if (Me.GotAlivePet)
				{ SetStance_Internal(_petStanceAssist ?? (_petStanceAssist = FindPetActionByName("Assist"))); }
		}
		private static WoWPetSpell _petStanceAssist;


		public static void SetStance_Defensive()
		{
			if (Me.GotAlivePet)
				{ SetStance_Internal(_petStanceDefensive ?? (_petStanceDefensive = FindPetActionByName("Defensive"))); }
		}
		private static WoWPetSpell _petStanceDefensive;


		public static void SetStance_Passive()
		{
			if (Me.GotAlivePet)
				{ SetStance_Internal(_petStancePassive ?? (_petStancePassive = FindPetActionByName("Passive"))); }
		}
		private static WoWPetSpell _petStancePassive;


		private static void SetStance_Internal(WoWPetSpell petStance)
		{
			if (!IsPetActionActive(petStance) && CanCastPetAction(petStance))
				{ CastPetAction(petStance); }
		}

		private static LocalPlayer Me { get { return StyxWoW.Me; } }
	}
}