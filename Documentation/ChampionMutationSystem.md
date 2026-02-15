# Champion Mutation System - Implementation Documentation

## Overview
The Champion Mutation System is an ARPG-style feature that allows creatures to spawn as enhanced "champion" variants with increased stats, special abilities, and better rewards.

## Implementation Status
**Status:** Core implementation complete, ready for testing

## Files Created

### 1. ChampionMutationType.cs
**Path:** `ACE.Server\Factories\Enum\ChampionMutationType.cs`

Flags enum defining all mutation types:
- **Offensive Mutations (T1-T8):** Menacing, Fierce, Vicious, Deadly, Ruthless, Merciless, Brutal, Devastating
- **Defensive Mutations (T1-T8):** Stalwart, Hardened, Protected, Sturdy, Solid, Fortified, Ironclad, Adamant
- **Critical Mutations (T1-T8):** Sharp, Keen, Piercing, Razor, Acute, Critical, Lethal, Relentless
- **Special Mutations:** Rich, Tempest, Enraged

### 2. ChampionManager.cs
**Path:** `ACE.Server\Managers\ChampionManager.cs`

Static manager class that:
- Builds wcid→tier cache on server startup (queries creature DeathTreasureType → treasure_death.Tier)
- Provides `GetTierForWcid(uint wcid)` for tier lookup
- Provides `RollChampionTier(uint wcid)` - rolls 1 to maxTier based on creature's treasure tier
- Provides `RollMutations(int tier)` - rolls which mutations to apply
- Provides bonus multiplier calculations (health, XP, loot quality)

### 3. Creature_Champion.cs
**Path:** `ACE.Server\WorldObjects\Creature_Champion.cs`

Partial class for Creature that handles:
- `IsChampion` property
- `ChampionTier` property
- `ChampionMutations` property
- `ApplyChampionMutation()` - main method that applies all champion effects
- `ApplyChampionStatBonuses()` - applies DR, defense, crit bonuses
- `ApplyChampionVisuals()` - size increase, radar color
- `ApplyChampionAbilities()` - Tempest (SpellChain/SplitArrow), Enraged (leap, etc.)
- `GetChampionXpMultiplier()` / `GetChampionLuminanceMultiplier()` - for death rewards
- `AnnounceChampionSpawn()` - broadcasts to nearby players

## Files Modified

### 1. PropertyInt.cs
**Path:** `ACE.Entity\Enum\Properties\PropertyInt.cs`

Added:
```csharp
ChampionSpawnChance = 9400,            // Generator property - % chance (0-100)
ChampionTier = 9401,                   // Creature property - tier (1-8)
ChampionMutationFlags = 9402,          // Creature property - ChampionMutationType flags
ChampionBonusDamageRating = 9403,      // Bonus DR from mutation
ChampionBonusDefenseRating = 9404,     // Bonus defense from mutation
ChampionBonusCritDamageRating = 9405,  // Bonus crit DR from mutation
```

### 2. PropertyBool.cs
**Path:** `ACE.Entity\Enum\Properties\PropertyBool.cs`

Added:
```csharp
ChampionEnabled = 9200,    // Generator property - enables champion spawns
IsChampion = 9201,         // Creature property - this is a champion
```

### 3. PropertyFloat.cs
**Path:** `ACE.Entity\Enum\Properties\PropertyFloat.cs`

Added:
```csharp
ChampionHealthMod = 9400,      // Health multiplier (e.g., 1.5 = +50%)
ChampionXpMod = 9401,          // XP multiplier
ChampionLuminanceMod = 9402,   // Luminance multiplier
ChampionLootQualityMod = 9403, // Loot quality modifier
```

### 4. GeneratorProfile.cs
**Path:** `ACE.Server\Entity\GeneratorProfile.cs`

- Added `using ACE.Common;`
- Modified `Spawn()` method to call `TryApplyChampionMutation()` after creature creation
- Added `TryApplyChampionMutation(Creature creature)` method that checks generator properties and rolls for champion spawn

### 5. Creature_Death.cs
**Path:** `ACE.Server\WorldObjects\Creature_Death.cs`

- Modified `OnDeath_GrantXP()` to apply `GetChampionXpMultiplier()` to XP rewards
- Modified luminance calculation to apply `GetChampionLuminanceMultiplier()`

### 6. Program.cs
**Path:** `ACE.Server\Program.cs`

- Added `ChampionManager.Initialize()` call after database caches are built

## How It Works

