using System;

using ACE.Entity;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    public class DamageHistoryInfo
    {
        public readonly WeakReference<WorldObject> Attacker;

        public readonly ObjectGuid Guid;
        public readonly string Name;

        public float TotalDamage;

        public readonly WeakReference<Player> PetOwner;

        public bool IsPlayer => Guid.IsPlayer();

        /// <summary>
        /// Returns true if the attacker is a player OR if it's a pet owned by a player
        /// </summary>
        public bool IsPlayerOrPetOfPlayer => IsPlayer || PetOwner != null;

        public readonly bool IsOlthoiPlayer;

        public DamageHistoryInfo(WorldObject attacker, float totalDamage = 0.0f)
        {
            Attacker = new WeakReference<WorldObject>(attacker);

            Guid = attacker.Guid;
            Name = attacker.Name;

            IsOlthoiPlayer = attacker is Player player && player.IsOlthoiPlayer;

            TotalDamage = totalDamage;

            if (attacker is CombatPet combatPet && combatPet.P_PetOwner != null)
                PetOwner = new WeakReference<Player>(combatPet.P_PetOwner);
        }

        public WorldObject TryGetAttacker()
        {
            Attacker.TryGetTarget(out var attacker);

            return attacker;
        }

        public Player TryGetPetOwner()
        {
            PetOwner.TryGetTarget(out var petOwner);

            return petOwner;
        }

        public WorldObject TryGetPetOwnerOrAttacker()
        {
            if (PetOwner != null)
                return TryGetPetOwner();
            else
                return TryGetAttacker();
        }
    }
}
