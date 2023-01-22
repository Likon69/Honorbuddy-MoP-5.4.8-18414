using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Bots.FishingBuddy
{
	partial class Coroutines
	{
		public static async Task<bool> HandleVendoring()
		{
			if ((Me.BagsFull || StyxWoW.Me.DurabilityPercent <= 0.2)
				&& BotPoi.Current.Type != PoiType.Kill
				&& BotPoi.Current.Type != PoiType.Mail
				&& BotPoi.Current.Type != PoiType.Repair
				&& BotPoi.Current.Type != PoiType.InnKeeper)
			{
				var profile = ProfileManager.CurrentOuterProfile;
				if (profile != null && profile.MailboxManager != null)
				{
					Mailbox mbox = profile.MailboxManager.GetClosestMailbox();
					if (mbox != null && !String.IsNullOrEmpty(CharacterSettings.Instance.MailRecipient))
					{
						BotPoi.Current = new BotPoi(mbox);
					}
					else
					{
						Vendor ven = ProfileManager.CurrentOuterProfile.VendorManager.GetClosestVendor();
						BotPoi.Current = ven != null
							? new BotPoi(ven, PoiType.Repair)
							: new BotPoi(PoiType.InnKeeper);
					}
				}
			}

			if (BotPoi.Current.Type == PoiType.Mail)
				return await MailItems();

			if (BotPoi.Current.Type == PoiType.Repair)
				return await VendorItems();

			if (BotPoi.Current.Type == PoiType.InnKeeper)
				return await Logout();

			return false;
		}

		public static async Task<bool> MailItems()
		{
			WoWPoint mboxLoc = BotPoi.Current.Location;
			var mailbox = ObjectManager.GetObjectsOfType<WoWGameObject>().
				FirstOrDefault(
					m => m.SubType == WoWGameObjectType.Mailbox &&
						m.Location.Distance(mboxLoc) < 10);

			if (mailbox == null)
			{
				if (Me.Location.DistanceSqr(BotPoi.Current.Location) > 4*4)
				{
					Flightor.MoveTo(BotPoi.Current.Location);
					return true;
				}

				var profile = ProfileManager.CurrentOuterProfile;
				if (profile != null)
					profile.MailboxManager.Blacklist.Add(BotPoi.Current.AsMailbox);

				BotPoi.Clear(string.Format("Unable to find mailbox @ {0}", BotPoi.Current.Location));
				return false;
			}
			if (!mailbox.WithinInteractRange)
				return await FlyTo(BotPoi.Current.Location);

			if (!mailbox.WithinInteractRange)
				return await FlyTo(mailbox.Location, mailbox.SafeName);

			if (!MailFrame.Instance.IsVisible)
			{
				mailbox.Interact();
				await CommonCoroutines.SleepForLagDuration();
				return true;
			}

			await Vendors.MailAllItemsCoroutine();

			Vendor ven = ProfileManager.CurrentOuterProfile.VendorManager.GetClosestVendor();
			BotPoi.Current = ven != null ? new BotPoi(ven, PoiType.Repair) : new BotPoi(PoiType.None);
			return true;
		}

		public static async Task<bool> VendorItems()
		{
			Vendor ven = BotPoi.Current.AsVendor;
			WoWUnit vendor = ObjectManager.GetObjectsOfType<WoWUnit>().
				FirstOrDefault(m => m.Entry == ven.Entry || m.Entry == ven.Entry2);

			if (vendor == null)
			{
				if (Me.Location.DistanceSqr(BotPoi.Current.Location) > 4*4)
					return await FlyTo(BotPoi.Current.Location, "Vendor");

				// just wait at location, maybe vendor got killed by opposite faction
				return true;
			}


			if (!vendor.WithinInteractRange)
				return await FlyTo(vendor.Location, vendor.SafeName);

			if (GossipFrame.Instance.IsVisible && GossipFrame.Instance.GossipOptionEntries != null)
			{
				var gossip = GossipFrame.Instance.GossipOptionEntries.FirstOrDefault(g => g.Type == GossipEntry.GossipEntryType.Vendor);
				if (gossip.Type == GossipEntry.GossipEntryType.Unknown)
				{
					TreeRoot.Stop(string.Format("{0} does not provide a vendor frame. ", vendor.SafeName));
					return true;
				}

				GossipFrame.Instance.SelectGossipOption(gossip.Index);
				await Coroutine.Sleep(new Random().Next(200, 500));
				return true;
			}

			if (!MerchantFrame.Instance.IsVisible)
			{
				vendor.Interact();
				await CommonCoroutines.SleepForLagDuration();
				return true;
			}

			// sell all poor and common items not in protected Items list.
			List<WoWItem> itemList = StyxWoW.Me.BagItems.Where(
				i => !ProtectedItemsManager.Contains(i.Entry) &&
					!i.IsSoulbound && !i.IsConjured &&
					(i.Quality == WoWItemQuality.Poor || i.Quality == WoWItemQuality.Common)).ToList();
			foreach (var item in itemList)
			{
				item.UseContainerItem();
				await Coroutine.Sleep(new Random().Next(200, 500));
			}
			MerchantFrame.Instance.RepairAllItems();
			BotPoi.Current = new BotPoi(PoiType.None);
			return true;
		}
	}
}