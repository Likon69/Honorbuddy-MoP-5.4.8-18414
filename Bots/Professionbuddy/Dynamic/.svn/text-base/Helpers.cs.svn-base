using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.WoWInternals;

namespace HighVoltz.Professionbuddy.Dynamic
{
	public class Helpers
	{
		private const string LoginLua = @" 
if (RealmList and RealmList:IsVisible()) then  
	for i = 1, select('#',GetRealmCategories()) do  
		for j = 1, GetNumRealms(i) do  
			if GetRealmInfo(i, j) == ""{1}""  then  
				RealmList:Hide()  
				ChangeRealm(i, j)  
			end  
		end  
	end  
elseif (CharacterSelectUI and CharacterSelectUI:IsVisible()) then  
	if GetServerName() ~= ""{1}""  and (not RealmList or not RealmList:IsVisible()) then  
		RequestRealmList(1)  
	else  
		for i = 1,GetNumCharacters() do  
			if (GetCharacterInfo(i) == ""{0}"" ) then  
				CharacterSelect_SelectCharacter(i)  
				EnterWorld()  
			end  
		end  
	end  
elseif (CharCreateRandomizeButton and CharCreateRandomizeButton:IsVisible()) then  
	CharacterCreate_Back()  
end ";

		// {0} = GuildName
		private const string GbankTabCountFormat = @"for k,v in pairs(DataStore_ContainersDB.global.Guilds) do  
	if string.find(k,""{0}"") and v.Tabs then  
		local cnt = 0
		for k2,v2 in ipairs(v.Tabs) do
			cnt = cnt + 1
		end
		return cnt
	end  
end  
return 0
";

		// {0} = GuildName, {1} = item Id;
		private const string InGbankCountFormat = @"local itemCount = 0  
for k,v in pairs(DataStore_ContainersDB.global.Guilds) do  
	if string.find(k,""{0}"") and v.Tabs then  
		for k2,v2 in ipairs(v.Tabs) do  
			if v2 and v2.ids then  
				for k3,v3 in pairs(v2.ids) do  
					if v3 == {1} then  
						itemCount = itemCount + (v2.counts[k3] or 1)  
					end  
				end  
			end  
		end  
	end  
end  
return itemCount 
";

		// {0} = GuildName
		private const string GBankTotalFreeSlotsFormat = @"
local freeSlots = 0
for k,v in pairs(DataStore_ContainersDB.global.Guilds) do  
   if string.find(k,""{0}"") and v.Tabs then  
	  for k2,v2 in ipairs(v.Tabs) do  
		 if v2 and v2.ids then  
			local usedSlotCnt = 0
			for k3,v3 in pairs(v2.ids) do  
			   usedSlotCnt = usedSlotCnt + 1
			end  
			freeSlots =  freeSlots + (98 - usedSlotCnt)
		 end  
	  end  
   end  
end 
return freeSlots
";

		// {0} = GuildName, {1} = Tab
		private const string GBankTabFreeSlotsFormat = @"
for k,v in pairs(DataStore_ContainersDB.global.Guilds) do  
   if string.find(k,""{0}"") and v.Tabs then  
	  for k2,v2 in ipairs(v.Tabs) do
		  if {1} == k2 then
			  local usedSlotCnt = 0
			  for k3,v3 in pairs(v2.ids) do  
				 usedSlotCnt = usedSlotCnt + 1
			  end  
			  return 98 - usedSlotCnt
		  end
	  end
   end  
end 
return 0";
		private static HBRelogApi _hbRelog;
		private static bool _isSwitchingToons;

		static Helpers()
		{
			Alchemy = new TradeskillHelper(SkillLine.Alchemy);
			Archaeology = new TradeskillHelper(SkillLine.Archaeology);
			Blacksmithing = new TradeskillHelper(SkillLine.Blacksmithing);
			Cooking = new TradeskillHelper(SkillLine.Cooking);
			Enchanting = new TradeskillHelper(SkillLine.Enchanting);
			Engineering = new TradeskillHelper(SkillLine.Engineering);
			FirstAid = new TradeskillHelper(SkillLine.FirstAid);
			Fishing = new TradeskillHelper(SkillLine.Fishing);
			Herbalism = new TradeskillHelper(SkillLine.Herbalism);
			Inscription = new TradeskillHelper(SkillLine.Inscription);
			Jewelcrafting = new TradeskillHelper(SkillLine.Jewelcrafting);
			Leatherworking = new TradeskillHelper(SkillLine.Leatherworking);
			Mining = new TradeskillHelper(SkillLine.Mining);
			Tailoring = new TradeskillHelper(SkillLine.Tailoring);
			Skinning = new TradeskillHelper(SkillLine.Skinning);
		}

