// Behavior originally contributed by Nesox / completely reworked by Chinajade
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Usings
using System.Linq;
using Styx;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	// 11Mar2013-04:41UTC chinajade
	public class VehicleAbility
	{
		public VehicleAbility(int abilityIndex /*[1..12]*/)
		{
			Contract.Requires((1 <= abilityIndex) && (abilityIndex <= 12),
								context => "1 <= abilityIndex) && (abilityIndex <= 12)");

			AbilityIndex = abilityIndex;
			_luaCommand_UseAbility = string.Format("CastPetAction({0})", abilityIndex);
			_luaQuery_IsUsable = string.Format("return GetPetActionSlotUsable({0})", abilityIndex);
			_vehicleAbilityNameDefault = string.Format("VehicleAbility({0})", abilityIndex);

			QBCLog.DeveloperInfo("NEW VehicleAbility{0}: {1}", AbilityIndex, Name);
			LogAbilityUse = true;

			AbilityUseCount = 0;
		}

		public int AbilityIndex { get; private set; }
		public int AbilityUseCount { get; private set; }
		public bool LogAbilityUse { get; set; }

		#region Private and Convenience variables
		private LocalPlayer Me { get { return QuestBehaviorBase.Me; } }

		private readonly string _luaQuery_IsUsable;
		private readonly string _luaCommand_UseAbility;
		private WoWPetSpell _vehicleAbility;
		private string _vehicleAbilityName;
		private readonly string _vehicleAbilityNameDefault;
		private readonly static WoWPetSpell.PetSpellType[] _vehicleAbilityTypes =
			{
				WoWPetSpell.PetSpellType.Action,
				WoWPetSpell.PetSpellType.Spell,
				WoWPetSpell.PetSpellType.Stance
			};
		#endregion


		// 11Mar2013-04:41UTC chinajade
		protected WoWPetSpell FindVehicleAbility()
		{
			if (!Query.IsVehicleActionBarShowing())
			{
				// assignment intentional, we want it to become null again when we exit vehicle...
				return _vehicleAbility = null;
			}

			return
				_vehicleAbility
				?? (_vehicleAbility =
						Me.PetSpells
						.FirstOrDefault(petSpell => _vehicleAbilityTypes.Contains(petSpell.SpellType)
														&& ((petSpell.ActionBarIndex + 1) == AbilityIndex)));
		}


		// 11Mar2013-04:41UTC chinajade
		protected WoWSpell FindVehicleAbilitySpell()
		{
			var vehicleAbility = FindVehicleAbility();

			return
				(vehicleAbility == null)
				? null
				: vehicleAbility.Spell;
		}


		// 11Mar2013-04:41UTC chinajade
		public bool IsAbilityReady()
		{
			var spell = FindVehicleAbilitySpell();

			return
				IsAbilityUsable()
				&& (spell != null)
				&& !spell.Cooldown;
		}


		// 11Mar2013-04:41UTC chinajade
		public bool IsAbilityUsable()
		{
			return Query.IsVehicleActionBarShowing()
				&& Lua.GetReturnVal<bool>(_luaQuery_IsUsable, 0);
		}


		public double MaxRange
		{
			get
			{
				var vehicleAbility = FindVehicleAbility();

				return (vehicleAbility != null)
					? vehicleAbility.Spell.MaxRange
					: double.NaN;
			}
		}


		// 11Mar2013-04:41UTC chinajade
		public string Name
		{
			get
			{
				var vehicleAbility = FindVehicleAbility();

				if (vehicleAbility == null)
				{
					// We want dynamic name to return to null when we exit vehicle...
					_vehicleAbilityName = null;
					return _vehicleAbilityNameDefault;
				}

				// If we don't yet know the name, acquire it...
				if (string.IsNullOrEmpty(_vehicleAbilityName))
					{ _vehicleAbilityName = string.Format("{0}({1})", vehicleAbility.ToString(), vehicleAbility.Spell.Id); }

				return _vehicleAbilityName;
			}
		}


		public bool UseAbility()
		{
			if (!Query.IsVehicleActionBarShowing())
			{
				QBCLog.Warning("Attempted to use {0} while not in Vehicle!", Name);
				return false;
			}

			if (IsAbilityReady())
			{
				Lua.DoString(_luaCommand_UseAbility);
				++AbilityUseCount;

				if (LogAbilityUse)
					{ QBCLog.DeveloperInfo("{0} ability used (count: {1})", Name, AbilityUseCount);  }

				return true;
			}

			return false;
		}
	}
}
