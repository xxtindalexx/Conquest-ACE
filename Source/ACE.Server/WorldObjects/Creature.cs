using ACE.Common;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.WorldObjects.Entity;
using log4net;
using System;
using System.Collections.Generic;
using Position = ACE.Entity.Position;

namespace ACE.Server.WorldObjects
{
    public partial class Creature : Container
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public bool IsExhausted { get => Stamina.Current == 0; }

        protected QuestManager _questManager;

        public QuestManager QuestManager
        {
            get
            {
                if (_questManager == null)
                {
                    /*if (!(this is Player))
                        log.DebugFormat("Initializing non-player QuestManager for {0} (0x{1})", Name, Guid);*/

                    _questManager = new QuestManager(this);
                }

                return _questManager;
            }
        }

        /// <summary>
        /// A table of players who currently have their targeting reticule on this creature
        /// </summary>
        private Dictionary<uint, WorldObjectInfo> selectedTargets;

        /// <summary>
        /// Currently used to handle some edge cases for faction mobs
        /// DamageHistory.HasDamager() has the following issues:
        /// - if a player attacks a same-factioned mob but is evaded, the mob would quickly de-aggro
        /// - if a player attacks a same-factioned mob in a group of same-factioned mobs, the other nearby faction mobs should be alerted, and should maintain aggro, even without a DamageHistory entry
        /// - if a summoner attacks a same-factioned mob, should the summoned CombatPet possibly defend the player in that situation?
        /// </summary>
        //public HashSet<uint> RetaliateTargets { get; set; }

        // Enrage Leap Attack tracking
        public Position EnrageLeapTargetPosition;
        public string EnrageLeapTargetPlayerName;
        public double EnrageLeapLastTime;
        public bool EnrageLeapInProgress;

        // Enrage Grapple tracking (for exclusion from leap)
        public uint? EnrageGrappleTargetGuid;