		// lazy load the HBRelogApi class.
		public static HBRelogApi HBRelog
		{
			get { return _hbRelog ?? (_hbRelog = new HBRelogApi()); }
		}

		public static bool IsSwitchingToons
		{
			get { return _isSwitchingToons; }
		}

		public static TradeskillHelper Alchemy { get; private set; }
		public static TradeskillHelper Archaeology { get; private set; }
		public static TradeskillHelper Blacksmithing { get; private set; }
		public static TradeskillHelper Cooking { get; private set; }
		public static TradeskillHelper Enchanting { get; private set; }
		public static TradeskillHelper Engineering { get; private set; }
		public static TradeskillHelper FirstAid { get; private set; }
		public static TradeskillHelper Fishing { get; private set; }
		public static TradeskillHelper Herbalism { get; private set; }
		public static TradeskillHelper Inscription { get; private set; }
		public static TradeskillHelper Jewelcrafting { get; private set; }
		public static TradeskillHelper Leatherworking { get; private set; }
		public static TradeskillHelper Mining { get; private set; }
		public static TradeskillHelper Tailoring { get; private set; }
		public static TradeskillHelper Skinning { get; private set; }

		public static void Log(string f, params object[] args)
		{
			Logging.Write(f, args);
		}

		public static void Log(Color c, string f, params object[] args)
		{
			Logging.Write(c, f, args);
		}

		public static void Log(System.Drawing.Color c, string f, params object[] args)
		{
			Logging.Write(Color.FromArgb(c.A, c.R, c.G, c.B), f, args);
		}

		public static int InbagCount(uint id)
		{
			return (int) Ingredient.GetInBagItemCount(id);
		}

		public static float DistanceTo(double x, double y, double z)
		{
			return StyxWoW.Me.Location.Distance(new WoWPoint(x, y, z));
		}

		public static void MoveTo(double x, double y, double z)
		{
			Util.MoveTo(new WoWPoint(x, y, z));
		}

		public static void CTM(double x, double y, double z)
		{
			WoWMovement.ClickToMove(new WoWPoint(x, y, z));
		}

        /// <summary>
        ///     Switches to a different character on same account
        /// </summary>
        /// <param name="character"></param>
        /// <param name="server"></param>
        /// <param name="botName">Name of bot to use on that character. The bot class type name will also work.</param>
        public static void SwitchCharacter(string character, string server, string botName)
        {
            if (_isSwitchingToons)
            {
                PBLog.Log("Already switching characters");
                return;
            }
            string loginLua = string.Format(LoginLua, character, server);
            _isSwitchingToons = true;
            // reset all actions 
            ProfessionbuddyBot.Instance.IsRunning = false;
            ProfessionbuddyBot.Instance.Reset();

			Application.Current.Dispatcher.BeginInvoke(
				new Action(
					() =>
						{
							Lua.DoString("Logout()");

							new Thread(
								() =>
									{
										while (StyxWoW.IsInGame)
											Thread.Sleep(2000);
										while (!StyxWoW.IsInGame)
										{
											Lua.DoString(loginLua);
											Thread.Sleep(2000);
										}
										BotBase bot = Util.GetBotByName(botName);
										if (bot != null)
										{
											if (ProfessionbuddyBot.Instance.SecondaryBot.Name != bot.Name)
												ProfessionbuddyBot.Instance.SecondaryBot = bot;
											if (!bot.Initialized)
												bot.DoInitialize();
											if (ProfessionBuddySettings.Instance.LastBotBase != bot.Name)
											{
												ProfessionBuddySettings.Instance.LastBotBase = bot.Name;
												ProfessionBuddySettings.Instance.Save();
											}
										}
										else
										{
											PBLog.Warn("Could not find bot with name {0}", botName);
										}
                                        TreeRoot.Start();
                                        ProfessionbuddyBot.Instance.OnTradeSkillsLoaded += ProfessionbuddyBot.Instance.Professionbuddy_OnTradeSkillsLoaded;
                                        ProfessionbuddyBot.Instance.LoadTradeSkills();
                                        _isSwitchingToons = false;
                                        ProfessionbuddyBot.Instance.IsRunning = true;
                                    }) {IsBackground = true}.Start();
                        }));
			TreeRoot.Stop();
		}


