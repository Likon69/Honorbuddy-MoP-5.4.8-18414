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
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Styx;
using Styx.CommonBot.Database;
using Styx.CommonBot.Profiles;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.DBC;

#endregion


namespace Honorbuddy.QuestBehaviorCore
{
	// TODO: This class still a work in progress...
	public class Query_Database
	{
		// From Highvoltz!
		// NB: The database is password protected, so you can't use the normal sqlite db viewer to look at it.
		// NB: To find information about the database, look at this: https://www.sqlite.org/faq.html#q7
		private static readonly HashSet<int> VendorBlacklist = new HashSet<int>();

		private static readonly SQLiteCommand SqlCmd_NearestVendor = 
			BuildSqLiteCommand("SELECT * FROM npcs"
								+ "WHERE MapId = @MAP_ID AND flag & @FLAG"
								+ " ORDER BY VECTORDISTANCE(x,y,z,@X,@Y,@Z) ASC");
	 
		private static readonly SQLiteCommand SqlCmd_NearestMailbox =
			BuildSqLiteCommand("SELECT * FROM mailbox"
								+ " WHERE map = @MAP_ID"
							   + " ORDER BY VECTORDISTANCE(x,y,z,@X,@Y,@Z) ASC");


		private static Mailbox FindNearestMailbox()
		{
			var results = new List<Mailbox>();

			WoWPoint loc = StyxWoW.Me.Location;
			using (SQLiteDataReader reader = GetSqliteDataReader(SqlCmd_NearestMailbox, StyxWoW.Me.MapId, loc.X, loc.Y, loc.Z))
			{
				while (reader.Read())
				{
					Mailbox result = GetMailbox(reader);
					var factionId = (uint)reader.GetInt32(reader.GetOrdinal("faction"));
					var factionTemplate = FactionTemplate.FromId(factionId);
					if ((factionTemplate == null || StyxWoW.Me.FactionTemplate.GetReactionTowards(factionTemplate) >= WoWUnitReaction.Neutral) && Navigator.CanNavigateFully(loc, result.Location))
					{
						results.Add(result);
						if (results.Count >= 5)
							break;
					}
				}
			}
			return results.Any() ? results.OrderBy(r => r.Location.DistanceSqr(loc)).FirstOrDefault() : null;
		}


		private static Vendor FindNearestVendor(Vendor.VendorType vendorType)
		{
			var results = new List<Vendor>();

			WoWPoint loc = StyxWoW.Me.Location;

			using (SQLiteDataReader reader = GetSqliteDataReader(SqlCmd_NearestVendor, StyxWoW.Me.MapId, (uint)vendorType.AsNpcFlag(), loc.X, loc.Y, loc.Z))
			{
				while (reader.Read())
				{
					Vendor result = GetVendor(reader, vendorType);
					var factionId = (uint)reader.GetInt32(reader.GetOrdinal("FactionId"));
					if (StyxWoW.Me.FactionTemplate.GetReactionTowards(FactionTemplate.FromId(factionId)) >= WoWUnitReaction.Neutral && !VendorBlacklist.Contains(result.Entry) &&
						Navigator.CanNavigateFully(loc, result.Location))
					{
						results.Add(result);
						if (results.Count >= 5)
							break;
					}
				}
			}
			return results.Any() ? results.OrderBy(r => r.Location.DistanceSqr(loc)).FirstOrDefault() : null;
		}


		private static Vendor GetVendor(IDataReader reader, Vendor.VendorType type)
		{
			int entry = reader.GetInt32(reader.GetOrdinal("entry"));
			var name = reader["name"] as string;
			float x = Convert.ToSingle(reader["x"].ToString().Replace(',', '.'), CultureInfo.InvariantCulture);
			float y = Convert.ToSingle(reader["y"].ToString().Replace(',', '.'), CultureInfo.InvariantCulture);
			float z = Convert.ToSingle(reader["z"].ToString().Replace(',', '.'), CultureInfo.InvariantCulture);
			return new Vendor(entry, name, type, new WoWPoint(x, y, z));
		}


		private static Mailbox GetMailbox(IDataReader reader)
		{
			float x = Convert.ToSingle(reader["x"].ToString().Replace(',', '.'), CultureInfo.InvariantCulture);
			float y = Convert.ToSingle(reader["y"].ToString().Replace(',', '.'), CultureInfo.InvariantCulture);
			float z = Convert.ToSingle(reader["z"].ToString().Replace(',', '.'), CultureInfo.InvariantCulture);
			return new Mailbox(new XElement("Mailbox", new XAttribute("x", x), new XAttribute("y", y), new XAttribute("z", z)));
		}


		private static SQLiteCommand BuildSqLiteCommand(string commandString)
		{
			var sqliteCmd = new SQLiteCommand(commandString, Connection.Instance);
			foreach (object match in Regex.Matches(commandString, @"@[\w_]*", RegexOptions.CultureInvariant))
			{
				sqliteCmd.Parameters.Add(new SQLiteParameter(match.ToString()));
			}
			return sqliteCmd;
		}


		private static SQLiteDataReader GetSqliteDataReader(SQLiteCommand command, params object[] args)
		{
			for (int i = 0; i < args.Length; i++)
			{
				command.Parameters[i].Value = args[i];
			}
			return command.ExecuteReader();
		}
	}
}