        // Enrage Mirror Image tracking
        public bool EnrageMirrorImageTriggered;
        public List<Creature> EnrageMirrorImageClones = new List<Creature>();
        public bool EnrageMirrorImageImmune;
        public double EnrageMirrorImageLastCheck;

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Creature(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            InitializePropertyDictionaries();
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Creature(Biota biota) : base(biota)
        {
            InitializePropertyDictionaries();
            SetEphemeralValues();
        }

        private void InitializePropertyDictionaries()
        {
            if (Biota.PropertiesAttribute == null)
                Biota.PropertiesAttribute = new Dictionary<PropertyAttribute, PropertiesAttribute>();
            if (Biota.PropertiesAttribute2nd == null)
                Biota.PropertiesAttribute2nd = new Dictionary<PropertyAttribute2nd, PropertiesAttribute2nd>();
            if (Biota.PropertiesBodyPart == null)
                Biota.PropertiesBodyPart = new Dictionary<CombatBodyPart, PropertiesBodyPart>();
            if (Biota.PropertiesSkill == null)
                Biota.PropertiesSkill = new Dictionary<Skill, PropertiesSkill>();
        }

        private void SetEphemeralValues()
        {
            CombatMode = CombatMode.NonCombat;
            DamageHistory = new DamageHistory(this);

            if (!(this is Player))
                GenerateNewFace();

            // If any of the vitals don't exist for this biota, one will be created automatically in the CreatureVital ctor
            Vitals[PropertyAttribute2nd.MaxHealth] = new CreatureVital(this, PropertyAttribute2nd.MaxHealth);
            Vitals[PropertyAttribute2nd.MaxStamina] = new CreatureVital(this, PropertyAttribute2nd.MaxStamina);
            Vitals[PropertyAttribute2nd.MaxMana] = new CreatureVital(this, PropertyAttribute2nd.MaxMana);

            // If any of the attributes don't exist for this biota, one will be created automatically in the CreatureAttribute ctor
            Attributes[PropertyAttribute.Strength] = new CreatureAttribute(this, PropertyAttribute.Strength);
            Attributes[PropertyAttribute.Endurance] = new CreatureAttribute(this, PropertyAttribute.Endurance);
            Attributes[PropertyAttribute.Coordination] = new CreatureAttribute(this, PropertyAttribute.Coordination);
            Attributes[PropertyAttribute.Quickness] = new CreatureAttribute(this, PropertyAttribute.Quickness);
            Attributes[PropertyAttribute.Focus] = new CreatureAttribute(this, PropertyAttribute.Focus);
            Attributes[PropertyAttribute.Self] = new CreatureAttribute(this, PropertyAttribute.Self);

            foreach (var kvp in Biota.PropertiesSkill)
                Skills[kvp.Key] = new CreatureSkill(this, kvp.Key, kvp.Value);

            if (Health.Current <= 0)
                Health.Current = Health.MaxValue;
            if (Stamina.Current <= 0)
                Stamina.Current = Stamina.MaxValue;
            if (Mana.Current <= 0)
                Mana.Current = Mana.MaxValue;

            if (!(this is Player))
            {
                GenerateWieldList();

                EquipInventoryItems();

                GenerateWieldedTreasure();

                EquipInventoryItems();

                GenerateInventoryTreasure();

                // TODO: fix tod data
                Health.Current = Health.MaxValue;
                Stamina.Current = Stamina.MaxValue;
                Mana.Current = Mana.MaxValue;
            }

            SetMonsterState();

            CurrentMotionState = new Motion(MotionStance.NonCombat, MotionCommand.Ready);

            selectedTargets = new Dictionary<uint, WorldObjectInfo>();
        }

        // verify logic
        public bool IsNPC => !(this is Player) && !Attackable && TargetingTactic == TargetingTactic.None;

        public void GenerateNewFace()
        {
            var cg = DatManager.PortalDat.CharGen;

            if (!Heritage.HasValue)
            {
                if (!string.IsNullOrEmpty(HeritageGroupName) && Enum.TryParse(HeritageGroupName.Replace("'", ""), true, out HeritageGroup heritage))
                    Heritage = (int)heritage;
            }

            if (!Gender.HasValue)
            {
                if (!string.IsNullOrEmpty(Sex) && Enum.TryParse(Sex, true, out Gender gender))
                    Gender = (int)gender;
            }

            if (!Heritage.HasValue || !Gender.HasValue)
            {
#if DEBUG
                //if (!(NpcLooksLikeObject ?? false))
                    //log.DebugFormat("Creature.GenerateNewFace: {0} (0x{1}) - wcid {2} - Heritage: {3} | HeritageGroupName: {4} | Gender: {5} | Sex: {6} - Data missing or unparsable, Cannot randomize face.", Name, Guid, WeenieClassId, Heritage, HeritageGroupName, Gender, Sex);
#endif
                return;
            }

            if (!cg.HeritageGroups.TryGetValue((uint)Heritage, out var heritageGroup) || !heritageGroup.Genders.TryGetValue((int)Gender, out var sex))
            {
#if DEBUG
                log.DebugFormat("Creature.GenerateNewFace: {0} (0x{1}) - wcid {2} - Heritage: {3} | HeritageGroupName: {4} | Gender: {5} | Sex: {6} - Data invalid, Cannot randomize face.", Name, Guid, WeenieClassId, Heritage, HeritageGroupName, Gender, Sex);
#endif
                return;
            }

            PaletteBaseId = sex.BasePalette;

            var appearance = new Appearance
            {
                HairStyle = 1,
                HairColor = 1,
                HairHue = 1,

                EyeColor = 1,
                Eyes = 1,

                Mouth = 1,
                Nose = 1,

                SkinHue = 1
            };

            // Get the hair first, because we need to know if you're bald, and that's the name of that tune!
            if (sex.HairStyleList.Count > 1)
            {
                if (PropertyManager.GetBool("npc_hairstyle_fullrange"))
                    appearance.HairStyle = (uint)ThreadSafeRandom.Next(0, sex.HairStyleList.Count - 1);
                else
                    appearance.HairStyle = (uint)ThreadSafeRandom.Next(0, Math.Min(sex.HairStyleList.Count - 1, 8)); // retail range data compiled by OptimShi
            }
            else
                appearance.HairStyle = 0;

            if (sex.HairStyleList.Count < appearance.HairStyle)
            {
                log.Warn($"Creature.GenerateNewFace: {Name} (0x{Guid}) - wcid {WeenieClassId} - HairStyle = {appearance.HairStyle} | HairStyleList.Count = {sex.HairStyleList.Count} - Data invalid, Cannot randomize face.");
                return;
            }

            var hairstyle = sex.HairStyleList[Convert.ToInt32(appearance.HairStyle)];

            appearance.HairColor = (uint)ThreadSafeRandom.Next(0, sex.HairColorList.Count - 1);
            appearance.HairHue = ThreadSafeRandom.Next(0.0f, 1.0f);

            appearance.EyeColor = (uint)ThreadSafeRandom.Next(0, sex.EyeColorList.Count - 1);
            appearance.Eyes = (uint)ThreadSafeRandom.Next(0, sex.EyeStripList.Count - 1);

            appearance.Mouth = (uint)ThreadSafeRandom.Next(0, sex.MouthStripList.Count - 1);

            appearance.Nose = (uint)ThreadSafeRandom.Next(0, sex.NoseStripList.Count - 1);

            appearance.SkinHue = ThreadSafeRandom.Next(0.0f, 1.0f);

            //// Certain races (Undead, Tumeroks, Others?) have multiple body styles available. This is controlled via the "hair style".
            ////if (hairstyle.AlternateSetup > 0)
            ////    character.SetupTableId = hairstyle.AlternateSetup;

            if (!EyesTextureDID.HasValue)
                EyesTextureDID = sex.GetEyeTexture(appearance.Eyes, hairstyle.Bald);
            if (!DefaultEyesTextureDID.HasValue)
                DefaultEyesTextureDID = sex.GetDefaultEyeTexture(appearance.Eyes, hairstyle.Bald);
            if (!NoseTextureDID.HasValue)
                NoseTextureDID = sex.GetNoseTexture(appearance.Nose);
            if (!DefaultNoseTextureDID.HasValue)
                DefaultNoseTextureDID = sex.GetDefaultNoseTexture(appearance.Nose);
            if (!MouthTextureDID.HasValue)
                MouthTextureDID = sex.GetMouthTexture(appearance.Mouth);
            if (!DefaultMouthTextureDID.HasValue)
                DefaultMouthTextureDID = sex.GetDefaultMouthTexture(appearance.Mouth);
            if (!HeadObjectDID.HasValue)
                HeadObjectDID = sex.GetHeadObject(appearance.HairStyle);

            // Skin is stored as PaletteSet (list of Palettes), so we need to read in the set to get the specific palette
            var skinPalSet = DatManager.PortalDat.ReadFromDat<PaletteSet>(sex.SkinPalSet);
            if (!SkinPaletteDID.HasValue)
                SkinPaletteDID = skinPalSet.GetPaletteID(appearance.SkinHue);

            // Hair is stored as PaletteSet (list of Palettes), so we need to read in the set to get the specific palette
            var hairPalSet = DatManager.PortalDat.ReadFromDat<PaletteSet>(sex.HairColorList[Convert.ToInt32(appearance.HairColor)]);
            if (!HairPaletteDID.HasValue)
                HairPaletteDID = hairPalSet.GetPaletteID(appearance.HairHue);

            // Eye Color
            if (!EyesPaletteDID.HasValue)
                EyesPaletteDID = sex.EyeColorList[Convert.ToInt32(appearance.EyeColor)];
        }

        public virtual float GetBurdenMod()
        {
            return 1.0f;    // override for players
        }

        /// <summary>
        /// This will be false when creature is dead and waits for respawn
        /// </summary>
        public bool IsAlive { get => Health.Current > 0; }

        /// <summary>
        /// Sends the network commands to move a player towards an object
        /// </summary>
        public void MoveToObject(WorldObject target, float? useRadius = null)
        {
            var distanceToObject = useRadius ?? target.UseRadius ?? 0.6f;

            var moveToObject = new Motion(this, target, MovementType.MoveToObject);
            moveToObject.MoveToParameters.DistanceToObject = distanceToObject;

            // move directly to portal origin
            //if (target is Portal)
                //moveToObject.MoveToParameters.MovementParameters &= ~MovementParams.UseSpheres;

            SetWalkRunThreshold(moveToObject, target.Location);

            EnqueueBroadcastMotion(moveToObject);
        }

        /// <summary>
        /// Sends the network commands to move a player towards a position
        /// </summary>
        public void MoveToPosition(Position position)
        {
            var moveToPosition = new Motion(this, position);
            moveToPosition.MoveToParameters.DistanceToObject = 0.0f;

            SetWalkRunThreshold(moveToPosition, position);

            EnqueueBroadcastMotion(moveToPosition);
        }

        public void SetWalkRunThreshold(Motion motion, Position targetLocation)
        {
            // FIXME: WalkRunThreshold (default 15 distance) seems to not be used automatically by client
            // player will always walk instead of run, and if MovementParams.CanCharge is sent, they will always charge
            // to remedy this, we manually calculate a threshold based on WalkRunThreshold

            var dist = Location.DistanceTo(targetLocation);
            if (dist >= motion.MoveToParameters.WalkRunThreshold / 2.0f)     // default 15 distance seems too far, especially with weird in-combat walking animation?
            {
                motion.MoveToParameters.MovementParameters |= MovementParams.CanCharge;

                // TODO: find the correct runrate here
                // the default runrate / charge seems much too fast...
                //motion.RunRate = GetRunRate() / 4.0f;
                motion.RunRate = GetRunRate();
            }
        }

        /// <summary>
        /// This is raised by Player.HandleActionUseItem.<para />
        /// The item does not exist in the players possession.<para />
        /// If the item was outside of range, the player will have been commanded to move using DoMoveTo before ActOnUse is called.<para />
        /// When this is called, it should be assumed that the player is within range.
        /// 
        /// This is the OnUse method.   This is just an initial implemention.   I have put in the turn to action at this point.
        /// If we are out of use radius, move to the object.   Once in range, let's turn the creature toward us and get started.
        /// Note - we may need to make an NPC class vs monster as using a monster does not make them turn towrad you as I recall. Og II
        ///  Also, once we are reading in the emotes table by weenie - this will automatically customize the behavior for creatures.
        /// </summary>
        public override void ActOnUse(WorldObject worldObject)
        {
            // handled in base.OnActivate -> EmoteManager.OnUse()
        }

        public override void OnCollideObject(WorldObject target)
        {
            if (target.ReportCollisions == false)
                return;

            if (target is Door door)
                door.OnCollideObject(this);
            else if (target is Hotspot hotspot)
                hotspot.OnCollideObject(this);
        }

        /// <summary>
        /// Called when a player selects a target
        /// </summary>
        public bool OnTargetSelected(Player player)
        {
            return selectedTargets.TryAdd(player.Guid.Full, new WorldObjectInfo(player));
        }

        /// <summary>
        /// Called when a player deselects a target
        /// </summary>
        public bool OnTargetDeselected(Player player)
        {
            return selectedTargets.Remove(player.Guid.Full);
        }

        /// <summary>
        /// Called when a creature's health changes
        /// </summary>
        public void OnHealthUpdate()
        {
            foreach (var kvp in selectedTargets)
            {
                var player = kvp.Value.TryGetWorldObject() as Player;

                if (player?.Session != null)
                    QueryHealth(player.Session);
                else
                    selectedTargets.Remove(kvp.Key);
            }
        }

        /// <summary>
        /// Initiates enrage leap attack - selects target, announces, schedules leap
        /// Called from async task, so we defer everything via ActionChain to avoid thread conflicts
        /// </summary>
        public void TriggerEnrageLeap()
        {
            // Capture values before deferring
            var minRange = GetProperty(PropertyFloat.EnrageLeapMinRange) ?? 10.0f;
            var maxRange = GetProperty(PropertyFloat.EnrageLeapMaxRange) ?? 35.0f;
            var warningTime = GetProperty(PropertyFloat.EnrageLeapWarningTime) ?? 5.0f;
            var bossName = Name;

            // Defer all operations to game tick to avoid thread conflicts with physics tick
            var initChain = new ACE.Server.Entity.Actions.ActionChain();
            initChain.AddAction(this, ACE.Server.Entity.Actions.ActionType.MonsterCombat_SpawnHotspot, () =>
            {
                if (!IsAlive) return;

                // Get all players in range using GetPlayersInRange helper
                var allPlayers = GetPlayersInRange((double)maxRange);
                var players = new List<Player>();
                foreach (var player in allPlayers)
                {
                    var dist = Location.Distance2D(player.Location);
                    if (dist >= minRange && dist <= maxRange)
                    {
                        // Don't select players already targeted by grapple
                        if (EnrageGrappleTargetGuid.HasValue && player.Guid.Full == EnrageGrappleTargetGuid.Value)
                            continue;

                        players.Add(player);
                    }
                }

                if (players.Count == 0)
                    return;

                // Select random target
                var targetPlayer = players[ThreadSafeRandom.Next(0, players.Count - 1)];

                EnrageLeapTargetPosition = new Position(targetPlayer.Location);
                EnrageLeapTargetPlayerName = targetPlayer.Name;
                EnrageLeapInProgress = true;

                // Broadcast warning
                var msg = $"{bossName} begins to lunge toward {targetPlayer.Name}!";
                var nearbyPlayers = GetPlayersInRange(250.0);
                foreach (var player in nearbyPlayers)
                {
                    player.Session.Network.EnqueueSend(new Network.GameMessages.Messages.GameMessageSystemChat(msg, ChatMessageType.Combat));
                }

                // Spawn ground marker
                SpawnLeapGroundMarker(EnrageLeapTargetPosition);

                // Schedule leap execution
                var leapChain = new ACE.Server.Entity.Actions.ActionChain();
                leapChain.AddDelaySeconds(warningTime);
                leapChain.AddAction(this, ACE.Server.Entity.Actions.ActionType.MonsterCombat_SpawnHotspot, () => ExecuteEnrageLeap());
                leapChain.EnqueueChain();
            });
            initChain.EnqueueChain();
        }

        /// <summary>
        /// Spawns visual ground marker at leap target position
        /// Deferred to next tick to avoid collection modification during physics tick
        /// </summary>
        public void SpawnLeapGroundMarker(Position targetPos)
        {
            var warningTime = GetProperty(PropertyFloat.EnrageLeapWarningTime) ?? 5.0;
            var markerWCID = GetProperty(PropertyInt.EnrageGroundMarkerWCID) ?? 0;

            if (markerWCID <= 0)
                return;

            // Defer ground marker spawn to next tick to avoid collection modification
            var spawnChain = new ACE.Server.Entity.Actions.ActionChain();
            spawnChain.AddDelaySeconds(0.1);
            spawnChain.AddAction(this, ACE.Server.Entity.Actions.ActionType.MonsterCombat_SpawnHotspot, () =>
            {
                if (!IsAlive) return;

                var groundMarker = WorldObjectFactory.CreateNewWorldObject((uint)markerWCID);
                if (groundMarker == null) return;

                groundMarker.Location = new Position(targetPos);

                if (groundMarker.EnterWorld())
                {
                    groundMarker.PlayParticleEffect(PlayScript.Explode, groundMarker.Guid);

                    var despawnChain = new ACE.Server.Entity.Actions.ActionChain();
                    despawnChain.AddDelaySeconds(warningTime);
                    despawnChain.AddAction(groundMarker, ACE.Server.Entity.Actions.ActionType.WorldObject_Destroy, () =>
                    {
                        groundMarker.Destroy();
                    });
                    despawnChain.EnqueueChain();
                }
            });
            spawnChain.EnqueueChain();
        }

        /// <summary>
        /// Executes the leap - teleports boss, deals split damage, applies effects
        /// </summary>
        public void ExecuteEnrageLeap()
        {
            if (EnrageLeapTargetPosition == null)
            {
                EnrageLeapInProgress = false;
                return;
            }

            // VISUAL EFFECT SEQUENCE FOR JUMP

            // 1. Play takeoff effect (upward particle burst)
            var takeoffEffect = GetProperty(PropertyInt.EnrageLeapVisualEffect) ?? (int)PlayScript.Explode;
            PlayParticleEffect((PlayScript)takeoffEffect, Guid);

            // 2. Brief delay + invisibility to simulate flight
            var jumpChain = new ACE.Server.Entity.Actions.ActionChain();
            jumpChain.AddDelaySeconds(0.1); // Small delay for takeoff effect

            jumpChain.AddAction(this, ACE.Server.Entity.Actions.ActionType.WorldObjectNetworking_EnqueueMotion, () =>
            {
                // Make boss invisible during "jump"
                Hidden = true;
                EnqueueBroadcast(new Network.GameMessages.Messages.GameMessageScript(Guid, PlayScript.Hide));
            });

            jumpChain.AddDelaySeconds(0.3); // "Flight time"

            jumpChain.AddAction(this, ACE.Server.Entity.Actions.ActionType.MonsterCombat_SpawnHotspot, () =>
            {
                // Teleport while hidden (no portal effect)
                var newPos = new Position(EnrageLeapTargetPosition);
                FakeTeleport(newPos);

                // Reappear at destination
                Hidden = false;
                EnqueueBroadcast(new Network.GameMessages.Messages.GameMessageScript(Guid, PlayScript.UnHide));

                // Execute damage logic
                ExecuteLeapDamage();
            });

            jumpChain.EnqueueChain();
        }

        /// <summary>
        /// Executes leap damage, knockback, and visual effects
        /// </summary>
        private void ExecuteLeapDamage()
        {
            // Get damage properties
            var baseDamage = GetProperty(PropertyInt.EnrageLeapBaseDamage) ?? 5000;
            var radius = GetProperty(PropertyFloat.EnrageLeapRadius) ?? 6.0f;

            // Find all players in damage radius
            var allNearbyPlayers = GetPlayersInRange(250.0);
            var playersInRadius = new List<Player>();
            foreach (var player in allNearbyPlayers)
            {
                var dist = Location.Distance2D(player.Location);
                if (dist <= radius)
                    playersInRadius.Add(player);
            }

            if (playersInRadius.Count > 0)
            {
                // Calculate split damage (removed minimum enforcement per user request)
                var damagePerPlayer = baseDamage / playersInRadius.Count;

                // Deal damage to each player
                foreach (var player in playersInRadius)
                {
                    player.TakeDamage(this, DamageType.Bludgeon, damagePerPlayer);

                    // Send damage message
                    player.Session.Network.EnqueueSend(new Network.GameMessages.Messages.GameMessageSystemChat(
                        $"{Name}'s devastating leap strikes you for {damagePerPlayer} damage!", ChatMessageType.Combat));

                    // Apply knockback if enabled
                    if (GetProperty(PropertyBool.EnrageLeapKnockback) ?? false)
                    {
                        ApplyLeapKnockback(player);
                    }
                }

                // Broadcast result
                var broadcastMsg = $"{Name} crashes down with tremendous force, splitting {baseDamage} damage among {playersInRadius.Count} nearby players!";
                var broadcastPlayers = GetPlayersInRange(250.0);
                foreach (var player in broadcastPlayers)
                {
                    player.Session.Network.EnqueueSend(new Network.GameMessages.Messages.GameMessageSystemChat(broadcastMsg, ChatMessageType.Broadcast));
                }
            }

            // Visual effect on landing (dust/shockwave)
            var landingEffect = GetProperty(PropertyInt.EnrageLeapParticleEffect) ?? (int)PlayScript.Explode;
            PlayParticleEffect((PlayScript)landingEffect, Guid);

            // Cleanup
            EnrageLeapTargetPosition = null;
            EnrageLeapTargetPlayerName = null;
            EnrageLeapLastTime = Time.GetUnixTime();
            EnrageLeapInProgress = false;
        }

        /// <summary>
        /// Applies knockback effect to player - instant position change without portal space
        /// </summary>
        private void ApplyLeapKnockback(Player player)
        {
            var knockbackDistance = GetProperty(PropertyFloat.EnrageLeapKnockbackDistance) ?? 8.0f;

            // Calculate knockback vector (away from boss)
            var dx = player.Location.PositionX - Location.PositionX;
            var dy = player.Location.PositionY - Location.PositionY;
            var distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < 0.1) // Avoid division by zero if player exactly on boss
                return;

            // Normalize and apply knockback distance
            var normalizedX = dx / distance;
            var normalizedY = dy / distance;

            var newX = player.Location.PositionX + (normalizedX * knockbackDistance);
            var newY = player.Location.PositionY + (normalizedY * knockbackDistance);

            // Create new position
            var knockbackPos = new Position(player.Location);
            knockbackPos.PositionX = (float)newX;
            knockbackPos.PositionY = (float)newY;

            // INSTANT POSITION UPDATE (no portal space)
            player.Location = knockbackPos;
            player.SendUpdatePosition();

            // Play knockback visual effect on player
            var knockbackEffect = GetProperty(PropertyInt.EnrageLeapKnockbackEffect) ?? (int)PlayScript.Fizzle;
            player.PlayParticleEffect((PlayScript)knockbackEffect, player.Guid);

            // Optional: Brief stun/dazed effect
            var stunDuration = GetProperty(PropertyFloat.EnrageLeapStunDuration) ?? 0.0f;
            if (stunDuration > 0)
            {
                // Player can't move/attack for brief period
                player.IsBusy = true;

                var stunChain = new ACE.Server.Entity.Actions.ActionChain();
                stunChain.AddDelaySeconds(stunDuration);
                stunChain.AddAction(player, ACE.Server.Entity.Actions.ActionType.Player_SetNonBusy, () =>
                {
                    player.IsBusy = false;
                });
                stunChain.EnqueueChain();
            }

            player.Session.Network.EnqueueSend(new Network.GameMessages.Messages.GameMessageSystemChat(
                "You are knocked backwards by the impact!", ChatMessageType.Combat));
        }

