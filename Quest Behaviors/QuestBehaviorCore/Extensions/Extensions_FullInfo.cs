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
using System.Text;

using Styx;
using Styx.WoWInternals;
using Styx.WoWInternals.DBC;

using Tripper.Tools.Math;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	public static class Extensions_FullInfo
	{
		// 25Sep2013-07:55UTC chinajade
		public static string ToString_FullInfo(this Matrix matrix, bool useCompactForm = false, int indentLevel = 0)
		{
			var indent = string.Empty.PadLeft(indentLevel);
			var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);
			var tmp = new StringBuilder();

			tmp.AppendFormat("<Matrix");
			if (useCompactForm)
			{
				tmp.AppendFormat("{0}M11={1}, M12={2} , M13={3}, M14={4}", fieldSeparator, matrix.M11, matrix.M12, matrix.M13, matrix.M14);
				tmp.AppendFormat("{0}M21={1}, M22={2} , M23={3}, M24={4}", fieldSeparator, matrix.M21, matrix.M22, matrix.M23, matrix.M24);
				tmp.AppendFormat("{0}M31={1}, M32={2} , M33={3}, M34={4}", fieldSeparator, matrix.M31, matrix.M32, matrix.M33, matrix.M34);
				tmp.AppendFormat("{0}M41={1}, M42={2} , M43={3}, M44={4}", fieldSeparator, matrix.M41, matrix.M42, matrix.M43, matrix.M44);
			}
			else
			{
				tmp.AppendFormat("{0}M11={1,13:F5}, M12={2,13:F5}, M13={3,13:F5}, M14={4,13:F5}", fieldSeparator, matrix.M11, matrix.M12, matrix.M13, matrix.M14);
				tmp.AppendFormat("{0}M21={1,13:F5}, M22={2,13:F5}, M23={3,13:F5}, M24={4,13:F5}", fieldSeparator, matrix.M21, matrix.M22, matrix.M23, matrix.M24);
				tmp.AppendFormat("{0}M31={1,13:F5}, M32={2,13:F5}, M33={3,13:F5}, M34={4,13:F5}", fieldSeparator, matrix.M31, matrix.M32, matrix.M33, matrix.M34);
				tmp.AppendFormat("{0}M41={1,13:F5}, M42={2,13:F5}, M43={3,13:F5}, M44={4,13:F5}", fieldSeparator, matrix.M41, matrix.M42, matrix.M43, matrix.M44);
			}
			tmp.AppendFormat("{0}/>", fieldSeparator);

			return tmp.ToString();
		}


		// 9Mar2013-07:55UTC chinajade
		public static string ToString_FullInfo(this PlayerQuest playerQuest, bool useCompactForm = false, int indentLevel = 0)
		{
			var tmp = new StringBuilder();

			if (playerQuest != null)
			{
				var indent = string.Empty.PadLeft(indentLevel);
				var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

				tmp.AppendFormat("<PlayerQuest Key_Id=\"{0}\" Key_Name=\"{1}\"", playerQuest.Id, playerQuest.Name);
				tmp.AppendFormat("{0}CompletionText=\"{1}\"", fieldSeparator, playerQuest.CompletionText);
				tmp.AppendFormat("{0}Description=\"{1}\"", fieldSeparator, playerQuest.Description);
				tmp.AppendFormat("{0}FlagsPvP=\"{1}\"", fieldSeparator, playerQuest.FlagsPVP);
				tmp.AppendFormat("{0}Id=\"{1}\"", fieldSeparator, playerQuest.Id);
				tmp.AppendFormat("{0}InternalInfo=\"{1}\"", fieldSeparator, playerQuest.InternalInfo);
				tmp.AppendFormat("{0}IsAutoAccepted=\"{1}\"", fieldSeparator, playerQuest.IsAutoAccepted);
				tmp.AppendFormat("{0}IsCompleted=\"{1}\"", fieldSeparator, playerQuest.IsCompleted);
				tmp.AppendFormat("{0}IsDaily=\"{1}\"", fieldSeparator, playerQuest.IsDaily);
				tmp.AppendFormat("{0}IsFailed=\"{1}\"", fieldSeparator, playerQuest.IsFailed);
				tmp.AppendFormat("{0}IsPartyQuest=\"{1}\"", fieldSeparator, playerQuest.IsPartyQuest);
				tmp.AppendFormat("{0}IsSharable=\"{1}\"", fieldSeparator, playerQuest.IsShareable);
				tmp.AppendFormat("{0}IsStayAliveQuest=\"{1}\"", fieldSeparator, playerQuest.IsStayAliveQuest);
				tmp.AppendFormat("{0}IsWeekly=\"{1}\"", fieldSeparator, playerQuest.IsWeekly);
				tmp.AppendFormat("{0}Level=\"{1}\"", fieldSeparator, playerQuest.Level);
				tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, playerQuest.Name);
				tmp.AppendFormat("{0}NormalObjectiveRequiredCounts=\"{1}\"", fieldSeparator,
					(playerQuest.NormalObjectiveRequiredCounts == null)
					? "NONE"
					: string.Join(", ", playerQuest.NormalObjectiveRequiredCounts.Select(c => c.ToString())));
				tmp.AppendFormat("{0}Objectives=\"{1}\"", fieldSeparator,
					(playerQuest.Objectives == null)
					? "NONE"
					: string.Join(",", playerQuest.Objectives.Select(o => string.Format("{0}  \"{1}\"", fieldSeparator, o))));
				tmp.AppendFormat("{0}ObjectiveText=\"{1}\"", fieldSeparator, playerQuest.ObjectiveText);
				tmp.AppendFormat("{0}RequiredLevel=\"{1}\"", fieldSeparator, playerQuest.RequiredLevel);
				tmp.AppendFormat("{0}RewardMoney=\"{1}\"", fieldSeparator, playerQuest.RewardMoney);
				tmp.AppendFormat("{0}RewardMoneyAtMaxLevel=\"{1}\"", fieldSeparator, playerQuest.RewardMoneyAtMaxLevel);
				tmp.AppendFormat("{0}RewardNumTalentPoints=\"{1}\"", fieldSeparator, playerQuest.RewardNumTalentPoints);
				tmp.AppendFormat("{0}RewardSpell=\"{1}\"", fieldSeparator, 
					(playerQuest.RewardSpell == null)
					? null
					: ToString_FullInfo(playerQuest.RewardSpell, false, indentLevel +4));
				tmp.AppendFormat("{0}RewardSpellId=\"{1}\"", fieldSeparator, playerQuest.RewardSpellId);
				tmp.AppendFormat("{0}RewardTitleId=\"{1}\"", fieldSeparator, playerQuest.RewardTitleId);
				tmp.AppendFormat("{0}RewardXp=\"{1}\"", fieldSeparator, playerQuest.RewardXp);
				tmp.AppendFormat("{0}SubDescription=\"{1}\"", fieldSeparator, playerQuest.SubDescription);
				tmp.AppendFormat("{0}SuggestedPlayers=\"{1}\"", fieldSeparator, playerQuest.SuggestedPlayers);
				tmp.AppendFormat("{0}/>", fieldSeparator);
			}

			return tmp.ToString();
		}


		// 9Mar2013-07:55UTC chinajade
		public static string ToString_FullInfo(this Styx.WoWInternals.Quest.QuestObjective questObjective, bool useCompactForm = false, int indentLevel = 0)
		{
			var tmp = new StringBuilder();

			var indent = string.Empty.PadLeft(indentLevel);
			var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

			tmp.AppendFormat("<QuestObjective Key_Index=\"{0}\"", questObjective.Index);
			tmp.AppendFormat("{0}Count=\"{1}\"", fieldSeparator, questObjective.Count);
			tmp.AppendFormat("{0}ID=\"{1}\"", fieldSeparator, questObjective.ID);
			tmp.AppendFormat("{0}Index=\"{1}\"", fieldSeparator, questObjective.Index);
			tmp.AppendFormat("{0}IsEmpty=\"{1}\"", fieldSeparator, questObjective.IsEmpty);
			tmp.AppendFormat("{0}Objective=\"{1}\"", fieldSeparator, questObjective.Objective);
			tmp.AppendFormat("{0}Type=\"{1}\"", fieldSeparator, questObjective.Type);
			tmp.AppendFormat("{0}/>", fieldSeparator);

			return tmp.ToString();
		}


		//  9Mar2013-07:55UTC chinajade
		public static string ToString_FullInfo(this SpellEffect spellEffect, bool useCompactForm = false, int indentLevel = 0)
		{
			var tmp = new StringBuilder();

			if (spellEffect != null)
			{
				var indent = string.Empty.PadLeft(indentLevel);
				var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

				tmp.AppendFormat("<SpellEffect Key_TriggerSpell=\"{0}\"", spellEffect.TriggerSpell);
				tmp.AppendFormat("{0}Amplitude=\"{1}\"", fieldSeparator, spellEffect.Amplitude);
				tmp.AppendFormat("{0}AuraType=\"{1}\"", fieldSeparator, spellEffect.AuraType);
				tmp.AppendFormat("{0}BasePoints=\"{1}\"", fieldSeparator, spellEffect.BasePoints);
				tmp.AppendFormat("{0}ChainTarget=\"{1}\"", fieldSeparator, spellEffect.ChainTarget);
				tmp.AppendFormat("{0}EffectType=\"{1}\"", fieldSeparator, spellEffect.EffectType);
				tmp.AppendFormat("{0}ImplicitTargetA=\"{1}\"", fieldSeparator, spellEffect.ImplicitTargetA);
				tmp.AppendFormat("{0}ImplicitTargetB=\"{1}\"", fieldSeparator, spellEffect.ImplicitTargetB);
				tmp.AppendFormat("{0}ItemType=\"{1}\"", fieldSeparator, spellEffect.ItemType);
				tmp.AppendFormat("{0}Mechanic=\"{1}\"", fieldSeparator, spellEffect.Mechanic);
				tmp.AppendFormat("{0}MiscValueA=\"{1}\"", fieldSeparator, spellEffect.MiscValueA);
				tmp.AppendFormat("{0}MiscValueB=\"{1}\"", fieldSeparator, spellEffect.MiscValueB);
				tmp.AppendFormat("{0}MultipleValue=\"{1}\"", fieldSeparator, spellEffect.MultipleValue);
				tmp.AppendFormat("{0}PointsPerComboPoint=\"{1}\"", fieldSeparator, spellEffect.PointsPerComboPoint);
				tmp.AppendFormat("{0}RadiusIndex=\"{1}\"", fieldSeparator, spellEffect.RadiusIndex);
				tmp.AppendFormat("{0}RealPointsPerLevel=\"{1}\"", fieldSeparator, spellEffect.RadiusIndex);
				tmp.AppendFormat("{0}SpellClassMask=\"{1}\"", fieldSeparator, spellEffect.SpellClassMask);
				tmp.AppendFormat("{0}TriggerSpell=\"{1}\"", fieldSeparator, spellEffect.TriggerSpell);
				tmp.AppendFormat("{0}/>", fieldSeparator);
			}

			return tmp.ToString();
		}


		//  9Mar2013-07:55UTC chinajade
		public static string ToString_FullInfo(this WoWMissile wowMissile, bool useCompactForm = false, int indentLevel = 0)
		{
			var tmp = new StringBuilder();

			if (wowMissile != null)
			{
				var indent = string.Empty.PadLeft(indentLevel);
				var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

				bool isInFlight = WoWMissile.InFlightMissiles.FirstOrDefault(m => m.BaseAddress == wowMissile.BaseAddress) != null;

				tmp.AppendFormat("<WoWMissile Key_Spell=\"{0}\" BaseAddress=\"0x{1:x}\"",
					((wowMissile.Spell == null) ? "UNKNOWN" : wowMissile.Spell.Name),
					(wowMissile.BaseAddress));
				tmp.AppendFormat("{0}Caster=\"{1}\"", fieldSeparator,
                    (wowMissile.Caster == null) ? "UNKNOWN" : wowMissile.Caster.SafeName);
				tmp.AppendFormat("{0}CasterGuid=\"0x{1:x}\" <!--Me=\"0x{2:x}\" MyVehicle=\"0x{3:x}\" -->",
					fieldSeparator, wowMissile.Caster.Guid, StyxWoW.Me.Guid, StyxWoW.Me.TransportGuid);
				tmp.AppendFormat("{0}FirePosition=\"{1}\"", fieldSeparator, wowMissile.FirePosition);
				tmp.AppendFormat("{0}Flags=\"0x{1:x}\"", fieldSeparator, wowMissile.Flags);
				tmp.AppendFormat("{0}ImpactPosition=\"{1}\" <!--dist: {2:F1}-->", fieldSeparator, wowMissile.ImpactPosition,
					wowMissile.ImpactPosition.Distance(StyxWoW.Me.Location));
				tmp.AppendFormat("{0}IsInFlight=\"{1}\"", fieldSeparator, isInFlight);
				tmp.AppendFormat("{0}Position=\"{1}\" <!--dist: {2:F1}-->", fieldSeparator, wowMissile.Position,
					wowMissile.Position.Distance(StyxWoW.Me.Location));
				tmp.AppendFormat("{0}Spell=\"{1}\"", fieldSeparator,
					(wowMissile.Spell == null) ? "NONE" : wowMissile.Spell.Name);
				tmp.AppendFormat("{0}SpellId=\"{1}\"", fieldSeparator, wowMissile.SpellId);
				tmp.AppendFormat("{0}SpellVisualId=\"{1}\"", fieldSeparator, wowMissile.SpellVisualId);
				tmp.AppendFormat("{0}Target=\"{1}\"", fieldSeparator,
                    (wowMissile.Target == null) ? "NONE" : wowMissile.Target.SafeName);
				tmp.AppendFormat("{0}TargetGuid=\"0x{1:x}\"", fieldSeparator, wowMissile.TargetGuid);
				tmp.AppendFormat("{0}/>", fieldSeparator);
			}

			return tmp.ToString();
		}


		//  9Mar2013-07:55UTC chinajade
		public static string ToString_FullInfo(this WoWPetSpell wowPetSpell, bool useCompactForm = false, int indentLevel = 0)
		{
			StringBuilder tmp = new StringBuilder();

			if (wowPetSpell != null)
			{
				var indent = string.Empty.PadLeft(indentLevel);
				var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

				tmp.AppendFormat("<WoWPetSpell Key_ActionBarIndex=\"{0}\"", wowPetSpell.ActionBarIndex);
				tmp.AppendFormat("{0}Action=\"{1}\"", fieldSeparator, wowPetSpell.Action);
				tmp.AppendFormat("{0}Cooldown=\"{1}\"", fieldSeparator, wowPetSpell.Cooldown);
				tmp.AppendFormat("{0}Spell=\"{1}\"", fieldSeparator,
					(wowPetSpell.Spell == null)
					? "NONE"
					: ToString_FullInfo(wowPetSpell.Spell, useCompactForm, indentLevel + 4));
				tmp.AppendFormat("{0}SpellType=\"{1}\"", fieldSeparator, wowPetSpell.SpellType);
				tmp.AppendFormat("{0}Stance=\"{1}\"", fieldSeparator, wowPetSpell.Stance);
				tmp.AppendFormat("{0}/>", fieldSeparator);
			}

			return tmp.ToString();
		}


		//  9Mar2013-07:55UTC chinajade
		public static string ToString_FullInfo(this WoWSpell wowSpell, bool useCompactForm = false, int indentLevel = 0)
		{
			StringBuilder tmp = new StringBuilder();

			if (wowSpell != null)
			{
				var indent = string.Empty.PadLeft(indentLevel);
				var fieldSeparator = useCompactForm ? " " : string.Format("\n  {0}", indent);

				tmp.AppendFormat("<WoWSpell Key_Id=\"{0}\" Key_Name=\"{1}\"", wowSpell.Id, wowSpell.Name);
				tmp.AppendFormat("{0}BaseCooldown=\"{1}\"", fieldSeparator, wowSpell.BaseCooldown);
				// tmp.AppendFormat("{0}BaseDuration=\"{1}\"", fieldSeparator, wowSpell.BaseDuration);
				tmp.AppendFormat("{0}BaseLevel=\"{1}\"", fieldSeparator, wowSpell.BaseLevel);
				tmp.AppendFormat("{0}CanCast=\"{1}\"", fieldSeparator, wowSpell.CanCast);
				tmp.AppendFormat("{0}CastTime=\"{1}\"", fieldSeparator, wowSpell.CastTime);
				tmp.AppendFormat("{0}Category=\"{1}\"", fieldSeparator, wowSpell.Category);
				tmp.AppendFormat("{0}CooldownTime=\"{1}\"", fieldSeparator, wowSpell.Cooldown);
				tmp.AppendFormat("{0}CooldownTimeLeft=\"{1}\"", fieldSeparator, wowSpell.CooldownTimeLeft);
				tmp.AppendFormat("{0}CreatesItemId=\"{1}\"", fieldSeparator, wowSpell.CreatesItemId);
				tmp.AppendFormat("{0}DispellType=\"{1}\"", fieldSeparator, wowSpell.DispelType);
				// tmp.AppendFormat("{0}DurationPerLevel=\"{1}\"", fieldSeparator, wowSpell.DurationPerLevel);
				tmp.AppendFormat("{0}HasRange=\"{1}\"", fieldSeparator, wowSpell.HasRange);
				tmp.AppendFormat("{0}Id=\"{1}\"", fieldSeparator, wowSpell.Id);
				tmp.AppendFormat("{0}IsFunnel=\"{1}\"", fieldSeparator, wowSpell.IsFunnel);
				tmp.AppendFormat("{0}IsMelee=\"{1}\"", fieldSeparator, wowSpell.IsMeleeSpell);
				tmp.AppendFormat("{0}IsSelfOnly=\"{1}\"", fieldSeparator, wowSpell.IsSelfOnlySpell);
				tmp.AppendFormat("{0}Level: {1}", fieldSeparator, wowSpell.Level);
				// tmp.AppendFormat("{0}MaxDuration=\"{1}\"", fieldSeparator, wowSpell.MaxDuration);
				tmp.AppendFormat("{0}MaxRange=\"{1}\"", fieldSeparator, wowSpell.MaxRange);
				tmp.AppendFormat("{0}MaxStackCount=\"{1}\"", fieldSeparator, wowSpell.MaxStackCount);
				tmp.AppendFormat("{0}MaxTargets=\"{1}\"", fieldSeparator, wowSpell.MaxTargets);
				tmp.AppendFormat("{0}Mechanic=\"{1}\"", fieldSeparator, wowSpell.Mechanic);
				tmp.AppendFormat("{0}MinRange=\"{1}\"", fieldSeparator, wowSpell.MinRange);
				tmp.AppendFormat("{0}Name=\"{1}\"", fieldSeparator, wowSpell.Name);
				tmp.AppendFormat("{0}PowerCost=\"{1}\"", fieldSeparator, wowSpell.PowerCost);
				tmp.AppendFormat("{0}ResearchProjectId=\"{1}\"", fieldSeparator, wowSpell.ResearchProjectId);
				tmp.AppendFormat("{0}School=\"{1}\"", fieldSeparator, wowSpell.School);
				tmp.AppendFormat("{0}SpellDescriptionVariableId=\"{1}\"", fieldSeparator, wowSpell.SpellDescriptionVariableId);

				tmp.AppendFormat("{0}SpellEffects=\"{1}\"", fieldSeparator, (wowSpell.SpellEffects.Count() == 0) ? " NONE" : "");
				foreach (var effect in wowSpell.SpellEffects)
					{ tmp.AppendFormat("{0}  {1}", fieldSeparator, ToString_FullInfo(effect, useCompactForm, indentLevel + 4)); }

				tmp.AppendFormat("{0}SpellMissileId=\"{1}\"", fieldSeparator, wowSpell.SpellMissileId);
				tmp.AppendFormat("{0}TargetType=\"0x{1:x}\"", fieldSeparator, wowSpell.TargetType);
				tmp.AppendFormat("{0}/>", fieldSeparator);
			}

			return tmp.ToString();
		}
	}
}