### Spawn Flow
1. Generator spawns a creature via `GeneratorProfile.Spawn()`
2. After creature is created, `TryApplyChampionMutation()` is called
3. Checks if generator has `ChampionEnabled = true`
4. Rolls against `ChampionSpawnChance` (default 10%)
5. If successful, calls `creature.ApplyChampionMutation()`

### Mutation Application
1. `RollChampionTier()` - determines tier (1 to creature's treasure tier)
2. `RollMutations()` - determines which mutations to apply:
   - Always one primary (offensive, defensive, or critical)
   - 30% chance for second primary type
   - 10% chance for Rich mutation
   - 10% chance for Tempest mutation
   - 5% chance for Enraged mutation
3. Apply stat bonuses based on tier
4. Apply visual effects (size +15%, red radar)
5. Apply special abilities if applicable
6. Rename creature with mutation prefix (e.g., "Deadly Drudge Stalker")

### Tier-Based Scaling

#### Base Stats
| Tier | Health Bonus | XP/Lum Bonus | Damage Rating | Defense Rating | Crit DR |
|------|--------------|--------------|---------------|----------------|---------|
| T1   | +25%         | +25%         | +1            | +1             | +1      |
| T2   | +36%         | +36%         | +2            | +2             | +2      |
| T3   | +46%         | +46%         | +3            | +3             | +3      |
| T4   | +57%         | +57%         | +4            | +4             | +4      |
| T5   | +68%         | +68%         | +5            | +5             | +5      |
| T6   | +79%         | +79%         | +6            | +6             | +6      |
| T7   | +89%         | +89%         | +7            | +7             | +7      |
| T8   | +100%        | +100%        | +8            | +8             | +8      |

#### Enraged Mutation Scaling
| Tier | Enrage Threshold | Dmg Multiplier | Dmg Reduction | Leap Damage | Leap Interval | Warning Time |
|------|------------------|----------------|---------------|-------------|---------------|--------------|
| T1   | 50%              | 1.10x          | 5%            | 1000        | 35s           | 4.0s         |
| T2   | 52%              | 1.15x          | 8%            | 1500        | 33s           | 3.8s         |
| T3   | 54%              | 1.20x          | 11%           | 2000        | 31s           | 3.6s         |
| T4   | 56%              | 1.25x          | 14%           | 2500        | 29s           | 3.4s         |
| T5   | 58%              | 1.30x          | 17%           | 3000        | 27s           | 3.2s         |
| T6   | 60%              | 1.35x          | 19%           | 3500        | 25s           | 3.0s         |
| T7   | 62%              | 1.40x          | 22%           | 4000        | 23s           | 2.8s         |
| T8   | 64%              | 1.45x          | 25%           | 4500        | 21s           | 2.6s         |

#### Mirror Image Scaling
| Tier | Clone Count | Spawn Interval | Clone Health | Clone Damage | Immune During Clones |
|------|-------------|----------------|--------------|--------------|----------------------|
| T1-2 | 1           | 60s / 56s      | 15-18%       | 20-24%       | No                   |
| T3-5 | 2           | 52s / 44s      | 21-27%       | 28-36%       | No                   |
| T6-8 | 3           | 40s / 30s      | 30-35%       | 40-50%       | Yes                  |

#### Tempest Mutation Scaling
| Tier | Split Arrow / Chain Targets |
|------|----------------------------|
| T1-3 | 2                          |
| T4-5 | 3                          |
| T6-8 | 4                          |

## How to Enable Champions

### On a Generator Weenie
Set these properties on the generator:
```sql
-- Enable champion spawns
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (@generator_wcid, 9200, 1);  -- ChampionEnabled = true

-- Set spawn chance (10% default)
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (@generator_wcid, 9400, 10);  -- ChampionSpawnChance = 10
```

### Via SQL for Existing Generators
```sql
-- Enable champions on all creature generators in a specific landblock
UPDATE weenie_properties_bool wpb
JOIN weenie w ON wpb.object_Id = w.class_Id
SET wpb.value = 1
WHERE wpb.type = 9200
AND w.class_Id IN (SELECT generator_wcid FROM landblock_instances WHERE landblock = 0xABCD);
```

## Future Enhancements (TODO)
- [ ] Loot quality modifier integration with LootGenerationFactory
- [ ] Aetheria surge visual effect (persistent particle effect)
- [ ] Champion death announcement
- [ ] Configurable mutation chances via PropertyManager
- [ ] Champion tracking/statistics
- [ ] Rare "Golden" mutation type with guaranteed rare drops

## Testing Commands (To Be Added)
- `/champion spawn <wcid> [tier]` - Spawn a champion creature
- `/champion info` - Show champion stats of targeted creature
- `/champion toggle <generator_guid>` - Toggle champion spawns on generator
