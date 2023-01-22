using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HighVoltz.BehaviorTree;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.Dynamic;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Component = HighVoltz.BehaviorTree.Component;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("CastSpell", new [] { "CastSpellAction" })]
    public sealed class CastSpellAction : PBAction
    {
        // number of times the recipe will be crafted
        public enum RepeatCalculationType
        {
            Specific,
            Craftable,
            Banker,
        }

        private readonly Stopwatch _spamControl;
        private uint _recastTime;
        private uint _waitTime;

        public CastSpellAction()
        {
            _spamControl = new Stopwatch();
            QueueIsRunning = false;
            Properties["Casted"] = new MetaProp("Casted", typeof(int), new ReadOnlyAttribute(true),
                                                new DisplayNameAttribute(Strings["Action_CastSpellAction_Casted"]));

            Properties["SpellName"] = new MetaProp("SpellName", typeof(string), new ReadOnlyAttribute(true),
                                                   new DisplayNameAttribute(Strings["Action_Common_SpellName"]));

            Properties["Repeat"] = new MetaProp("Repeat", typeof(DynamicProperty<int>),
                                                new TypeConverterAttribute(
                                                    typeof(DynamicProperty<int>.DynamivExpressionConverter)),
                                                new DisplayNameAttribute(Strings["Action_Common_Repeat"]));

            Properties["Entry"] = new MetaProp("Entry", typeof(DynamicProperty<uint>),
                new TypeConverterAttribute(typeof(DynamicProperty<uint>.DynamivExpressionConverter)),
                                               new DisplayNameAttribute(Strings["Action_Common_SpellEntry"]));

            Properties["CastOnItem"] = new MetaProp("CastOnItem", typeof(bool),
                                                    new DisplayNameAttribute(
                                                        Strings["Action_CastSpellAction_CastOnItem"]));

            Properties["ItemType"] = new MetaProp("ItemType", typeof(InventoryType),
                                                  new DisplayNameAttribute(Strings["Action_Common_ItemType"]));

            Properties["ItemId"] = new MetaProp("ItemId", typeof(uint),
                                                new DisplayNameAttribute(Strings["Action_Common_ItemEntry"]));

            Properties["RepeatType"] = new MetaProp("RepeatType", typeof(RepeatCalculationType),
                                                    new DisplayNameAttribute(
                                                        Strings["Action_CastSpellAction_RepeatType"]));
            // Properties["Recipe"] = new MetaProp("Recipe", typeof(Recipe), new TypeConverterAttribute(typeof(RecipeConverter)));

            Casted = 0;
            Repeat = new DynamicProperty<int>("Repeat", this, "1");
            Entry = new DynamicProperty<uint>("Entry", this, "0");
            RepeatType = RepeatCalculationType.Craftable;
            Recipe = null;
            CastOnItem = false;
            ItemType = InventoryType.Chest;
            ItemId = 0u;
            Properties["SpellName"].Value = SpellName;

            //Properties["Recipe"].Show = false;
            Properties["ItemType"].Show = false;
            Properties["ItemId"].Show = false;
            Properties["Casted"].PropertyChanged += OnCounterChanged;
            CheckTradeskillList();
            Properties["RepeatType"].PropertyChanged += CastSpellActionPropertyChanged;
            Properties["Entry"].PropertyChanged += OnEntryChanged;
            Properties["CastOnItem"].PropertyChanged += CastOnItemChanged;
        }

        public CastSpellAction(Recipe recipe, int repeat, RepeatCalculationType repeatType)
            : this()
        {
            Recipe = recipe;
            Repeat = new DynamicProperty<int>("Repeat", this, repeat.ToString(CultureInfo.InvariantCulture));
            Entry = new DynamicProperty<uint>("Entry", this, recipe.SpellId.ToString(CultureInfo.InvariantCulture));
            RepeatType = repeatType;
            //Properties["Recipe"].Show = true;
            Properties["SpellName"].Value = SpellName;
            PB.UpdateMaterials();
        }

        [PBXmlAttribute]
        public RepeatCalculationType RepeatType
        {
            get { return Properties.GetValue<RepeatCalculationType>("RepeatType"); }
            set { Properties["RepeatType"].Value = value; }
        }

        public int CalculatedRepeat
        {
            get { return CalculateRepeat(); }
        }

        [PBXmlAttribute]
        [TypeConverter(typeof(DynamicProperty<int>.DynamivExpressionConverter))]
        public DynamicProperty<int> Repeat
        {
            get { return Properties.GetValue<DynamicProperty<int>>("Repeat"); }
            set { Properties["Repeat"].Value = value; }
        }

        // number of times repeated.
        public int Casted
        {
            get { return Properties.GetValue<int>("Casted"); }
            set { Properties["Casted"].Value = value; }
        }

        // number of times repeated.
        [PBXmlAttribute]
        [TypeConverter(typeof(DynamicProperty<uint>.DynamivExpressionConverter))]
        public DynamicProperty<uint> Entry
        {
            get { return Properties.GetValue<DynamicProperty<uint>>("Entry"); }
            set { Properties["Entry"].Value = value; }
        }

        public Recipe Recipe { get; private set; }

        [PBXmlAttribute]
        public bool CastOnItem
        {
			get { return Properties.GetValue<bool>("CastOnItem"); }
            set { Properties["CastOnItem"].Value = value; }
        }

        [PBXmlAttribute]
        public InventoryType ItemType
        {
			get { return Properties.GetValue<InventoryType>("ItemType"); }
            set { Properties["ItemType"].Value = value; }
        }

        [PBXmlAttribute]
        public uint ItemId
        {
            get { return Properties.GetValue<uint>("ItemId"); }
            set { Properties["ItemId"].Value = value; }
        }

        public string SpellName
        {
            get { return Recipe != null ? Recipe.Name : Entry.Value.ToString(CultureInfo.InvariantCulture); }
        }

        public bool IsRecipe
        {
            get { return Recipe != null; }
        }

        // used to confim if a spell finished. set in the lua OnCastSucceeded callback.
        private bool Confimed { get; set; }
        private bool QueueIsRunning { get; set; }

        private WoWItem TargetedItem
        {
            get
            {
                return StyxWoW.Me.BagItems.Where(i => (i.ItemInfo.InventoryType == ItemType && ItemId == 0) ||
                                                            (ItemId > 0 && i.Entry == ItemId)).
                    OrderByDescending(i => i.ItemInfo.Level).ThenBy(i => i.Quality).FirstOrDefault();
            }
        }

        public override Color Color
        {
            get { return IsRecipe ? Color.DarkRed : Color.Black; }
        }

        public override string Name
        {
            get { return PB.Strings["Action_CastSpellAction_Name"]; }
        }

        public override string Title
        {
            get { return string.Format("{0}: {1} x{2} ({3})", Name, SpellName, CalculatedRepeat, CalculatedRepeat - Casted); }
        }

        public override string Help
        {
            get { return PB.Strings["Action_CastSpellAction_Help"]; }
        }

        private void OnEntryChanged(object sender, MetaPropArgs e)
        {
            CheckTradeskillList();
        }

        private void CastOnItemChanged(object sender, MetaPropArgs e)
        {
            if (CastOnItem)
            {
                Properties["ItemType"].Show = true;
                Properties["ItemId"].Show = true;
            }
            else
            {
                Properties["ItemType"].Show = false;
                Properties["ItemId"].Show = false;
            }
            RefreshPropertyGrid();
        }

        private void CastSpellActionPropertyChanged(object sender, MetaPropArgs e)
        {
            IsDone = false;
            PB.UpdateMaterials();
        }

        private int CalculateRepeat()
        {
            try
            {
                switch (RepeatType)
                {
                    case RepeatCalculationType.Specific:
                        return Repeat;
                    case RepeatCalculationType.Craftable:
                        return IsRecipe ? (int)Recipe.CanRepeatNum2 : Repeat;
                    case RepeatCalculationType.Banker:
                        if (IsRecipe && PB.DataStore.ContainsKey(Recipe.CraftedItemID))
                        {
                            return Repeat > PB.DataStore[Recipe.CraftedItemID]
                                       ? Repeat - PB.DataStore[Recipe.CraftedItemID]
                                       : 0;
                        }
                        return Repeat;
                }
                return Repeat;
            }
            catch
            {
                return 0;
            }
        }

        private void OnCounterChanged(object sender, MetaPropArgs e)
        {
            RefreshPropertyGrid();
        }

		protected async override Task Run()
		{
			if (Me.IsFlying && await CommonCoroutines.Dismount("Crafting an item"))
				return;

			if (!IsRecipe)
				CheckTradeskillList();

			if (Casted >= CalculatedRepeat)
			{
				Finished();
				return;
			}

			// can't make recipe so stop trying.
			if (IsRecipe && Recipe.CanRepeatNum2 <= 0)
			{
				Finished("Not enough material.");
				return;
			}

			if (Me.IsCasting && Me.CastingSpellId != Entry)
				SpellManager.StopCasting();

			// we should confirm the last recipe in list so we don't make an axtra
			if (Casted + 1 < CalculatedRepeat || (Casted + 1 == CalculatedRepeat &&
																  (Confimed || !_spamControl.IsRunning ||
																   (_spamControl.ElapsedMilliseconds >=
																	(_recastTime + (_recastTime / 2)) + _waitTime &&
																	!StyxWoW.Me.IsCasting))))
			{
				if (!_spamControl.IsRunning || Me.IsCasting && Me.CurrentCastTimeLeft <= TimeSpan.FromMilliseconds(250) ||
					(!StyxWoW.Me.IsCasting && _spamControl.ElapsedMilliseconds >= _waitTime))
				{
					if (StyxWoW.Me.IsMoving)
						WoWMovement.MoveStop();
					if (!QueueIsRunning)
					{
						Lua.Events.AttachEvent("UNIT_SPELLCAST_SUCCEEDED", OnUnitSpellCastSucceeded);
						QueueIsRunning = true;
						TreeRoot.StatusText = string.Format("Casting: {0}",
															IsRecipe
																? Recipe.Name
																: Entry.ToString());
					}
					WoWSpell spell = WoWSpell.FromId((int)Entry.Value);
					if (spell == null)
					{
						PBLog.Warn("{0}: {1}", Strings["Error_UnableToFindSpellWithEntry"], Entry.Value.ToString(CultureInfo.InvariantCulture));
						return;
					}

					_recastTime = spell.CastTime;
					ProfessionbuddyBot.Debug("Casting {0}, recast :{1}", spell.Name, _recastTime);
					if (CastOnItem)
					{
						WoWItem item = TargetedItem;
						if (item != null)
							spell.CastOnItem(item);
						else
						{
							PBLog.Warn(
								"{0}: {1}",
								Strings["Error_UnableToFindItemToCastOn"],
								IsRecipe
									? Recipe.Name
									: Entry.Value.ToString(CultureInfo.InvariantCulture));
							IsDone = true;
						}
					}
					else
					{
						spell.Cast();
					}
					_waitTime = Util.WowWorldLatency * 3 + 50;
					Confimed = false;
					_spamControl.Reset();
					_spamControl.Start();
				}
			}
		}

        void Finished(string reason = null)
        {
            if (StyxWoW.Me.IsCasting && StyxWoW.Me.CastingSpell.Id == Entry)
                SpellManager.StopCasting();
            Lua.Events.DetachEvent("UNIT_SPELLCAST_SUCCEEDED", OnUnitSpellCastSucceeded);
            var reasonString = !string.IsNullOrEmpty(reason) ? string.Format(" Reason: {0}", reason) : "";
            ProfessionbuddyBot.Debug("Done crafting {0}.{1}", IsRecipe ? SpellName : Entry.Value.ToString(CultureInfo.InvariantCulture), reasonString);
            IsDone = true;
        }

        private void OnUnitSpellCastSucceeded(object obj, LuaEventArgs args)
        {
            try
            {
                if ((string)args.Args[0] == "player" && (uint)((double)args.Args[4]) == Entry)
                {
                    // confirm last recipe
                    if (Casted + 1 == CalculatedRepeat)
                    {
                        Confimed = true;
                    }
                    if (RepeatType != RepeatCalculationType.Craftable)
                        Casted++;
                    ProfessionbuddyBot.Instance.UpdateMaterials();
                    if (MainForm.IsValid)
                    {
                        MainForm.Instance.RefreshTradeSkillTabs();
                        MainForm.Instance.RefreshActionTree(typeof(CastSpellAction));
                    }
                }
            }
            catch (Exception ex)
            {
                PBLog.Warn(ex.ToString());
            }
        }

        // check tradeskill list if spell is a recipe the player knows and updates Recipe if so.

        public void CheckTradeskillList()
        {
            if (PB.IsTradeSkillsLoaded)
            {
                Recipe =
                    PB.TradeSkillList.Where(t => t.KnownRecipes.ContainsKey(Entry)).Select(t => t.KnownRecipes[Entry]).
                        FirstOrDefault();
                if (IsRecipe)
                {
                    //Properties["Recipe"].Show = true;
                    Properties["SpellName"].Value = SpellName;
                    PB.UpdateMaterials();
                }
                else
                {
                    //Properties["Recipe"].Show = false;
                    Properties["SpellName"].Value = SpellName;
                }
            }
        }

        public override void Reset()
        {
            base.Reset();
            _spamControl.Reset();
            Lua.Events.DetachEvent("UNIT_SPELLCAST_SUCCEEDED", OnUnitSpellCastSucceeded);
            Casted = 0;
            QueueIsRunning = false;
            Confimed = false;
        }

	    public override IPBComponent DeepCopy()
	    {
			return new CastSpellAction
			{
				Entry = Entry,
				Repeat = Repeat,
				ItemType = ItemType,
				RepeatType = RepeatType,
				ItemId = ItemId,
				CastOnItem = CastOnItem
			};
	    }

        public static List<CastSpellAction> GetCastSpellActionList (Component pa)
        {
            if (pa is CastSpellAction)
                return new List<CastSpellAction> { (pa as CastSpellAction) };

            List<CastSpellAction> ret = null;
            var composite = pa as Composite;
            if (composite != null)
            {
                foreach (var child in composite.Children)
                {
					List<CastSpellAction> tmp = GetCastSpellActionList(child);
                    if (tmp != null)
                    {
                        // lets create a list only if we need to... (optimization)
                        if (ret == null)
                            ret = new List<CastSpellAction>();
                        ret.AddRange(tmp);
                    }
                }
            }
            return ret;
        }

    }

    internal static class WoWSpellExt
    {
        public static void CastOnItem(this WoWSpell spell, WoWItem item)
        {
            using (StyxWoW.Memory.AcquireFrame())
            {
                spell.Cast();
                Lua.DoString("UseContainerItem({0}, {1})", item.BagIndex + 1, item.BagSlot + 1);
            }
        }
    }
}