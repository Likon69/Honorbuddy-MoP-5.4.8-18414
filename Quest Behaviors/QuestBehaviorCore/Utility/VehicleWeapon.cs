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
using System;
using System.Collections.Generic;
using System.Linq;

using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    // 11Mar2013-04:41UTC chinajade
    public class VehicleWeapon : VehicleAbility
    {
        // 11Mar2013-04:41UTC chinajade
        public VehicleWeapon(int abilityIndex /*[1..12]*/,
                             WeaponArticulation weaponArticulation = null,
                             double? fixedMuzzleVelocity = null, double? gravity = null)
            : base(abilityIndex)
        {
            FixedMuzzleVelocity = fixedMuzzleVelocity;
            WeaponArticulation = weaponArticulation ?? new WeaponArticulation();

            MuzzleVelocityInFps =
                FixedMuzzleVelocity.HasValue
                ? FixedMuzzleVelocity.Value
                : double.NaN;   // "we don't know, yet"
            
            GravityInFpsSqr = 
                gravity.HasValue 
                ? gravity.Value 
                : 32.174;

            MeasuredMuzzleVelocity_Average = double.NaN;
            MeasuredMuzzleVelocity_Min = double.NaN;
            MeasuredMuzzleVelocity_Max = double.NaN;

            LogAbilityUse = false;
            LogWeaponFiringDetails = true;
        }

        public bool LogWeaponFiringDetails { get; set; }
        public double MuzzleVelocityInFps { get; private set; }
        public double GravityInFpsSqr { get; private set; }
        public double MeasuredMuzzleVelocity_Average { get; private set; }
        public double MeasuredMuzzleVelocity_Max { get; private set; }
        public double MeasuredMuzzleVelocity_Min { get; private set; }


        #region Private and Convenience variables
        private WoWPoint? AimedLocation { get; set; }
        private double? FixedMuzzleVelocity { get; set; }
        private LocalPlayer Me { get { return QuestBehaviorBase.Me; } }
        private readonly WaitTimer MissileWatchingTimer = new WaitTimer(TimeSpan.FromMilliseconds(1000));
        private int MuzzleVelocities_Count { get; set; }
        private double MuzzleVelocities_Summation { get; set; }
        private bool NeedsTestFire { get { return double.IsNaN(MuzzleVelocityInFps); } }
        private WeaponArticulation WeaponArticulation { get; set; }

        #endregion


        // 11Mar2013-04:41UTC chinajade
        private double? CalculateBallisticLaunchAngle(WoWPoint targetLocation)
        {
            if (targetLocation == WoWPoint.Empty)
                { return null; }

            double v0Sqr = MuzzleVelocityInFps * MuzzleVelocityInFps;
            double horizontalDistance = WoWMovement.ActiveMover.Location.Distance2D(targetLocation);
            double heightDiff = targetLocation.Z - WoWMovement.ActiveMover.Location.Z;

            double tmp1 = GravityInFpsSqr * (horizontalDistance * horizontalDistance);
            double tmp2 = 2 * heightDiff * v0Sqr;
            double radicalTerm = (v0Sqr * v0Sqr) - (GravityInFpsSqr * (tmp1 + tmp2));

            // If radicalTerm is negative, then both roots are imaginary...
            // This means that the muzzleVelocity is insufficient to hit the target
            // at the target's current distance.
            if (radicalTerm < 0)
                { return null; }

            radicalTerm = Math.Sqrt(radicalTerm);

            // Prefer the 'lower' angle, if its within the articulation range...
            double root = Math.Atan((v0Sqr - radicalTerm) / (GravityInFpsSqr * horizontalDistance));
            if (WeaponArticulation.IsWithinAzimuthLimits(root))
                { return root; }

            // First root provides no solution, try second root...
            root = Math.Atan((v0Sqr + radicalTerm) / (GravityInFpsSqr * horizontalDistance));
            if (WeaponArticulation.IsWithinAzimuthLimits(root))
                { return root; }

            // Both solutions are out of the vehicle's articulation capabilities, return "no solution"...
            return null;
        }


        // 11Mar2013-04:41UTC chinajade
        private double CalculateMuzzleVelocity()
        {
            double launchAngle = WeaponArticulation.AzimuthGet();
            double muzzleVelocity = 0.0;
            var spell = FindVehicleAbilitySpell();

            if ((spell != null) && (spell.SpellMissileId > 0))
            {
                IEnumerable<WoWMissile> firedMissileQuery =
                    from missile in WoWMissile.InFlightMissiles
                    where
                        (missile.CasterGuid == Me.TransportGuid)
                        && (missile.SpellId == spell.Id)
                    select missile;


                WoWMissile launchedMissile = firedMissileQuery.FirstOrDefault();
                /* N.B This has been commented out to fix momentary game 'freeze up' from framelock but has been left here for - 
                   future reference in the event this logic needs to be further repaired.

                // Launch missile, and wait until launch is observed;
                MissileWatchingTimer.Reset();
                 
                while ((launchedMissile == null) && !MissileWatchingTimer.IsFinished)
                {
                    // WoWMissiles are read directly from the games memory and are not stored in the 'ObjectManager' 
                    // ObjectManager.Update();
                    launchedMissile = firedMissileQuery.FirstOrDefault();
                }
                */

                // If we failed to see the missile, report error and move on...
                if (launchedMissile == null)
                {
                    QBCLog.Warning(
                        "Muzzle Velocity not calculated--"
                        + "Unable to locate projectile launched by Vehicle Ability button #{0}",
                        AbilityIndex);
                    return double.NaN;  // "we don't know"
                }

                // Initial velocity calculation...
                // * Accounts for uneven terrain
                //
                // v0 = sqrt((R^2 * g) / (R * sin(2*theta)  +  2 * h * cos^2(theta)))
                // where, R = range, g = grav const, h = drop height, theta = launch angle
                double R = launchedMissile.FirePosition.Distance2D(launchedMissile.ImpactPosition);
                double h = launchedMissile.FirePosition.Z - launchedMissile.ImpactPosition.Z;
                double sinTwoTheta = Math.Sin(2 * launchAngle);
                double cosTheta = Math.Cos(launchAngle);

                muzzleVelocity = Math.Sqrt(((R * R) * GravityInFpsSqr) / (R * sinTwoTheta + 2 * h * (cosTheta * cosTheta)));
            }

            return muzzleVelocity;
        }


        public TimeSpan CalculateTimeOfProjectileFlight(WoWPoint wowPoint)
        {
            var R = Me.Location.Distance2D(wowPoint);
            var launchAngle = CalculateBallisticLaunchAngle(wowPoint);

            if (!launchAngle.HasValue)
                { return TimeSpan.Zero; }

            var flightTime = TimeSpan.FromSeconds(R / (MuzzleVelocityInFps * Math.Cos(launchAngle.Value)));

            return flightTime;
        }


        // 11Mar2013-04:41UTC chinajade
        public bool IsWeaponReady()
        {
            return IsAbilityReady();
        }


        // 11Mar2013-04:41UTC chinajade
        public bool IsWeaponUsable()
        {
            return IsAbilityUsable();
        }


        // 11Mar2013-04:41UTC chinajade
        public bool WeaponAim(WoWObject selectedTarget)
        {
	        AimedLocation = null;
            // If target is moving, lead it...
            var wowUnit = selectedTarget as WoWUnit;
            if (Query.IsViable(wowUnit) && wowUnit.IsMoving)
            {
                var projectileFlightTime = CalculateTimeOfProjectileFlight(selectedTarget.Location);
                var anticipatedLocation = selectedTarget.AnticipatedLocation(projectileFlightTime);

                WoWMovement.StopFace(); 
                return WeaponAim(anticipatedLocation);
            }

            if (!Query.IsViable(selectedTarget))
            {
                QBCLog.Warning("No target for WeaponAim!");
                WoWMovement.StopFace();
                return false;
            }           

            if (!UtilAimPreReqsPassed())
                { return false; }

            // Show user what we're targeting...
            Utility.Target(selectedTarget);

			var spell = FindVehicleAbilitySpell();
			// Terrain is targeted when firing weapon.
			// Commented out until repairs are made.
			//if (spell != null && spell.CanTargetTerrain)
			//{
			//	// make sure target is within range of spell
			//	if ( Me.Location.Distance(selectedTarget.Location) > spell.MaxRange)
			//		return false;

			//	AimedLocation = selectedTarget.Location;
			//	return true;
			//}

            // Calculate the azimuth...
            // TODO: Take vehicle rotations (pitch, roll) into account
            var azimuth = CalculateBallisticLaunchAngle(selectedTarget.Location);

            //// Debugging--looking for pitch/roll contributions...
            //// NB: It currently looks like the GetWorldMatrix() does not populate enough info to make
            //// this calculation.
            //if (azimuth.HasValue)
            //{
            //    var pitch = StyxWoW.Memory.Read<float>(WoWMovement.ActiveMover.BaseAddress + 0x820 + 0x24);
            //    QBCLog.Debug("{0} {1:F3}/ {2:F3} pitch: {3:F3}", WoWMovement.ActiveMover.Name, azimuth, azimuth - pitch, pitch);

            //    QBCDebug.ShowVehicleArticulationChain(WoWMovement.ActiveMover);
            //}

            if (!azimuth.HasValue || !WeaponArticulation.AzimuthSet(azimuth.Value))
                { return false; }

            // For heading, we just face the location...
            if (!WeaponArticulation.HeadingSet(selectedTarget))
                { return false; }

            AimedLocation = selectedTarget.Location;
            return true;
        }


        // 11Mar2013-04:41UTC chinajade
        public bool WeaponAim(WoWPoint selectedLocation)
        {
			AimedLocation = null;
            if (selectedLocation == WoWPoint.Empty)
            {
                QBCLog.Warning("No target location for WeaponAim!");
                WoWMovement.StopFace();
                return false;
            }

            if (!UtilAimPreReqsPassed())
                { return false; }

			var spell = FindVehicleAbilitySpell();
			// No aiming is required if ability targets terrain using mouse cursor
			//if (spell != null && spell.CanTargetTerrain)
			//{
			//	// make sure target is within range of spell
			//	if (Me.Location.Distance(selectedLocation) > spell.MaxRange)
			//		return false;

			//	AimedLocation = selectedLocation;
			//	return true;
			//}

            // Calculate the azimuth...
            // TODO: Take vehicle rotations (pitch, roll) into account
            var azimuth = CalculateBallisticLaunchAngle(selectedLocation);
            if (!azimuth.HasValue || !WeaponArticulation.AzimuthSet(azimuth.Value))
                { return false; }

            // For heading, we just face the location...
            if (!WeaponArticulation.HeadingSet(selectedLocation))
                { return false; }

            AimedLocation = selectedLocation;
            return true;
        }


        // 11Mar2013-04:41UTC chinajade
        public bool WeaponFire()
        {
            if (!AimedLocation.HasValue)
            {
                QBCLog.MaintenanceError("Weapon {0} has not been aimed!", Name);
                return false;
            }

            var isWeaponUsed = UseAbility();

            if (isWeaponUsed)
            {
				// Commented out for now. Will be revisited when issue #HB-926 is worked on.
				//if (CanTargetTerrain)
				//{
				//	SpellManager.ClickRemoteLocation(AimedLocation.Value);
				//	return true;
				//}

                double instantaneousMuzzleVelocity = CalculateMuzzleVelocity();

                if (!double.IsNaN(instantaneousMuzzleVelocity))
                {
                    MuzzleVelocities_Summation += instantaneousMuzzleVelocity;
                    ++MuzzleVelocities_Count;
                    MeasuredMuzzleVelocity_Average = MuzzleVelocities_Summation / MuzzleVelocities_Count;

                    MuzzleVelocityInFps = FixedMuzzleVelocity.HasValue
                        ? FixedMuzzleVelocity.Value
                        : MeasuredMuzzleVelocity_Average;

                    if (double.IsNaN(MeasuredMuzzleVelocity_Min) || (instantaneousMuzzleVelocity < MeasuredMuzzleVelocity_Min))
                        { MeasuredMuzzleVelocity_Min = instantaneousMuzzleVelocity; }

                    if (double.IsNaN(MeasuredMuzzleVelocity_Max) || (instantaneousMuzzleVelocity > MeasuredMuzzleVelocity_Max))
                        { MeasuredMuzzleVelocity_Max = instantaneousMuzzleVelocity; }

                    if (LogWeaponFiringDetails)
                    {
                        QBCLog.DeveloperInfo(
                            "Weapon {1} fired:{0}"
                            + "  Angle: {2:F3}{0}"
                            + "  MuzzleVelocity: {3:F2} {4}{0}"
                            + "  MeasureMuzzleVelocities: {5:F2} instantaneous / {6:F2} avg / {7:F2} min / {8:F2} max{0}"
                            + "  Target Distance: {9:F2}{0}"
                            + "  Projectile Flight Time: {10}",
                            Environment.NewLine,
                            Name,
                            WeaponArticulation.AzimuthGet(),
                            MuzzleVelocityInFps,
                            (FixedMuzzleVelocity.HasValue ? "fixed" : "used"),
                            instantaneousMuzzleVelocity,
                            MeasuredMuzzleVelocity_Average,
                            MeasuredMuzzleVelocity_Min,
                            MeasuredMuzzleVelocity_Max,
                            Me.Location.Distance(AimedLocation.Value),
                            Utility.PrettyTime(CalculateTimeOfProjectileFlight(AimedLocation.Value), true, false));
                    }

                    AimedLocation = null;   // weapon fired--force re-aim for next shot
                }
            }

            return isWeaponUsed;
        }


        private bool UtilAimPreReqsPassed()
        {
            if (!Query.IsVehicleActionBarShowing())
            {
                QBCLog.Warning("Attempted to aim weapon while not in Vehicle!");
                return false;
            }

            // Test fire weapon, if needed.  This gets us the muzzle velocity...
            if (NeedsTestFire && IsWeaponReady())
            {
                // Lie about weapon being aimed...
                // NB:  S'ok, for test fire, we're after the measured muzzle velocity.
                AimedLocation = WoWPoint.Zero;
                WeaponFire();
                return false;
            }

            return true;
        }
    }
}
