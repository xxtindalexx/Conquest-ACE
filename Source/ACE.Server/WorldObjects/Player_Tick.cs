using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Entity.PKQuests;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Sequence;
using ACE.Server.Network.Structure;
using ACE.Server.Physics;
using ACE.Server.Physics.Animation;
using ACE.Server.Physics.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        private readonly ActionQueue actionQueue = new ActionQueue();

        private int initialAge;
        private DateTime initialAgeTime;

        private const double ageUpdateInterval = 7;
        private double nextAgeUpdateTime;

        private double houseRentWarnTimestamp;
        private const double houseRentWarnInterval = 3600;

        public void Player_Tick(double currentUnixTime)
        {
            if (CharacterSaveFailed)
            {
                // Boot the player as their Character object is not saving properly
                if (!IsLoggingOut)
                {
                    log.Error($"{Session.Player.Name} | 0x{Guid} | Account: {Account.AccountName} - disconnected for CharacterSaveFailed");
                    //Session.SendCharacterError(CharacterError.AccountLogin); // forces client to error screen
                    Session.Terminate(SessionTerminationReason.CharacterSaveFailed, new GameMessageCharacterError(CharacterError.AccountLogin));
                    //Session.LogOffPlayer(true);
                    CharacterSaveFailed = false;
                }
                return;
            }

            if (BiotaSaveFailed)
            {
                // Boot the player as their Biota object is not saving properly
                if (!IsLoggingOut)
                {
                    log.Error($"{Session.Player.Name} | 0x{Guid} | Account: {Account.AccountName} - disconnected for BiotaSaveFailed");
                    //Session.SendCharacterError(CharacterError.AccountLogin); // forces client to error screen
                    Session.Terminate(SessionTerminationReason.BiotaSaveFailed, new GameMessageCharacterError(CharacterError.AccountLogin));
                    //Session.LogOffPlayer(true);
                    BiotaSaveFailed = false;
                }
                return;
            }

            actionQueue.RunActions();

            if (nextAgeUpdateTime <= currentUnixTime)
            {
                nextAgeUpdateTime = currentUnixTime + ageUpdateInterval;

                if (initialAgeTime == DateTime.MinValue)
                {
                    initialAge = Age ?? 1;
                    initialAgeTime = DateTime.UtcNow;
                }

                Age = initialAge + (int)(DateTime.UtcNow - initialAgeTime).TotalSeconds;

                // In retail, this is sent every 7 seconds. If you adjust ageUpdateInterval from 7, you'll need to re-add logic to send this every 7s (if you want to match retail)
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.Age, Age ?? 1));
            }

            if (FellowVitalUpdate && Fellowship != null)
            {
                Fellowship.OnVitalUpdate(this);
                FellowVitalUpdate = false;
            }

            if (House != null && PropertyManager.GetBool("house_rent_enabled"))
            {
                if (houseRentWarnTimestamp > 0 && currentUnixTime > houseRentWarnTimestamp)
                {
                    HouseManager.GetHouse(House.Guid.Full, (house) =>
                    {
                        if (house != null && house.HouseStatus == HouseStatus.Active && !house.SlumLord.IsRentPaid())
                            Session.Network.EnqueueSend(new GameMessageSystemChat($"Warning!  You have not paid your maintenance costs for the last {(house.IsApartment ? "90" : "30")} day maintenance period.  Please pay these costs by this deadline or you will lose your house, and all your items within it.", ChatMessageType.Broadcast));
                    });

                    houseRentWarnTimestamp = Time.GetFutureUnixTime(houseRentWarnInterval);
                }
                else if (houseRentWarnTimestamp == 0)
                    houseRentWarnTimestamp = Time.GetFutureUnixTime(houseRentWarnInterval);
            }
        }

        private static readonly TimeSpan MaximumTeleportTime = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Called every ~5 seconds for Players
        /// </summary>
        public override void Heartbeat(double currentUnixTime)
        {
            NotifyLandblocks();

            ManaConsumersTick();

            HandleTargetVitals();

            LifestoneProtectionTick();

            PK_DeathTick();

            // CONQUEST: Check PK-only dungeon enforcement
            PKDungeonEnforcementTick();

            // CONQUEST: Track time spent in PK dungeons for quests
            PKDungeonQuestTick();

            // CONQUEST: Check PvP custom augmentation mode timeout
            if (PropertyManager.GetBool("pvp_disable_custom_augs"))
                CheckPvPModeTimeout();

            GagsTick();

            PhysicsObj.ObjMaint.DestroyObjects();

            // Check if we're due for our periodic SavePlayer
            if (LastRequestedDatabaseSave == DateTime.MinValue)
                LastRequestedDatabaseSave = DateTime.UtcNow;

            if (LastRequestedDatabaseSave.AddSeconds(PlayerSaveIntervalSecs) <= DateTime.UtcNow)
                SavePlayerToDatabase();

            if (Teleporting && DateTime.UtcNow > Time.GetDateTimeFromTimestamp(LastTeleportStartTimestamp ?? 0).Add(MaximumTeleportTime))
            {
                if (Session != null)
                    Session.LogOffPlayer(true);
                else
                    LogOut();
            }

            base.Heartbeat(currentUnixTime);
        }

        public static float MaxSpeed = 50;
        public static float MaxSpeedSq = MaxSpeed * MaxSpeed;

        public static bool DebugPlayerMoveToStatePhysics { get; set; } = false;

        /// <summary>
        /// Flag indicates if player is doing full physics simulation
        /// </summary>
        public bool FastTick => IsPKType;

        /// <summary>
        /// For advanced spellcasting / players glitching around during powersliding,
        /// the reason for this retail bug is from 2 different functions for player movement
        /// 
        /// The client's self-player uses DoMotion/StopMotion
        /// The server and other players on the client use apply_raw_movement
        ///
        /// When a 3+ button powerslide is performed, this bugs out apply_raw_movement,
        /// and causes the player to spin in place. With DoMotion/StopMotion, it performs a powerslide.
        ///
        /// With this option enabled (retail defaults to false), the player's position on the server
        /// will match up closely with the player's client during powerslides.
        ///
        /// Since the client uses apply_raw_movement to simulate the movement of nearby players,
        /// the other players will still glitch around on screen, even with this option enabled.
        ///
        /// If you wish for the positions of other players to be less glitchy, the 'MoveToState_UpdatePosition_Threshold'
        /// can be lowered to achieve that
        /// </summary>

        public void OnMoveToState(MoveToState moveToState)
        {
            if (!FastTick)
                return;

            if (DebugPlayerMoveToStatePhysics)
                Console.WriteLine(moveToState.RawMotionState);

            if (RecordCast.Enabled)
                RecordCast.OnMoveToState(moveToState);

            if (!PhysicsObj.IsMovingOrAnimating)
                PhysicsObj.UpdateTime = PhysicsTimer.CurrentTime;

            if (!PropertyManager.GetBool("client_movement_formula") || moveToState.StandingLongJump)
                OnMoveToState_ServerMethod(moveToState);
            else
                OnMoveToState_ClientMethod(moveToState);

            if (MagicState.IsCasting && MagicState.PendingTurnRelease && moveToState.RawMotionState.TurnCommand == 0)
                OnTurnRelease();
        }

        public void OnMoveToState_ClientMethod(MoveToState moveToState)
        {
            var rawState = moveToState.RawMotionState;
            var prevState = LastMoveToState?.RawMotionState ?? Network.Structure.RawMotionState.None;

            var mvp = new Physics.Animation.MovementParameters();
            mvp.HoldKeyToApply = rawState.CurrentHoldKey;

            if (!PhysicsObj.IsMovingOrAnimating)
                PhysicsObj.UpdateTime = PhysicsTimer.CurrentTime;

            // ForwardCommand
            if (rawState.ForwardCommand != MotionCommand.Invalid)
            {
                // press new key
                if (prevState.ForwardCommand == MotionCommand.Invalid)
                {
                    PhysicsObj.DoMotion((uint)MotionCommand.Ready, mvp);
                    PhysicsObj.DoMotion((uint)rawState.ForwardCommand, mvp);
                }
                // press alternate key
                else if (prevState.ForwardCommand != rawState.ForwardCommand)
                {
                    PhysicsObj.DoMotion((uint)rawState.ForwardCommand, mvp);
                }
            }
            else if (prevState.ForwardCommand != MotionCommand.Invalid)
            {
                // release key
                PhysicsObj.StopMotion((uint)prevState.ForwardCommand, mvp, true);
            }

            // StrafeCommand
            if (rawState.SidestepCommand != MotionCommand.Invalid)
            {
                // press new key
                if (prevState.SidestepCommand == MotionCommand.Invalid)
                {
                    PhysicsObj.DoMotion((uint)rawState.SidestepCommand, mvp);
                }
                // press alternate key
                else if (prevState.SidestepCommand != rawState.SidestepCommand)
                {
                    PhysicsObj.DoMotion((uint)rawState.SidestepCommand, mvp);
                }
            }
            else if (prevState.SidestepCommand != MotionCommand.Invalid)
            {
                // release key
                PhysicsObj.StopMotion((uint)prevState.SidestepCommand, mvp, true);
            }

            // TurnCommand
            if (rawState.TurnCommand != MotionCommand.Invalid)
            {
                // press new key
                if (prevState.TurnCommand == MotionCommand.Invalid)
                {
                    PhysicsObj.DoMotion((uint)rawState.TurnCommand, mvp);
                }
                // press alternate key
                else if (prevState.TurnCommand != rawState.TurnCommand)
                {
                    PhysicsObj.DoMotion((uint)rawState.TurnCommand, mvp);
                }
            }
            else if (prevState.TurnCommand != MotionCommand.Invalid)
            {
                // release key
                PhysicsObj.StopMotion((uint)prevState.TurnCommand, mvp, true);
            }
        }

        public void OnMoveToState_ServerMethod(MoveToState moveToState)
        {
            var minterp = PhysicsObj.get_minterp();
            minterp.RawState.SetState(moveToState.RawMotionState);

            if (moveToState.StandingLongJump)
            {
                minterp.RawState.ForwardCommand = (uint)MotionCommand.Ready;
                minterp.RawState.SideStepCommand = 0;
            }

            var allowJump = MotionInterp.motion_allows_jump(minterp.InterpretedState.ForwardCommand) == WeenieError.None;

            //PhysicsObj.cancel_moveto();

            minterp.apply_raw_movement(true, allowJump);
        }

        public bool InUpdate;

        public override bool UpdateObjectPhysics()
        {
            try
            {
                stopwatch.Restart();

                bool landblockUpdate = false;

                InUpdate = true;

                // update position through physics engine
                if (RequestedLocation != null)
                {
                    landblockUpdate = UpdatePlayerPosition(RequestedLocation);
                    RequestedLocation = null;
                }

                if (FastTick && PhysicsObj.IsMovingOrAnimating || PhysicsObj.Velocity != Vector3.Zero)
                {
                    UpdatePlayerPhysics();

                    if (MoveToParams?.Callback != null && !PhysicsObj.IsMovingOrAnimating)
                        HandleMoveToCallback();
                }

                InUpdate = false;

                return landblockUpdate;
            }
            finally
            {
                var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Player_Tick_UpdateObjectPhysics, elapsedSeconds);
                if (elapsedSeconds >= 1) // Yea, that ain't good....
                    log.Warn($"[PERFORMANCE][PHYSICS] {Guid}:{Name} took {(elapsedSeconds * 1000):N1} ms to process UpdateObjectPhysics() at loc: {Location}");
                else if (elapsedSeconds >= 0.010)
                    log.DebugFormat("[PERFORMANCE][PHYSICS] {0}:{1} took {2:N1} ms to process UpdateObjectPhysics() at loc: {3}", Guid, Name, (elapsedSeconds * 1000), Location);
            }
        }

        public void UpdatePlayerPhysics()
        {
            if (DebugPlayerMoveToStatePhysics)
                Console.WriteLine($"{Name}.UpdatePlayerPhysics({PhysicsObj.PartArray.Sequence.CurrAnim.Value.Anim.ID:X8})");

            //Console.WriteLine($"{PhysicsObj.Position.Frame.Origin}");
            //Console.WriteLine($"{PhysicsObj.Position.Frame.get_heading()}");

            PhysicsObj.update_object();

            // sync ace position?
            Location.Rotation = PhysicsObj.Position.Frame.Orientation;

            if (!FastTick) return;

            // ensure PKLogout position is synced up for other players
            if (PKLogout)
            {
                EnqueueBroadcast(new GameMessageUpdateMotion(this, new Motion(MotionStance.NonCombat, MotionCommand.Ready)));
                PhysicsObj.StopCompletely(true);

                if (!PhysicsObj.IsMovingOrAnimating)
                {
                    SyncLocation(Location.Variation);
                    EnqueueBroadcast(new GameMessageUpdatePosition(this));
                }
            }

            // this fixes some differences between client movement (DoMotion/StopMotion) and server movement (apply_raw_movement)
            //
            // scenario: start casting a self-spell, and then immediately start holding the run forward key during the windup
            // on client: player will start running forward after the cast has completed
            // on server: player will stand still

            // this block of code can improve the sync between these 2 methods,
            // however there are some bugs that originate in acclient that cannot be resolved on the server
            // for example, equip a wand, and then start running forward in non-combat mode. switch to magic combat mode, and then release forward during the stance swap
            // the client will never send a 'client released forward' MoveToState in this scenario unfortunately.
            // because of this, it's better to have the 'client blip forward' bug without it, than to have the client invisibly running forward on the server.
            // commenting out this block because of this...

            /*if (!PhysicsObj.IsMovingOrAnimating && LastMoveToState != null)
            {
                // apply latest MoveToState, if applicable
                //if ((LastMoveToState.RawMotionState.Flags & (RawMotionFlags.ForwardCommand | RawMotionFlags.SideStepCommand | RawMotionFlags.TurnCommand)) != 0)
                if ((LastMoveToState.RawMotionState.Flags & RawMotionFlags.ForwardCommand) != 0 && LastMoveToState.RawMotionState.ForwardHoldKey == HoldKey.Invalid)
                {
                    if (DebugPlayerMoveToStatePhysics)
                        Console.WriteLine("Re-applying movement: " + LastMoveToState.RawMotionState.Flags);

                    OnMoveToState(LastMoveToState);

                    // re-broadcast MoveToState to other clients only
                    EnqueueBroadcast(false, new GameMessageUpdateMotion(this, CurrentMovementData));
                }
                LastMoveToState = null;
            }*/

            if (MagicState.IsCasting && MagicState.PendingTurnRelease)
                CheckTurn();
        }

        /// <summary>
        /// The maximum rate UpdatePosition packets from MoveToState will be broadcast for each player
        /// AutonomousPosition still always broadcasts UpdatePosition
        ///  
        /// The default value (1 second) was estimated from this retail video:
        /// https://youtu.be/o5lp7hWhtWQ?t=112
        /// 
        /// If you wish for players to glitch around less during powerslides, lower this value
        /// </summary>
        public static TimeSpan MoveToState_UpdatePosition_Threshold = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Used by physics engine to actually update a player position
        /// Automatically notifies clients of updated position
        /// </summary>
        /// <param name="newPosition">The new position being requested, before verification through physics engine</param>
        /// <returns>TRUE if object moves to a different landblock</returns>
        public bool UpdatePlayerPosition(ACE.Entity.Position newPosition, bool forceUpdate = false)
        {
            //Console.WriteLine($"{Name}.UpdatePlayerPosition({newPosition}, {forceUpdate}, {Teleporting})");
            bool verifyContact = false;

            // possible bug: while teleporting, client can still send AutoPos packets from old landblock
            if (Teleporting && !forceUpdate) return false;
            if (!Teleporting && Location.Variation != null && newPosition.Variation == null) //do not wipe out the prior Variation unless teleporting
            {
                newPosition.Variation = Location.Variation;
            }

            // pre-validate movement
            if (!ValidateMovement(newPosition))
            {
                log.Error($"{Name}.UpdatePlayerPosition() - movement pre-validation failed from {Location} to {newPosition}, t: {Teleporting}");
                //log.Error($"{new StackTrace()}");
                return false;
            }

            bool variationChange = Location.Variation != newPosition.Variation;

            try
            {
                if (!forceUpdate) // This is needed beacuse this function might be called recursively
                    stopwatch.Restart();

                var success = true;

                if (PhysicsObj != null)
                {
                    var distSq = Location.SquaredDistanceTo(newPosition);

                    if (distSq > PhysicsGlobals.EpsilonSq || variationChange)
                    {
                        /*var p = new Physics.Common.Position(newPosition);
                        var dist = PhysicsObj.Position.Distance(p);
                        Console.WriteLine($"Dist: {dist}");*/

                        //if (newPosition.Landblock == 0x18A && Location.Landblock != 0x18A)
                        //    log.Info($"{Name} is getting swanky");

                        if (!Teleporting)
                        {
                            var blockDist = PhysicsObj.GetBlockDist(Location.Cell, newPosition.Cell);

                            // verify movement
                            if (distSq > MaxSpeedSq && blockDist > 1)
                            {
                                //Session.Network.EnqueueSend(new GameMessageSystemChat("Movement error", ChatMessageType.Broadcast));
                                log.Warn($"MOVEMENT SPEED: {Name} trying to move from {Location} to {newPosition}, speed: {Math.Sqrt(distSq)}");
                                return false;
                            }

                            // verify z-pos
                            if (blockDist == 0 && LastGroundPos != null && newPosition.PositionZ - LastGroundPos.PositionZ > 10 && DateTime.UtcNow - LastJumpTime > TimeSpan.FromSeconds(1) && GetCreatureSkill(Skill.Jump).Current < 1000)
                                verifyContact = true;
                        }

                        var curCell = LScape.get_landcell(newPosition.Cell, newPosition.Variation);
                        if (curCell != null)
                        {
                            //if (PhysicsObj.CurCell == null || curCell.ID != PhysicsObj.CurCell.ID)
                            //PhysicsObj.change_cell_server(curCell);
                            //Console.WriteLine($"{Name} Destination Cell {newPosition.Cell}, v: {curCell.VariationId}");
                            PhysicsObj.set_request_pos(newPosition.Pos, newPosition.Rotation, curCell, Location.LandblockId.Raw, newPosition.Variation);
                            if (FastTick)
                                success = PhysicsObj.update_object_server_new();
                            else
                                success = PhysicsObj.update_object_server();

                            if (PhysicsObj.CurCell == null && curCell.ID >> 16 != 0x18A)
                            {
                                PhysicsObj.CurCell = curCell;
                            }

                            if (verifyContact && IsJumping)
                            {
                                var blockDist = PhysicsObj.GetBlockDist(newPosition.Cell, LastGroundPos.Cell);

                                if (blockDist <= 1)
                                {
                                    log.Warn($"z-pos hacking detected for {Name}, lastGroundPos: {LastGroundPos.ToLOCString()} - requestPos: {newPosition.ToLOCString()}");
                                    Location = new ACE.Entity.Position(LastGroundPos);
                                    Sequences.GetNextSequence(SequenceType.ObjectForcePosition);
                                    SendUpdatePosition();
                                    return false;
                                }
                            }

                            CheckMonsters();
                        }
                    }
                    else
                        PhysicsObj.Position.Frame.Orientation = newPosition.Rotation;
                }

                // double update path: landblock physics update -> updateplayerphysics() -> update_object_server() -> Teleport() -> updateplayerphysics() -> return to end of original branch
                if (Teleporting && !forceUpdate) return true;

                if (!success) return false;

                var landblockUpdate = (Location.Cell >> 16 != newPosition.Cell >> 16) || variationChange;

                // CONQUEST: Store old landblock info before updating Location
                var oldLandblock = (ushort)(Location.Cell >> 16);
                // Get old variation from CurrentLandblock instead of Location.Variation (which can be reset to 0)
                var oldVariation = CurrentLandblock?.VariationId ?? Location.Variation ?? 0;

                Location = new ACE.Entity.Position(newPosition);

                // CONQUEST: Check if entering/exiting PK dungeon landblock (if pvp_disable_custom_augs is enabled)
                if (landblockUpdate && PropertyManager.GetBool("pvp_disable_custom_augs"))
                {
                    var newLandblock = (ushort)(newPosition.Cell >> 16);
                    var newVariation = newPosition.Variation ?? 0;

                    var wasInPKDungeon = ACE.Server.Entity.Landblock.pkDungeonLandblocks.Contains((oldLandblock, oldVariation));
                    var nowInPKDungeon = ACE.Server.Entity.Landblock.pkDungeonLandblocks.Contains((newLandblock, newVariation));

                    //log.Info($"[PK DUNGEON DEBUG] {Name} - Landblock change detected. Old: 0x{oldLandblock:X4} v{oldVariation} -> New: 0x{newLandblock:X4} v{newVariation}. WasInPK: {wasInPKDungeon}, NowInPK: {nowInPKDungeon}, InPKDungeonMode:{ InPKDungeonMode}");
                    if (!wasInPKDungeon && nowInPKDungeon)
                    {
                        // Entering PK dungeon - strip augs and rebuff
                        //log.Info($"[PK DUNGEON DEBUG] {Name} - Calling EnterPKDungeonMode()");
                        EnterPKDungeonMode();
                    }
                    else if (wasInPKDungeon && !nowInPKDungeon)
                    {
                        // Exiting PK dungeon - restore augs and rebuff
                        //log.Info($"[PK DUNGEON DEBUG] {Name} - Calling ExitPKDungeonMode()");
                        ExitPKDungeonMode();
                    }
                }

                if (RecordCast.Enabled)
                    RecordCast.Log($"CurPos: {Location.ToLOCString()}");

                if (RequestedLocationBroadcast || DateTime.UtcNow - LastUpdatePosition >= MoveToState_UpdatePosition_Threshold)
                    SendUpdatePosition();
                else
                    Session.Network.EnqueueSend(new GameMessageUpdatePosition(this));

                if (!InUpdate)
                    LandblockManager.RelocateObjectForPhysics(this, true);

                return landblockUpdate;
            }
            finally
            {
                if (!forceUpdate) // This is needed beacuse this function might be called recursively
                {
                    var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                    ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Player_Tick_UpdateObjectPhysics, elapsedSeconds);
                    if (elapsedSeconds >= 0.100) // Yea, that ain't good....
                        log.Warn($"[PERFORMANCE][PHYSICS] {Guid}:{Name} took {(elapsedSeconds * 1000):N1} ms to process UpdatePlayerPosition() at loc: {Location}");
                    else if (elapsedSeconds >= 0.010)
                        log.Debug($"[PERFORMANCE][PHYSICS] {Guid}:{Name} took {(elapsedSeconds * 1000):N1} ms to process UpdatePlayerPosition() at loc: {Location}");
                }
            }
        }

        private static HashSet<uint> buggedCells = new HashSet<uint>()
        {
            0xD6990112,
            0xD599012C
        };

        public bool ValidateMovement(ACE.Entity.Position newPosition)
        {
            if (CurrentLandblock == null)
                return false;

            if (!Teleporting && Location.Landblock != newPosition.Cell >> 16)
            {
                if ((Location.Cell & 0xFFFF) >= 0x100 && (newPosition.Cell & 0xFFFF) >= 0x100)
                {
                    if (!buggedCells.Contains(Location.Cell) || !buggedCells.Contains(newPosition.Cell))
                        return false;
                }

                if (CurrentLandblock.IsDungeon)
                {
                    var destBlock = LScape.get_landblock(newPosition.Cell, newPosition.Variation);
                    if (destBlock != null && destBlock.IsDungeon)
                        return false;
                }
            }
            return true;
        }


        public bool SyncLocationWithPhysics()
        {
            if (PhysicsObj.CurCell == null)
            {
                Console.WriteLine($"{Name}.SyncLocationWithPhysics(): CurCell is null!");
                return false;
            }

            var blockcell = PhysicsObj.Position.ObjCellID;
            var pos = PhysicsObj.Position.Frame.Origin;
            var rotate = PhysicsObj.Position.Frame.Orientation;
            var variation = PhysicsObj.Position.Variation;

            var landblockUpdate = blockcell << 16 != CurrentLandblock.Id.Landblock;

            Location = new ACE.Entity.Position(blockcell, pos, rotate, variation);

            return landblockUpdate;
        }

        /// <summary>
        /// CONQUEST: Enforces PK-only dungeon restrictions
        /// Boots NPK players out of PK-only dungeons every heartbeat (~5 seconds)
        /// /// Also enforces 20-minute per-dungeon lockout after death
        /// </summary>
        private void PKDungeonEnforcementTick()
        {
            // Check if player is in a landblock
            if (CurrentLandblock == null || Location == null)
                return;

            var currentLandblock = (ushort)CurrentLandblock.Id.Landblock;
            var currentVariation = Location.Variation ?? 0;

            // Check if current location is a PK-only dungeon
            if (ACE.Server.Entity.Landblock.pkDungeonLandblocks.Contains((currentLandblock, currentVariation)))
            {
                // Admins bypass PK dungeon restrictions
                if (IsAdmin)
                    return;

                string bootReason = null;

                // Check 1: NPK players not allowed
                if (PlayerKillerStatus != PlayerKillerStatus.PK)
                {
                    bootReason = "You have been removed from this PK-only dungeon. You must be PK to remain here.";
                }
                // Check 2: 20-minute dungeon lockout after death
                else
                {
                    var lastDeathLocation = LastPKDungeonDeathLocation ?? 0;
                    var lastDeathTime = LastPKDungeonDeathTime ?? 0;

                    if (lastDeathLocation > 0 && lastDeathTime > 0)
                    {
                        // Unpack the landblock and variation from stored location
                        var deathLandblock = (ushort)(lastDeathLocation >> 16);
                        var deathVariation = (int)(lastDeathLocation & 0xFFFF);

                        // Check if they died in THIS dungeon
                        if (deathLandblock == currentLandblock && deathVariation == currentVariation)
                        {
                            var timeSinceDeath = Time.GetUnixTime() - lastDeathTime;
                            var lockoutDuration = 1200; // 20 minutes in seconds

                            if (timeSinceDeath < lockoutDuration)
                            {
                                var remainingSeconds = (int)(lockoutDuration - timeSinceDeath);
                                var remainingMinutes = (int)Math.Ceiling(remainingSeconds / 60.0);
                                bootReason = $"You died in this dungeon recently. You cannot return for {remainingMinutes} more minute{(remainingMinutes == 1 ? "" : "s")}.";
                            }
                        }
                    }
                }

                // Boot the player if there's a reason
                if (bootReason != null)
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat(bootReason, ChatMessageType.Broadcast));

                    // Teleport to lifestone or last portal
                    if (Sanctuary != null)
                        Teleport(Sanctuary);
                    else if (LastPortal != null)
                        Teleport(LastPortal);
                    else
                    {
                        // Fallback to starter town (Holtburg)
                        var holtburg = new ACE.Entity.Position(0xA9B40019, 84.0f, 7.1f, 94.0f, 0f, 0f, -0.0784591f, 0.996917f);
                        Teleport(holtburg);
                    }
                }
            }
        }

        private bool gagNoticeSent = false;

        public void GagsTick()
        {
            if (IsGagged)
            {
                if (!gagNoticeSent)
                {
                    SendGagNotice();
                    gagNoticeSent = true;
                }

                // check for gag expiration, if expired, remove gag.
                GagDuration -= CachedHeartbeatInterval;

                if (GagDuration <= 0)
                {
                    IsGagged = false;
                    GagTimestamp = 0;
                    GagDuration = 0;
                    SaveBiotaToDatabase();
                    SendUngagNotice();
                    gagNoticeSent = false;
                }
            }
        }

        /// <summary>
        /// Prepare new action to run on this player
        /// </summary>
        public override void EnqueueAction(IAction action)
        {
            actionQueue.EnqueueAction(action);
        }

        /// <summary>
        /// Called every ~5 secs for equipped mana consuming items
        /// </summary>
        public void ManaConsumersTick()
        {
            if (!EquippedObjectsLoaded) return;

            foreach (var item in EquippedObjects.Values)
            {
                if (!item.IsAffecting)
                    continue;

                if (item.ItemCurMana == null || item.ItemMaxMana == null || item.ManaRate == null)
                    continue;

                var burnRate = -item.ManaRate.Value;

                if (LumAugItemManaUsage != 0)
                    burnRate *= GetNegativeRatingMod(LumAugItemManaUsage * 5);

                item.ItemManaRateAccumulator += (float)(burnRate * CachedHeartbeatInterval);

                if (item.ItemManaRateAccumulator < 1)
                    continue;

                var manaToBurn = (int)Math.Floor(item.ItemManaRateAccumulator);

                if (manaToBurn > item.ItemCurMana)
                    manaToBurn = item.ItemCurMana.Value;

                item.ItemCurMana -= manaToBurn;

                item.ItemManaRateAccumulator -= manaToBurn;

                if (item.ItemCurMana > 0)
                    CheckLowMana(item, burnRate);
                else
                    HandleManaDepleted(item);
            }
        }

        private bool CheckLowMana(WorldObject item, double burnRate)
        {
            const int lowManaWarningSeconds = 120;

            var secondsUntilEmpty = item.ItemCurMana / burnRate;

            if (secondsUntilEmpty > lowManaWarningSeconds)
            {
                item.ItemManaDepletionMessage = false;
                return false;
            }
            if (!item.ItemManaDepletionMessage)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {item.Name} is low on Mana.", ChatMessageType.Magic));
                item.ItemManaDepletionMessage = true;
            }
            return true;
        }

        private void HandleManaDepleted(WorldObject item)
        {
            var msg = new GameMessageSystemChat($"Your {item.Name} is out of Mana.", ChatMessageType.Magic);
            var sound = new GameMessageSound(Guid, Sound.ItemManaDepleted);
            Session.Network.EnqueueSend(msg, sound);

            // unsure if these messages / sounds were ever sent in retail,
            // or if it just purged the enchantments invisibly
            // doing a delay here to prevent 'SpellExpired' sounds from overlapping with 'ItemManaDepleted'
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(2.0f);
            actionChain.AddAction(this, ActionType.PlayerTick_RemoveSpellsOnItemManaDepleted, () =>
            {
                foreach (var spellId in item.Biota.GetKnownSpellsIds(item.BiotaDatabaseLock))
                    RemoveItemSpell(item, (uint)spellId);
            });
            actionChain.EnqueueChain();

            item.OnSpellsDeactivated();
        }

        public override void HandleMotionDone(uint motionID, bool success)
        {
            //Console.WriteLine($"{Name}.HandleMotionDone({(MotionCommand)motionID}, {success})");

            if (!FastTick) return;

            if (FoodState.IsChugging)
                HandleMotionDone_UseConsumable(motionID, success);

            if (MagicState.IsCasting)
                HandleMotionDone_Magic(motionID, success);
        }

        // Track accumulated time for PK dungeon quest (saved periodically, not every tick)
        private int _pkDungeonTimeAccumulator = 0;
        private const int PK_DUNGEON_TIME_SAVE_INTERVAL = 60; // Save every 60 seconds

        /// <summary>
        /// CONQUEST: Tracks time spent in PK dungeons for quest progress
        /// Called every heartbeat (~5 seconds) to increment PKDUNGEON_TIME_1H quest
        /// </summary>
        private void PKDungeonQuestTick()
        {
            // Check if player is in a landblock
            if (CurrentLandblock == null || Location == null)
                return;

            var currentLandblock = (ushort)CurrentLandblock.Id.Landblock;
            var currentVariation = Location.Variation ?? 0;

            // Check if current location is a PK-only dungeon
            if (!ACE.Server.Entity.Landblock.pkDungeonLandblocks.Contains((currentLandblock, currentVariation)))
                return;

            // Must be PK to earn time credit
            if (PlayerKillerStatus != PlayerKillerStatus.PK)
                return;

            // Increment time spent in PK dungeon (heartbeat interval is ~5 seconds)
            var timeIncrement = (int)CachedHeartbeatInterval;
            if (timeIncrement <= 0)
                timeIncrement = 5;

            // Check if player has the time quest assigned and not completed
            var timeQuest = PkQuestList.FirstOrDefault(x => x.QuestCode == "PKDUNGEON_TIME_1H");
            if (timeQuest != null && !timeQuest.IsCompleted)
            {
                timeQuest.TaskDoneCount += timeIncrement;
                _pkDungeonTimeAccumulator += timeIncrement;

                var quest = PKQuests.GetPkQuestByCode("PKDUNGEON_TIME_1H");
                if (quest != null && timeQuest.TaskDoneCount >= quest.TaskCount)
                {
                    if (!timeQuest.IsCompleted || !timeQuest.CompletedTime.HasValue)
                    {
                        timeQuest.IsCompleted = true;
                        timeQuest.CompletedTime = DateTime.Now;
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"\nPK Quest Completed: {quest.Description}", ChatMessageType.System));
                        SaveSerializedPkQuestList();
                        _pkDungeonTimeAccumulator = 0;
                    }
                }
                // Only save periodically to avoid excessive database writes
                else if (_pkDungeonTimeAccumulator >= PK_DUNGEON_TIME_SAVE_INTERVAL)
                {
                    SaveSerializedPkQuestList();
                    _pkDungeonTimeAccumulator = 0;
                }
            }
        }
    }
}