		/// <summary>
		///     Gets number of free slots in given gbank tab
		/// </summary>
		/// <param name="guildBankTab"></param>
		/// <param name="character">The character.</param>
		/// <param name="server">The server.</param>
		/// <returns></returns>
		public static int GBankTabFreeSlots(int guildBankTab, string character = null, string server = null)
		{
			int ret = 0;
			using (StyxWoW.Memory.AcquireFrame())
			{
				var guildProfileName = GetGuildProfileName(character, server);
				if (!string.IsNullOrEmpty(guildProfileName))
				{
					var lua = string.Format(GBankTabFreeSlotsFormat, guildProfileName, guildBankTab);
					ret = Lua.GetReturnVal<int>(lua, 0);
				}
			}
			return ret;
		}

		/// <summary>
		///     Gets total number of free slots in all the gbank tabs
		/// </summary>
		/// <param name="character">The character.</param>
		/// <param name="server">The server.</param>
		/// <returns></returns>
		public static int GBankTotalFreeSlots(string character = null, string server = null)
		{
			int ret = 0;
			using (StyxWoW.Memory.AcquireFrame())
			{
				var guildProfileName = GetGuildProfileName(character, server);
				if (!string.IsNullOrEmpty(guildProfileName))
				{
					var lua = string.Format(GBankTotalFreeSlotsFormat, guildProfileName);
					ret = Lua.GetReturnVal<int>(lua, 0);
				}
			}
			return ret;
		}

		/// <summary>
		///     Gets the number of guild bank tabs.
		/// </summary>
		/// <param name="character">The character.</param>
		/// <param name="server">The server.</param>
		/// <returns></returns>
		public static int GbankTabCount(string character = null, string server = null)
		{
			int ret = 0;
			using (StyxWoW.Memory.AcquireFrame())
			{
				var guildProfileName = GetGuildProfileName(character, server);
				if (!string.IsNullOrEmpty(guildProfileName))
				{
					var lua = string.Format(GbankTabCountFormat, guildProfileName);
					ret = Lua.GetReturnVal<int>(lua, 0);
				}
			}
			return ret;
		}

		private static string GetCharacterProfileName(string character = null, string server = null)
		{
			var hasDataStore = Lua.GetReturnVal<bool>("if DataStoreDB and DataStore_ContainersDB then return 1 else return 0 end", 0);
			if (hasDataStore)
			{
				if (string.IsNullOrEmpty(character))
					character = Lua.GetReturnVal<string>("return UnitName('player')", 0);

				if (string.IsNullOrEmpty(server))
					server = Lua.GetReturnVal<string>("return GetRealmName()", 0);

				List<string> profiles = Lua.GetReturnValues("local t={} for k,v in pairs(DataStoreDB.global.Characters) do table.insert(t,k) end return unpack(t)");
				return (from profile in profiles
						let elements = profile.Split('.')
						where
							elements[1].Equals(server, StringComparison.InvariantCultureIgnoreCase) &&
							elements[2].Equals(character, StringComparison.InvariantCultureIgnoreCase)
						select profile).FirstOrDefault();
			}
			return null;
		}

		private static string GetGuildProfileName(string character = null, string server = null)
		{
			var profile = GetCharacterProfileName(character, server);
			if (string.IsNullOrEmpty(profile))
				return null;
			string lua = string.Format("local val = DataStoreDB.global.Characters[\"{0}\"] if val and val.guildName then return val.guildName end return '' ", profile.ToFormatedUTF8());
			var guildName = Lua.GetReturnVal<string>(lua, 0);
			if (!string.IsNullOrEmpty(guildName))
				return guildName;
			return null;
		}

