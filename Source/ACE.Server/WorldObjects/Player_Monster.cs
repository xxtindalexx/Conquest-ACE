using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Server.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Handles player->monster visibility checks
    /// </summary>
    partial class Player
    {
        /// <summary>
        /// Wakes up any monsters within the applicable range
        /// </summary>
        public void CheckMonsters()
        {
            if (!Attackable || Teleporting) return;

            // CONQUEST: Check both visible objects AND creatures from adjacent landblocks
            // This ensures mobs detect the player even when standing at landblock boundaries
            var visibleObjs = PhysicsObj.ObjMaint.GetVisibleObjectsValuesOfTypeCreature();
            var monstersToCheck = new HashSet<Creature>(visibleObjs);

            // Also check creatures from adjacent landblocks with compatible variations
            if (CurrentLandblock != null)
            {
                foreach (var adjacent in CurrentLandblock.Adjacents)
                {
                    if (adjacent == null) continue;

                    // Only check adjacents with compatible variation
                    if (!AreVariationsCompatible(Location.Variation, adjacent.VariationId))
                        continue;

                    foreach (var wo in adjacent.GetWorldObjectsForPhysicsHandling())
                    {
                        if (wo is Creature creature && !(creature is Player) && creature.IsAlive)
                            monstersToCheck.Add(creature);
                    }
                }
            }

            foreach (var monster in monstersToCheck)
            {
                if (monster is Player) continue;

                //if (Location.SquaredDistanceTo(monster.Location) <= monster.VisualAwarenessRangeSq)
                if (PhysicsObj.get_distance_sq_to_object(monster.PhysicsObj, true) <= monster.VisualAwarenessRangeSq)
                    AlertMonster(monster);
            }
        }

        /// <summary>
        /// Called when this player attacks a monster
        /// </summary>
        public void OnAttackMonster(Creature monster)
        {
            if (monster == null || !Attackable) return;

            /*Console.WriteLine($"{Name}.OnAttackMonster({monster.Name})");
            Console.WriteLine($"Attackable: {monster.Attackable}");
            Console.WriteLine($"Tolerance: {monster.Tolerance}");*/

            // faction mobs will retaliate against players belonging to the same faction
            if (SameFaction(monster))
                monster.AddRetaliateTarget(this);

            // CONQUEST: Ensure player is added to monster's VisibleTargets for cross-landblock attacks
            // This allows the monster to properly track and chase the attacker even if they're
            // on a different landblock
            if (monster.PhysicsObj?.ObjMaint != null && PhysicsObj != null)
                monster.PhysicsObj.ObjMaint.AddVisibleTargets(new[] { PhysicsObj });

            if (monster.MonsterState != State.Awake && (monster.Tolerance & PlayerCombatPet_RetaliateExclude) == 0)
            {
                monster.AttackTarget = this;
                monster.WakeUp();
            }
        }
    }
}