        /// <summary>
        /// Triggers mirror image enrage - spawns clones, makes boss immune
        /// Called from async task, so we defer everything via ActionChain to avoid thread conflicts
        /// </summary>
        public void TriggerEnrageMirrorImage()
        {
            // Capture values before deferring
            var cloneCount = GetProperty(PropertyInt.EnrageMirrorImageCount) ?? 3;
            var spawnRadius = (float)(GetProperty(PropertyFloat.EnrageMirrorImageSpawnRadius) ?? 5.0);
            var immuneDuringClones = GetProperty(PropertyBool.EnrageMirrorImmuneDuringClones) ?? true;
            var bossName = Name;

            EnrageMirrorImageTriggered = true;

            // Defer all operations to game tick to avoid thread conflicts with physics tick
            var actionChain = new ACE.Server.Entity.Actions.ActionChain();
            actionChain.AddAction(this, ACE.Server.Entity.Actions.ActionType.MonsterCombat_SpawnHotspot, () =>
            {
                if (!IsAlive) return;

                // Make boss immune if configured
                if (immuneDuringClones)
                {
                    EnrageMirrorImageImmune = true;

                    var immuneMsg = $"{bossName} becomes invulnerable and splits into mirror images!";
                    var nearbyPlayers = GetPlayersInRange(250.0);
                    foreach (var player in nearbyPlayers)
                    {
                        player.Session.Network.EnqueueSend(new Network.GameMessages.Messages.GameMessageSystemChat(immuneMsg, ChatMessageType.Magic));
                    }
                }
                else
                {
                    var splitMsg = $"{bossName} splits into mirror images!";
                    var nearbyPlayers = GetPlayersInRange(250.0);
                    foreach (var player in nearbyPlayers)
                    {
                        player.Session.Network.EnqueueSend(new Network.GameMessages.Messages.GameMessageSystemChat(splitMsg, ChatMessageType.Magic));
                    }
                }

                // Spawn clones
                for (int i = 0; i < cloneCount; i++)
                {
                    SpawnMirrorImageClone(spawnRadius, i);
                }

                // Reset triggered flag to allow future triggers
                var resetChain = new ACE.Server.Entity.Actions.ActionChain();
                resetChain.AddDelaySeconds(5.0);
                resetChain.AddAction(this, ACE.Server.Entity.Actions.ActionType.MonsterCombat_SpawnHotspot, () =>
                {
                    EnrageMirrorImageTriggered = false;
                });
                resetChain.EnqueueChain();
            });
            actionChain.EnqueueChain();
        }