		public static int OnAhCount(uint itemId, string character = null, string server = null)
		{
			int ret = 0;
			using (StyxWoW.Memory.AcquireFrame())
			{
				var profile = GetCharacterProfileName(character, server);
				if (string.IsNullOrEmpty(profile))
					return 0;
				string lua = string.Format(
					"local char=DataStore_AuctionsDB.global.Characters[\"{0}\"] if char and char.Auctions then return #char.Auctions end return 0 ", profile.ToFormatedUTF8());
				var tableSize = Lua.GetReturnVal<int>(lua, 0);
				for (int i = 1; i <= tableSize; i++)
				{
					lua = string.Format("local char=DataStore_AuctionsDB.global.Characters[\"{0}\"] if char then return char.Auctions[{1}] end return '' ", profile.ToFormatedUTF8(), i);
					var aucStr = Lua.GetReturnVal<string>(lua, 0);
					string[] strs = aucStr.Split('|');
					int id = int.Parse(strs[1]);
					if (id == itemId)
					{
						int cnt = int.Parse(strs[2]);
						ret += cnt;
					}
				}
			}
			return ret;
		}

		public static int InGBankCount(uint itemId, string character = null, string server = null)
		{
			int ret = 0;
			using (StyxWoW.Memory.AcquireFrame())
			{
				var guildProfileName = GetGuildProfileName(character, server);
				if (!string.IsNullOrEmpty(guildProfileName))
				{
					var lua = string.Format(InGbankCountFormat, guildProfileName, itemId);
					ret = Lua.GetReturnVal<int>(lua, 0);
				}
			}
			return ret;
		}

		#region Nested type: TradeskillHelper

		public class TradeskillHelper
		{
			private readonly SkillLine _skillLine;
			private WoWSkill _wowSkill;

			public TradeskillHelper(SkillLine skillLine)
			{
				_skillLine = skillLine;
				_wowSkill = StyxWoW.Me.GetSkill(skillLine);
			}

			public uint Level
			{
				get
				{
					_wowSkill = StyxWoW.Me.GetSkill(_skillLine);
					return (uint) _wowSkill.CurrentValue;
				}
			}

			public uint MaxLevel
			{
				get
				{
					_wowSkill = StyxWoW.Me.GetSkill(_skillLine);
					return (uint) _wowSkill.MaxValue;
				}
			}

			public uint Bonus
			{
				get
				{
					_wowSkill = StyxWoW.Me.GetSkill(_skillLine);
					return _wowSkill.Bonus;
				}
			}

            public static uint CanRepeatNum(uint recipeID)
            {
                return
                    (from ts in ProfessionbuddyBot.Instance.TradeSkillList where ts.KnownRecipes.ContainsKey(recipeID) select ts.KnownRecipes[recipeID].CanRepeatNum)
                        .FirstOrDefault();
            }

            public static bool CanCraft(uint recipeID)
            {
                return (from ts in ProfessionbuddyBot.Instance.TradeSkillList
                        where ts.KnownRecipes.ContainsKey(recipeID)
                        select (ts.KnownRecipes[recipeID].Tools.Count(t => t.HasTool) == ts.KnownRecipes[recipeID].Tools.Count) && ts.KnownRecipes[recipeID].CanRepeatNum > 0)
                    .FirstOrDefault();
            }

			public static bool HasMats(uint recipeID)
			{
				return CanRepeatNum(recipeID) > 0;
			}

            public static bool HasRecipe(uint recipeID)
            {
                return ProfessionbuddyBot.Instance.TradeSkillList.Any(ts => ts.KnownRecipes.ContainsKey(recipeID));
            }

            public static bool HasTools(uint recipeID)
            {
                return (from ts in ProfessionbuddyBot.Instance.TradeSkillList
                        where ts.KnownRecipes.ContainsKey(recipeID)
                        select ts.KnownRecipes[recipeID].Tools.Count(t => t.HasTool) == ts.KnownRecipes[recipeID].Tools.Count).FirstOrDefault();
            }
        }

		#endregion
	}
}