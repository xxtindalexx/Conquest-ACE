using System;
using System.Collections.Generic;
using System.Numerics;

using log4net;

using ACE.Common;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Physics.Animation;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// A passive summonable creature
    /// </summary>
    public class Pet : Creature
    {
        public Player P_PetOwner;

        public PetDevice P_PetDevice;

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Mythic pet spell casting cooldowns
        private double lastHealCastTime = 0;
        private double lastStaminaCastTime = 0;

        public uint? PetDevice
        {
            get => GetProperty(PropertyInstanceId.PetDevice);
            set { if (value.HasValue) SetProperty(PropertyInstanceId.PetDevice, value.Value); else RemoveProperty(PropertyInstanceId.PetDevice); }
        }

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Pet(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Pet(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            Ethereal = true;
            RadarBehavior = ACE.Entity.Enum.RadarBehavior.ShowNever;
            ItemUseable = Usable.No;

            SuppressGenerateEffect = true;
        }

        public virtual bool? Init(Player player, PetDevice petDevice)
        {
            var result = HandleCurrentActivePet(player);

            if (result == null || !result.Value)
                return result;

            // get physics radius of player and pet
            var playerRadius = player.PhysicsObj.GetPhysicsRadius();
            var petRadius = GetPetRadius();

            var spawnDist = playerRadius + petRadius + MinDistance;

            if (IsPassivePet)
            {
                Location = player.Location.InFrontOf(spawnDist, true);

                TimeToRot = -1;
            }
            else
            {
                Location = player.Location.InFrontOf(spawnDist, false);
            }

            Location.LandblockId = new LandblockId(Location.GetCell());

            Name = player.Name + "'s " + Name;

            PetOwner = player.Guid.Full;
            P_PetOwner = player;

            // All pets don't leave corpses, this maybe should have been in data, but isn't so lets make sure its true.
            NoCorpse = true;

            var success = EnterWorld();

            if (!success)
            {
                player.SendTransientError($"Couldn't spawn {Name}");
                return false;
            }

            player.CurrentActivePet = this;

            petDevice.Pet = Guid.Full;
            PetDevice = petDevice.Guid.Full;
            P_PetDevice = petDevice;

            // Copy pet rarity and rating bonuses from device to creature
            // This ensures GetPetRatingBonus() can read from CurrentActivePet (the creature)
            var deviceRarity = petDevice.GetProperty(PropertyInt.PetRarity);
            if (deviceRarity != null)
                SetProperty(PropertyInt.PetRarity, deviceRarity.Value);

            var damageRating = petDevice.GetProperty(PropertyInt.PetBonusDamageRating);
            if (damageRating != null)
                SetProperty(PropertyInt.PetBonusDamageRating, damageRating.Value);

            var damageReductionRating = petDevice.GetProperty(PropertyInt.PetBonusDamageReductionRating);
            if (damageReductionRating != null)
                SetProperty(PropertyInt.PetBonusDamageReductionRating, damageReductionRating.Value);

            var critDamageRating = petDevice.GetProperty(PropertyInt.PetBonusCritDamageRating);
            if (critDamageRating != null)
                SetProperty(PropertyInt.PetBonusCritDamageRating, critDamageRating.Value);

            var critDamageReductionRating = petDevice.GetProperty(PropertyInt.PetBonusCritDamageReductionRating);
            if (critDamageReductionRating != null)
                SetProperty(PropertyInt.PetBonusCritDamageReductionRating, critDamageReductionRating.Value);


            if (IsPassivePet)
                nextSlowTickTime = Time.GetUnixTime();

            return true;
        }

        public bool? HandleCurrentActivePet(Player player)
        {
            if (PropertyManager.GetBool("pet_stow_replace"))
                return HandleCurrentActivePet_Replace(player);
            else
                return HandleCurrentActivePet_Retail(player);
        }

        public bool HandleCurrentActivePet_Replace(Player player)
        {
            // original ace logic
            if (player.CurrentActivePet == null)
                return true;

            if (player.CurrentActivePet is CombatPet)
            {
                // possibly add the ability to stow combat pets with passive pet devices here?
                player.SendTransientError($"{player.CurrentActivePet.Name} is already active");
                return false;
            }

            var stowPet = WeenieClassId == player.CurrentActivePet.WeenieClassId;

            // despawn passive pet
            player.CurrentActivePet.Destroy();

            return !stowPet;
        }

        public bool? HandleCurrentActivePet_Retail(Player player)
        {
            if (player.CurrentActivePet == null)
                return true;

            if (IsPassivePet)
            {
                // using a passive pet device
                // stow currently active passive/combat pet, as per retail
                // spawning the new passive pet requires another double click
                player.CurrentActivePet.Destroy();
            }
            else
            {
                // using a combat pet device
                if (player.CurrentActivePet is CombatPet)
                {
                    player.SendTransientError($"{player.CurrentActivePet.Name} is already active");
                }
                else
                {
                    // stow currently active passive pet
                    // stowing the currently active passive pet w/ a combat pet device will unfortunately start the cooldown timer (and decrease the structure?) on the combat pet device, as per retail
                    // spawning the combat pet will require another double click in ~45s, as per retail
                    player.CurrentActivePet.Destroy();

                    return null;
                }
            }
            return false;
        }

        /// <summary>
        /// Called 5x per second for passive pets
        /// </summary>
        public void Tick(double currentUnixTime)
        {
            NextMonsterTickTime = currentUnixTime + monsterTickInterval;

            if (IsMoving)
            {
                PhysicsObj.update_object();

                UpdatePosition_SyncLocation();

                SendUpdatePosition();
            }

            if (currentUnixTime >= nextSlowTickTime)
                SlowTick(currentUnixTime);
        }

        private const double slowTickSeconds = 1.0;
        private double nextSlowTickTime;

        /// <summary>
        /// Called 1x per second
        /// </summary>
        public void SlowTick(double currentUnixTime)
        {
            //Console.WriteLine($"{Name}.HeartbeatStatic({currentUnixTime})");

            nextSlowTickTime += slowTickSeconds;

            if (P_PetOwner?.PhysicsObj == null)
            {
                log.Error($"{Name} ({Guid}).SlowTick() - P_PetOwner: {P_PetOwner}, P_PetOwner.PhysicsObj: {P_PetOwner?.PhysicsObj}");
                Destroy();
                return;
            }

            var dist = GetCylinderDistance(P_PetOwner);

            if (dist > MaxDistance)
                Destroy();

            // Mythic pet spell casting logic
            TryMythicPetSpellCasting(currentUnixTime);

            if (!IsMoving && dist > MinDistance)
                StartFollow();
        }

        /// <summary>
        /// Checks if this is a Mythic pet and attempts to cast healing/stamina spells on owner
        /// </summary>
        private void TryMythicPetSpellCasting(double currentUnixTime)
        {
            // Only Mythic pets (rarity 4) can cast spells
            var petRarity = GetProperty(PropertyInt.PetRarity);
            if (petRarity == null || petRarity.Value != 4)
                return;

            // Don't cast if owner is null or dead
            if (P_PetOwner == null || P_PetOwner.IsDead)
                return;

            // Don't cast during PvP combat if configured
            if (PropertyManager.GetBool("pet_rating_bonuses_disabled_in_pvp") && P_PetOwner.PKTimerActive)
                return;

            var cooldownSeconds = PropertyManager.GetLong("pet_mythic_spell_cooldown");

            // Check if we should cast Heal Other
            var healthPercent = (float)P_PetOwner.Health.Current / (float)P_PetOwner.Health.MaxValue;
            if (healthPercent <= 0.5f) // 50% health threshold
            {
                var timeSinceLastHeal = currentUnixTime - lastHealCastTime;
                if (timeSinceLastHeal >= cooldownSeconds)
                {
                    CastHealOtherOnOwner();
                    lastHealCastTime = currentUnixTime;
                    return; // Only cast one spell per tick
                }
            }

            // Check if we should cast Stamina to Health Other
            var staminaPercent = (float)P_PetOwner.Stamina.Current / (float)P_PetOwner.Stamina.MaxValue;
            if (staminaPercent <= 0.5f) // 50% stamina threshold
            {
                var timeSinceLastStamina = currentUnixTime - lastStaminaCastTime;
                if (timeSinceLastStamina >= cooldownSeconds)
                {
                    CastStaminaOtherOnOwner();
                    lastStaminaCastTime = currentUnixTime;
                }
            }
        }

        /// <summary>
        /// Casts Heal spell on the pet's owner (Spell ID 1163)
        /// </summary>
        private void CastHealOtherOnOwner()
        {
            var spell = new Server.Entity.Spell(1163);

            // Cast the spell
            TryCastSpell(spell, P_PetOwner, this, tryResist: false);
        }

        /// <summary>
        /// Casts Stamina spell on the pet's owner (Spell ID 1185)
        /// </summary>
        private void CastStaminaOtherOnOwner()
        {
            var spell = new Server.Entity.Spell(1185);

            // Cast the spell
            TryCastSpell(spell, P_PetOwner, this, tryResist: false);
        }

        // if the passive pet is between min-max distance to owner,
        // it will turn and start running torwards its owner

        private const float MinDistance = 2.0f;
        private const float MaxDistance = 192.0f;

        private void StartFollow()
        {
            // similar to Monster_Navigation.StartTurn()

            //Console.WriteLine($"{Name}.StartFollow()");

            IsMoving = true;

            // broadcast to clients
            MoveTo(P_PetOwner, RunRate);

            // perform movement on server
            var mvp = new MovementParameters();
            mvp.DistanceToObject = MinDistance;
            mvp.WalkRunThreshold = 0.0f;

            //mvp.UseFinalHeading = true;

            PhysicsObj.MoveToObject(P_PetOwner.PhysicsObj, mvp);

            // prevent snap forward
            PhysicsObj.UpdateTime = Physics.Common.PhysicsTimer.CurrentTime;
        }

        /// <summary>
        /// Broadcasts passive pet movement to clients
        /// </summary>
        public override void MoveTo(WorldObject target, float runRate = 1.0f)
        {
            if (!IsPassivePet)
            {
                base.MoveTo(target, runRate);
                return;
            }

            if (MoveSpeed == 0.0f)
                GetMovementSpeed();

            var motion = new Motion(this, target, MovementType.MoveToObject);

            motion.MoveToParameters.MovementParameters |= MovementParams.CanCharge;
            motion.MoveToParameters.DistanceToObject = MinDistance;
            motion.MoveToParameters.WalkRunThreshold = 0.0f;

            motion.RunRate = RunRate;

            CurrentMotionState = motion;

            EnqueueBroadcastMotion(motion);
        }

        /// <summary>
        /// Called when the MoveTo process has completed
        /// </summary>
        public override void OnMoveComplete(WeenieError status)
        {
            //Console.WriteLine($"{Name}.OnMoveComplete({status})");

            if (!IsPassivePet)
            {
                base.OnMoveComplete(status);
                return;
            }

            if (status != WeenieError.None)
                return;

            PhysicsObj.CachedVelocity = Vector3.Zero;
            IsMoving = false;
        }

        public static Dictionary<uint, float> PetRadiusCache = new Dictionary<uint, float>();

        private float GetPetRadius()
        {
            if (PetRadiusCache.TryGetValue(WeenieClassId, out var radius))
                return radius;

            var setup = DatManager.PortalDat.ReadFromDat<SetupModel>(SetupTableId);

            var scale = ObjScale ?? 1.0f;

            return ProjectileRadiusCache[WeenieClassId] = setup.Spheres[0].Radius * scale;
        }
    }
}