        /// <summary>
        /// Spawns a single mirror image clone
        /// Deferred to next tick to avoid collection modification during physics tick
        /// </summary>
        private void SpawnMirrorImageClone(float radius, int index)
        {
            // Calculate spawn position around boss in a circle
            var angle = (360.0f / (GetProperty(PropertyInt.EnrageMirrorImageCount) ?? 3)) * index;
            var radian = angle * (Math.PI / 180.0);

            var offsetX = radius * Math.Cos(radian);
            var offsetY = radius * Math.Sin(radian);

            var clonePos = new Position(Location);
            clonePos.PositionX += (float)offsetX;
            clonePos.PositionY += (float)offsetY;

            // Capture values needed for deferred spawn
            var healthPercent = GetProperty(PropertyInt.EnrageMirrorImageHealthPercent) ?? 50;
            var damagePercent = GetProperty(PropertyInt.EnrageMirrorImageDamagePercent) ?? 75;
            var maxHealth = Health.MaxValue;
            var bossName = Name;
            var wcid = WeenieClassId;

            // Defer clone spawn to next tick to avoid collection modification during physics tick
            var actionChain = new ACE.Server.Entity.Actions.ActionChain();
            actionChain.AddDelaySeconds(0.1);
            actionChain.AddAction(this, ACE.Server.Entity.Actions.ActionType.MonsterCombat_SpawnHotspot, () =>
            {
                if (!IsAlive) return;

                // Create clone using same WeenieClassId
                var clone = WorldObjectFactory.CreateNewWorldObject(wcid) as Creature;
                if (clone == null)
                {
                    log.Error($"[ENRAGE MIRROR IMAGE] Failed to create clone for {bossName} (WCID: {wcid})");
                    return;
                }

                // Set clone position
                clone.Location = clonePos;

                // Set HP to percentage of original
                var cloneMaxHP = (uint)((maxHealth * healthPercent) / 100);
                clone.Health.Current = cloneMaxHP;

                // Reduce damage output using DamageMod property
                if (damagePercent < 100)
                {
                    var damageMod = damagePercent / 100.0f;
                    clone.DamageMod = damageMod;
                }

                // Set clone name to indicate it's a mirror
                clone.Name = $"{bossName} (Mirror Image)";

                // Prevent clones from dropping loot
                clone.DeathTreasureType = null;
                clone.GeneratorProfiles = null;
                clone.NoCorpse = true;
                clone.TimeToRot = 10; // Corpse disappears quickly

                // Spawn clone into world
                clone.EnterWorld();

                // Track clone
                EnrageMirrorImageClones.Add(clone);

                // Visual effect on spawn
                clone.PlayParticleEffect(PlayScript.RestrictionEffectBlue, clone.Guid);

                log.Info($"[ENRAGE MIRROR IMAGE] Spawned clone #{index + 1} for {bossName} at {clonePos}");
            });
            actionChain.EnqueueChain();
        }
    }
}
