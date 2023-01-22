using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bots.DungeonBuddy.Enums;
using Buddy.Coroutines;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Bots.DungeonBuddy.Attributes;
using Bots.DungeonBuddy.Helpers;
namespace Bots.DungeonBuddy.Dungeon_Scripts.Mists_of_Pandaria
{
	class The_Frost_Lord_Ahune : Dungeon
	{

		#region Overrides of Dungeon

		public override uint DungeonId
		{
			get { return 286; }
		}

		private readonly WoWPoint _entrance = new WoWPoint(740.8317, 7013.667, -72.97391);
		public override WoWPoint Entrance
		{
			get { return _entrance; }
		}


		public override void RemoveTargetsFilter(List<WoWObject> units)
		{
			units.RemoveAll(
				ret =>
				{
					var unit = ret.ToUnit();
					if (unit != null) { }
					return false;
				});
		}

		public override void IncludeTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				var unit = obj as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == FrozenCoreId && unit.Attackable)
						outgoingunits.Add(unit);
				}
			}
		}

		public override void WeighTargetsFilter(List<Targeting.TargetPriority> units)
		{
			foreach (var priority in units)
			{
				var unit = priority.Object as WoWUnit;
				if (unit != null)
				{
					if (unit.Entry == AhuneId)
					{
						priority.Score -= 10000;
					}
					else if (unit.Entry == FrozenCoreId)
					{
						priority.Score += 10000;
					}
				}
			}
		}

		public override void IncludeLootTargetsFilter(List<WoWObject> incomingunits, HashSet<WoWObject> outgoingunits)
		{
			foreach (var obj in incomingunits)
			{
				var gObj = obj as WoWGameObject;
				if (gObj != null)
				{
					// chest that spawns after killing ahune
					if (gObj.Entry == IceChestId
						&& DungeonBuddySettings.Instance.LootMode != LootMode.Off
						&& gObj.CanUse())
					{
						outgoingunits.Add(gObj);
					}

				}
			}
		}

		#endregion

		#region corpse run behavior

		private readonly CircularQueue<WoWPoint> _tunnelPath = new CircularQueue<WoWPoint>()
													{
														new WoWPoint(563.1432f, 6943.247f, -1.951628f),
														new WoWPoint(568.424f, 6941.722f, -24.89723f),
														new WoWPoint(582.709f, 6936.663f, -38.11215f),
														new WoWPoint(602.8282f, 6915.195f, -45.36474f),
														new WoWPoint(611.301f, 6895.487f, -51.30582f),
														new WoWPoint(637.5763f, 6869.115f, -79.11536f),
														new WoWPoint(667.0811f, 6865.27f, -81.14445f),
														new WoWPoint(667.0811f, 6865.27f, -70.73276f),
													};

		readonly WoWPoint _corpseRunSubmergeStart = new WoWPoint(561.1754f, 6944.772f, 16.60149f);

		public override MoveResult MoveTo(WoWPoint location)
		{
			// Coilfang corpse run. 
			if (Me.IsGhost)
			{
				var myLoc = Me.Location;
				// Outside and above water logic
				if (Me.Z > 12)
				{
					// move to a point on the outside water surface just above underwater tunnel 
					if (myLoc.DistanceSqr(_corpseRunSubmergeStart) > 10 * 10)
						return Navigator.MoveTo(_corpseRunSubmergeStart);

					if (_tunnelPath.Peek() != _tunnelPath.First)
						_tunnelPath.CycleTo(_tunnelPath.First);

					// submerge. We can only break through the water's surface if player's vertical pitch is facing down. 
					if (!Me.IsSwimming)
						Lua.DoString("VehicleAimIncrement(-1)");
					else
						Navigator.PlayerMover.MoveTowards(_tunnelPath.First);
					return MoveResult.Moved;
				}
				// tunnel navigation.
				if (Me.IsSwimming)
				{
					var moveTo = _tunnelPath.Peek();
					if (myLoc.DistanceSqr(moveTo) < 5 * 5)
					{
						_tunnelPath.Dequeue();
						moveTo = _tunnelPath.Peek();
						// Tunnel path ends at the water surface of the underwater pool. Jump to walk on the surface.
						if (moveTo == _tunnelPath.First)
						{
							WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
							WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
							return MoveResult.Moved;
						}
					}
					Navigator.PlayerMover.MoveTowards(moveTo);
					return MoveResult.Moved;
				}

			}
			return base.MoveTo(location);
		}

		public override void OnEnter()
		{
			// if coming back from a DC and a ghost then make sure current point in _tunnelPath is closest one.
			if (Me.IsGhost && Me.IsSwimming)
			{
				var myLoc = Me.Location;
				_tunnelPath.CycleTo(_tunnelPath.OrderBy(loc => loc.DistanceSqr(myLoc)).FirstOrDefault());
			}
			base.OnEnter();
		}

		#endregion

		const uint SlipperyFloorBunnyId = 25952;
		const uint SpankTargetBunnyId = 26190;
		private const uint IceSpearBunnyId = 25985;
		private const uint IceStoneId = 187882;
		const uint FrozenCoreId = 25865;
		const uint AhuneId = 25740;
		private const uint PocketOfSnowId = 35512;
		private const uint IceChestId = 187892;
		private const uint SatchelId = 54536;

		LocalPlayer Me { get { return StyxWoW.Me; } }

		[EncounterHandler(0)]
		public Func<WoWUnit, Task<bool>> RootBehavior()
		{
			const uint bigIceBlockId = 188142;
			const uint iceBlockId = 188067;
			const uint iseStoneMountId = 188072;
			const uint extraBigIceBlockId = 195000;

			AddAvoidObject(ctx => true, 6, bigIceBlockId, extraBigIceBlockId);
			AddAvoidObject(ctx => true, 3, iceBlockId, iseStoneMountId);
			AddAvoidObject(ctx => true, 4, IceSpearBunnyId);

			return async npc =>
						{
							if (!Me.Combat)
							{
								var pocketOfSnow = Me.BagItems.FirstOrDefault(i => i.Entry == PocketOfSnowId || i.Entry == SatchelId);
								if (pocketOfSnow != null)
								{
									pocketOfSnow.UseContainerItem();
									if (!await Coroutine.Wait(2000, () => LootFrame.Instance.IsVisible))
										return false;
									LootFrame.Instance.LootAll();
									return true;
								}
							}
							return false;
						};
		}

		[ObjectHandler(187882, "Ice Stone", 100)]
		public async Task<bool> IceStoneBehavior(WoWGameObject iceStone)
		{
			if (!Me.IsLeader() || !Targeting.Instance.IsEmpty() || !iceStone.CanUse())
				return false;

			if (!ScriptHelpers.GroupMembers.All(g => g.Player != null && g.IsAlive && g.Location.DistanceSqr(Me.Location) < 45*45))
				return false;

			if (!iceStone.CanUseNow())
				return (await CommonCoroutines.MoveTo(iceStone.Location)).IsSuccessful();
			
			if (!GossipFrame.Instance.IsVisible)
			{
				await ScriptHelpers.StopMovingIfMoving();
				iceStone.Interact();
				return true;
			}
			GossipFrame.Instance.SelectGossipOption(0);
			return true;
		}
	}
}
