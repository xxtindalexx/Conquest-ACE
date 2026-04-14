# Conquest PvP Configuration Guide

## Table of Contents
1. [Overview](#overview)
2. [Damage Calculation Flow](#damage-calculation-flow)
3. [Configuration Categories](#configuration-categories)
4. [Nether/Void Weapon Balancing](#nethervoid-weapon-balancing)
5. [Imbue and Weapon Property Controls](#imbue-and-weapon-property-controls)
6. [Per-Weapon Type Damage Modifiers](#per-weapon-type-damage-modifiers)
7. [Hollow and Phantom Weapons](#hollow-and-phantom-weapons)
8. [PvP Mode and Augmentation System](#pvp-mode-and-augmentation-system)
9. [Common Scenarios](#common-scenarios)
10. [Troubleshooting](#troubleshooting)

---

## Overview

The Conquest PvP system provides granular control over damage calculations in player-vs-player combat. This allows server operators to balance weapons, imbues, and damage types without affecting PvE gameplay.

**Key Principles:**
- All PvP configs only apply when both attacker AND defender are players
- PvE combat uses standard retail calculations
- Configs can be changed at runtime via /modify commands
- Most multipliers default to `1.0` (no change) or `0` (disabled/no cap)

---

## Damage Calculation Flow

Understanding the order of operations is critical for effective balancing:

```
1. Base Damage Roll (weapon min-max)
2. Attribute Modifier (Strength/Coord based on weapon type)
3. Power/Accuracy Modifier
4. Damage Rating Modifiers (ratings, heritage, recklessness, sneak attack)
5. Critical Hit Check
   └─ If critical: Apply crit damage multipliers
6. PvP Damage Modifiers Applied (pvp_dmg_mod_* configs)
7. Armor Calculation
   └─ Equipment AL + BodyArmorMod (Armor Self/Imperil) + Enchantments
   └─ Armor Cleaving/Rending applied
   └─ For Nether: Synthetic armor override may replace this
8. Resistance Calculation
   └─ Protection spells, vulnerabilities, wards
   └─ For Nether: Synthetic protection may override
9. Shield Modifier (if blocking)
10. Damage Resistance Rating
11. Final Damage = DamageBeforeMitigation × ArmorMod × ResistanceMod × ShieldMod × DRR
```

---

## Configuration Categories

### Boolean Configs (true/false)
Access via: `/modifybool <config_name> <true|false>`

### Double Configs (decimal numbers)
Access via: `/modifydouble <config_name> <value>`

### Long Configs (integers)
Access via: `/modifylong <config_name> <value>`

---

## Nether/Void Weapon Balancing

### The Problem
In retail AC, players could protect against nether damage via:
- Nether Protection spells
- Nether Ward augmentations
- ArmorModVsNether tinkers on armor

On most private servers, these protections don't exist or aren't accessible, making nether weapons extremely overpowered in PvP.

### The Solution
The Conquest system provides synthetic nether protection that simulates properly-protected armor.

### Master Enable Switch

| Config | Type | Default | Description |
|--------|------|---------|-------------|
| `pvp_nether_protection_enabled` | bool | `false` | **Master switch** - Must be `true` for any nether balancing to apply |

### Synthetic Protection Configs

| Config | Type | Default | Description |
|--------|------|---------|-------------|
| `pvp_nether_protection_spell` | double | `0.32` | Simulates Nether Protection VII. Lower = more protection. `0.32` = 68% resistance |
| `pvp_nether_protection_ward` | double | `0.75` | Simulates Legendary Nether Ward. Multiplied with protection spell. `0.75` = additional 25% reduction |

**Combined Effect Example:**
```
ResistanceMod = 0.32 × 0.75 = 0.24 (76% nether resistance)
```

### Per-Weapon Armor Override

These configs REPLACE the normal ArmorMod calculation for nether damage, providing consistent protection regardless of actual armor worn.

| Config | Type | Default | Description |
|--------|------|---------|-------------|
| `pvp_nether_armor_override_heavy` | double | `0` | Heavy Weapons (0 = use actual armor) |
| `pvp_nether_armor_override_light` | double | `0` | Light Weapons |
| `pvp_nether_armor_override_finesse` | double | `0` | Finesse Weapons |
| `pvp_nether_armor_override_2h` | double | `0` | Two-Handed Weapons |
| `pvp_nether_armor_override_bow` | double | `0` | Bows |
| `pvp_nether_armor_override_xbow` | double | `0` | Crossbows |
| `pvp_nether_armor_override_thrown` | double | `0` | Thrown Weapons |
| `pvp_nether_armor_override_atlatl` | double | `0` | Atlatls |

**Value Interpretation:**
- `0` = Disabled, use actual armor calculation
- `0.3` = 70% damage reduction from armor
- `0.5` = 50% damage reduction from armor
- `1.0` = No armor protection

**Important:** These overrides now respect Imperil/Armor Self enchantments. The synthetic AL is calculated, Imperil is applied, then converted back to ArmorMod.

### Per-Weapon Damage Multipliers

| Config | Type | Default | Description |
|--------|------|---------|-------------|
| `pvp_nether_damage_mod_heavy` | double | `1.0` | Damage multiplier for Heavy Weapons |
| `pvp_nether_damage_mod_light` | double | `1.0` | Damage multiplier for Light Weapons |
| `pvp_nether_damage_mod_finesse` | double | `1.0` | Damage multiplier for Finesse Weapons |
| `pvp_nether_damage_mod_2h` | double | `1.0` | Damage multiplier for Two-Handed |
| `pvp_nether_damage_mod_bow` | double | `1.0` | Damage multiplier for Bows |
| `pvp_nether_damage_mod_xbow` | double | `1.0` | Damage multiplier for Crossbows |
| `pvp_nether_damage_mod_thrown` | double | `1.0` | Damage multiplier for Thrown |
| `pvp_nether_damage_mod_atlatl` | double | `1.0` | Damage multiplier for Atlatls |

### Per-Weapon Critical Multipliers

Applied ONLY on critical hits, stacks with damage mod.

| Config | Type | Default | Description |
|--------|------|---------|-------------|
| `pvp_nether_crit_mod_heavy` | double | `1.0` | Crit multiplier for Heavy Weapons |
| `pvp_nether_crit_mod_light` | double | `1.0` | Crit multiplier for Light Weapons |
| `pvp_nether_crit_mod_finesse` | double | `1.0` | Crit multiplier for Finesse Weapons |
| `pvp_nether_crit_mod_2h` | double | `1.0` | Crit multiplier for Two-Handed |
| `pvp_nether_crit_mod_bow` | double | `1.0` | Crit multiplier for Bows |
| `pvp_nether_crit_mod_xbow` | double | `1.0` | Crit multiplier for Crossbows |
| `pvp_nether_crit_mod_thrown` | double | `1.0` | Crit multiplier for Thrown |
| `pvp_nether_crit_mod_atlatl` | double | `1.0` | Crit multiplier for Atlatls |

### Extremity Penalty

| Config | Type | Default | Description |
|--------|------|---------|-------------|
| `pvp_nether_armor_extremity_penalty` | double | `0` | Reduces protection for head/hands/feet hits |

**Example:** Set to `0.05` means extremity hits take 5% more damage than body hits.

---

## Imbue and Weapon Property Controls

### Understanding the Difference

**Weapon Properties** (from item stats):
- `CriticalFrequency` (PropertyFloat 147) = "Biting Strike" effect
- `CriticalMultiplier` (PropertyFloat 136) = "Crushing Blow" effect
- `IgnoreArmor` (PropertyFloat 155) = "Armor Cleaving" effect

**Imbues** (from skill-based tinkering):
- Critical Strike = Increases crit chance based on imbue skill
- Crippling Blow = Increases crit damage based on imbue skill
- Armor Rending = Penetrates armor based on imbue skill

### Disable Weapon Properties (Keep Imbues)

These completely disable the ITEM PROPERTY in PvP while allowing the IMBUE to still function.

| Config | Type | Default | Description |
|--------|------|---------|-------------|
| `pvp_disable_biting_strike` | bool | `true` | Disables CriticalFrequency property, Critical Strike imbue still works |
| `pvp_disable_crushing_blow` | bool | `true` | Disables CriticalMultiplier property, Crippling Blow imbue still works |
| `pvp_disable_armor_cleaving` | bool | `true` | Disables IgnoreArmor property, Armor Rending imbue still works |

**Why disable properties but keep imbues?**
- Item properties are static bonuses that can stack to extreme levels
- Imbues scale with skill, providing more balanced progression
- Allows skill investment to matter in PvP

### Effect Caps

If you prefer to CAP rather than disable, use these (only apply if disable is `false`):

| Config | Type | Default | Description |
|--------|------|---------|-------------|
| `pvp_max_biting_strike` | double | `0` | Max crit chance from property (0 = no cap) |
| `pvp_max_crushing_blow` | double | `0` | Max crit multiplier from property (0 = no cap) |
| `pvp_max_armor_cleaving` | double | `0` | Max armor bypass (e.g., 0.5 = max 50% ignored) |
| `pvp_max_critical_strike` | double | `0` | Max crit chance from imbue |
| `pvp_max_crippling_blow` | double | `0` | Max crit damage from imbue |
| `pvp_max_armor_rend` | double | `0` | Max armor penetration from imbue |

---

## Per-Weapon Type Damage Modifiers

### Global Imbue Modifiers

| Config | Type | Default | Description |
|--------|------|---------|-------------|
| `pvp_dmg_mod_ar` | double | `1.0` | Global Armor Rending modifier |
| `pvp_dmg_mod_cb` | double | `1.0` | Global Crippling Blow modifier |
| `pvp_dmg_mod_cs` | double | `1.0` | Global Critical Strike modifier |
| `pvp_dmg_mod_bs` | double | `1.0` | Global Biting Strike modifier |
| `pvp_dmg_mod_hollow` | double | `1.0` | Global Hollow weapon modifier |
| `pvp_dmg_mod_phantom` | double | `1.0` | Global Phantom weapon modifier |

### Weapon-Specific Modifiers

Format: `pvp_dmg_mod_<weapon>_<imbue>` and `pvp_dmg_mod_<weapon>_<imbue>_crit`

**Finesse Weapons (fw):**
| Config | Description |
|--------|-------------|
| `pvp_dmg_mod_fw` | Base damage modifier |
| `pvp_dmg_mod_fw_ar` | Armor Rending modifier |
| `pvp_dmg_mod_fw_cb` | Crippling Blow modifier |
| `pvp_dmg_mod_fw_cb_crit` | Crippling Blow crit modifier |
| `pvp_dmg_mod_fw_cs` | Critical Strike modifier |
| `pvp_dmg_mod_fw_cs_crit` | Critical Strike crit modifier |
| `pvp_dmg_mod_fw_bs` | Biting Strike modifier |
| `pvp_dmg_mod_fw_bs_crit` | Biting Strike crit modifier |

**Light Weapons (lw):** Same pattern as above with `lw` prefix
**Heavy Weapons (hw):** Same pattern with `hw` prefix
**Two-Handed (2h):** Same pattern with `2h` prefix
**Bow:** Same pattern with `bow` prefix
**Crossbow (xbow):** Same pattern with `xbow` prefix
**Thrown (tw):** Same pattern with `tw` prefix
**Atlatl:** Same pattern with `atlatl` prefix

---

## Hollow and Phantom Weapons

### Hollow Weapons (IgnoreMagicResist)

Hollow weapons bypass life magic enchantments (Armor Self, Imperil, etc.).

| Config | Type | Default | Description |
|--------|------|---------|-------------|
| `ignore_magic_resist_pvp_scalar` | double | `1.0` | Scales hollow effectiveness. `1.0` = full bypass, `0.5` = half bypass, `0` = no bypass |

### Phantom Weapons (IgnoreAllArmor)

Phantom weapons completely bypass armor.

| Config | Type | Default | Description |
|--------|------|---------|-------------|
| `pvp_disable_ignore_all_armor` | bool | `true` | If true, phantom effect disabled in PvP |
| `ignore_magic_armor_pvp_scalar` | double | `1.0` | Scales armor enchantment bypass |

---

## PvP Mode and Augmentation System

### Automatic PvP Mode

When players enter PvP combat, the system can automatically:
1. Store current augmentation values
2. Zero out augmentations for balanced combat
3. Silently rebuff the player
4. Restore augmentations when PvP ends

| Config | Type | Default | Description |
|--------|------|---------|-------------|
| `pvp_disable_custom_augs` | bool | `false` | Enable/disable aug zeroing in PvP |
| `pvp_custom_aug_timeout` | long | `300` | Seconds without combat before exiting PvP mode |

### Rare Dispelling

| Config | Type | Default | Description |
|--------|------|---------|-------------|
| `dispel_rares_pvp` | bool | `false` | Auto-dispel rare gem buffs when PvP starts |

---

## Common Scenarios

### Scenario 1: Nerf Nether Weapons Significantly

Goal: Make nether weapons do ~50% of their normal damage in PvP.

```sql
-- Enable nether protection system
UPDATE config_properties_boolean SET value = 1 WHERE key = 'pvp_nether_protection_enabled';

-- Set strong resistance (0.5 = 50% resistance from spells)
UPDATE config_properties_double SET value = 0.5 WHERE key = 'pvp_nether_protection_spell';

-- Set ward multiplier (0.6 = additional 40% reduction)
UPDATE config_properties_double SET value = 0.6 WHERE key = 'pvp_nether_protection_ward';

-- Or use in-game commands:
/modifybool pvp_nether_protection_enabled true
/modifydouble pvp_nether_protection_spell 0.5
/modifydouble pvp_nether_protection_ward 0.6
```

### Scenario 2: Balance Bows vs Melee

Goal: Bows are too strong, reduce their damage by 20%.

```
/modifydouble pvp_dmg_mod_bow 0.8
```

### Scenario 3: Disable All "Pay-to-Win" Item Properties

Goal: Only allow skill-based imbues, not item stat bonuses.

```
/modifybool pvp_disable_biting_strike true
/modifybool pvp_disable_crushing_blow true
/modifybool pvp_disable_armor_cleaving true
/modifybool pvp_disable_ignore_all_armor true
```

### Scenario 4: Cap Critical Damage

Goal: Crits are doing too much damage, cap at 3x.

```
/modifydouble pvp_max_crippling_blow 3.0
/modifydouble pvp_max_crushing_blow 2.0
```

### Scenario 5: Make Finesse Weapons Weaker Than Heavy

Goal: Finesse should do 15% less damage than heavy weapons.

```
/modifydouble pvp_dmg_mod_fw 0.85
/modifydouble pvp_dmg_mod_hw 1.0
```

### Scenario 6: Buff Underused Weapon Types

Goal: Thrown weapons and atlatls need a buff.

```
/modifydouble pvp_dmg_mod_tw 1.25
/modifydouble pvp_dmg_mod_atlatl 1.25
```

### Scenario 7: Nether Bows Too Strong, Nether Melee Fine

Goal: Only nerf nether damage from ranged weapons.

```
/modifybool pvp_nether_protection_enabled true
/modifydouble pvp_nether_damage_mod_bow 0.7
/modifydouble pvp_nether_damage_mod_xbow 0.7
/modifydouble pvp_nether_damage_mod_thrown 0.7
/modifydouble pvp_nether_damage_mod_atlatl 0.7
-- Leave melee at 1.0 (default)
```

---

## Troubleshooting

### Debug Damage Command

Use `/debugdamage` to see detailed damage calculations:

1. Examine (appraise) the target player
2. Run `/debugdamage all`
3. Attack or be attacked
4. Check chat for damage breakdown

**Key values to check:**
- `BodyArmorMod`: Shows Armor Self (+) or Imperil (-) effect
- `ArmorMod`: Final armor damage multiplier (lower = more protection)
- `ResistanceMod`: Resistance multiplier (lower = more protection)
- `DamageBeforeMitigation`: Damage before armor/resistance
- `Damage`: Final damage dealt

### Common Issues

**Issue: Nether protection not working**
- Check `pvp_nether_protection_enabled` is `true`
- Verify both players are flagged PK
- Check if armor override is set to `0` (disabled)

**Issue: Imbue still working after disable**
- Disable configs only affect ITEM PROPERTIES, not imbues
- Use `pvp_max_*` configs to cap imbue effects
- Or use `pvp_dmg_mod_*` to reduce imbue damage

**Issue: Damage too high/low after config change**
- Remember multipliers stack: `base × global × weapon × imbue × crit`
- Check all applicable modifiers
- Use `/debugdamage` to trace the calculation

**Issue: Config change not applying**
- Configs apply immediately, no restart needed
- Verify spelling matches exactly (case-sensitive)
- Check you're modifying the right type (bool vs double)

### Viewing Current Config Values

```
/serverinfo <config_name>
```

Or check database directly:
```sql
SELECT * FROM config_properties_double WHERE key LIKE 'pvp_%';
SELECT * FROM config_properties_boolean WHERE key LIKE 'pvp_%';
```

---

## Quick Reference Card

### Most Common Configs

| Config | What It Does | Typical Values |
|--------|--------------|----------------|
| `pvp_nether_protection_enabled` | Enable nether balancing | `true` / `false` |
| `pvp_nether_protection_spell` | Nether resistance | `0.32` (68% resist) |
| `pvp_disable_biting_strike` | Disable item crit chance | `true` |
| `pvp_disable_crushing_blow` | Disable item crit damage | `true` |
| `pvp_dmg_mod_<weapon>` | Scale weapon damage | `0.8` = -20%, `1.2` = +20% |

### Formula Reference

**Armor Modifier:**
```
If AL > 0: ArmorMod = 66.67 / (AL + 66.67)
If AL < 0: ArmorMod = 1.0 + |AL| / 66.67
If AL = 0: ArmorMod = 1.0
```

**Damage Multiplier Stack:**
```
FinalMod = GlobalMod × WeaponTypeMod × ImbueTypeMod × (IsCrit ? CritMod : 1.0)
```

---

*Document Version: 1.0*
*Last Updated: 2026-04-11*
*For Conquest ACE Server*
